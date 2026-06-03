// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Forms;

/// <summary>
/// Validates a <see cref="FieldRecord"/> against a <see cref="FormDefinition"/>.
/// </summary>
public static class FormValidator
{
    private static readonly TimeSpan RegexEvaluationTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Validates every visible field in a form, including each captured row of a
    /// repeatable section.
    /// </summary>
    /// <param name="form">Form definition.</param>
    /// <param name="record">Record values to validate.</param>
    /// <returns>
    /// Validation result with field-level errors. Errors raised inside a
    /// repeatable section are reported with a field id of the form
    /// <c>sectionId[index].fieldId</c>.
    /// </returns>
    public static FormValidationResult Validate(FormDefinition form, FieldRecord record)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(record);

        var errors = new List<FormValidationError>();

        foreach (var section in form.Sections)
        {
            if (section.Repeatable)
            {
                ValidateRepeatSection(errors, section, record);
                continue;
            }

            foreach (var field in section.Fields)
            {
                ValidateField(errors, field, record.Values, record.Media, prefix: null);
            }
        }

        return new FormValidationResult(errors);
    }

    private static void ValidateRepeatSection(List<FormValidationError> errors, FormSection section, FieldRecord record)
    {
        if (!record.Repeats.TryGetValue(section.SectionId, out var instances) || instances is null)
        {
            return;
        }

        for (var index = 0; index < instances.Count; index++)
        {
            var instance = instances[index];
            var prefix = $"{section.SectionId}[{index}].";
            foreach (var field in section.Fields)
            {
                ValidateField(errors, field, instance.Values, instance.Media, prefix);
            }
        }
    }

    private static void ValidateField(
        List<FormValidationError> errors,
        FormField field,
        Dictionary<string, object?> values,
        IEnumerable<FieldMediaAttachment> media,
        string? prefix)
    {
        var local = new List<FormValidationError>();

        var isVisible = IsVisible(field, values);
        values.TryGetValue(field.FieldId, out var rawValue);
        var mediaCount = CountMedia(media, field);

        if (field.Required && isVisible && IsMissing(rawValue) && mediaCount == 0)
        {
            local.Add(new FormValidationError(field.FieldId, $"{field.Label} is required."));
        }
        else if (isVisible && (!IsMissing(rawValue) || mediaCount > 0))
        {
            if (!IsMissing(rawValue))
            {
                ValidateType(local, field, rawValue);
                ValidateRules(local, field, rawValue);
                ValidateConstraintExpression(local, field, rawValue, values);
            }

            ValidateMediaRules(local, field, mediaCount);
        }

        foreach (var error in local)
        {
            errors.Add(prefix is null ? error : new FormValidationError(prefix + error.FieldId, error.Message));
        }
    }

    private static bool IsVisible(FormField field, Dictionary<string, object?> values)
    {
        // A boolean relevance expression, when present, supersedes the single-comparison rule.
        if (!string.IsNullOrWhiteSpace(field.RelevanceExpression))
        {
            return Expressions.ExpressionEvaluator.EvaluateBoolean(field.RelevanceExpression, values);
        }

        if (field.VisibilityRule is null)
        {
            return true;
        }

        if (!values.TryGetValue(field.VisibilityRule.DependsOnFieldId, out var actual))
        {
            return false;
        }

        var expected = field.VisibilityRule.MatchValue;

        return field.VisibilityRule.Operator switch
        {
            ComparisonOperator.Equals => AreEqual(actual, expected),
            ComparisonOperator.NotEquals => !AreEqual(actual, expected),
            ComparisonOperator.GreaterThan => TryAsDouble(actual, out var a) && TryAsDouble(expected, out var b) && a > b,
            ComparisonOperator.LessThan => TryAsDouble(actual, out var c) && TryAsDouble(expected, out var d) && c < d,
            ComparisonOperator.Contains => ConvertToString(actual).Contains(ConvertToString(expected), StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private static void ValidateType(List<FormValidationError> errors, FormField field, object? rawValue)
    {
        switch (field.Type)
        {
            case FormFieldType.Numeric when !TryAsDouble(rawValue, out _):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be numeric."));
                break;
            case FormFieldType.YesNo when !TryAsBoolean(rawValue):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be true/false."));
                break;
            case FormFieldType.SingleChoice when field.Choices.Count > 0 && !ContainsChoice(field, rawValue):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must match an allowed choice."));
                break;
            case FormFieldType.MultipleChoice when !TryGetStringValues(rawValue, out _):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must provide a list of choices."));
                break;
            case FormFieldType.MultipleChoice when field.Choices.Count > 0 && !AllChoicesAllowed(field, rawValue):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} contains a value outside the allowed choices."));
                break;
            case FormFieldType.Hyperlink when !Uri.TryCreate(ConvertToString(rawValue), UriKind.Absolute, out _):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be a valid URL."));
                break;
            case FormFieldType.Date when !DateOnly.TryParse(ConvertToString(rawValue), CultureInfo.InvariantCulture, DateTimeStyles.None, out _):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be a date."));
                break;
            case FormFieldType.Time when !TimeOnly.TryParse(ConvertToString(rawValue), CultureInfo.InvariantCulture, DateTimeStyles.None, out _):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be a time."));
                break;
            case FormFieldType.DateTime when !DateTimeOffset.TryParse(ConvertToString(rawValue), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _):
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be a date/time."));
                break;
        }
    }

    private static void ValidateRules(List<FormValidationError> errors, FormField field, object? rawValue)
    {
        var text = ConvertToString(rawValue);

        if (!string.IsNullOrWhiteSpace(field.Validation.RegexPattern) &&
            !IsRegexMatch(text, field.Validation.RegexPattern!, out var regexErrorMessage))
        {
            errors.Add(new FormValidationError(field.FieldId, regexErrorMessage ?? $"{field.Label} does not match the required format."));
        }

        if (field.Validation.MinLength is { } minLength && text.Length < minLength)
        {
            errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be at least {minLength} character(s)."));
        }

        if (field.Validation.MaxLength is { } maxLength && text.Length > maxLength)
        {
            errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be at most {maxLength} character(s)."));
        }

        if (TryAsDouble(rawValue, out var numericValue))
        {
            if (field.Validation.MinNumericValue is { } min && numericValue < min)
            {
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be >= {min}."));
            }

            if (field.Validation.MaxNumericValue is { } max && numericValue > max)
            {
                errors.Add(new FormValidationError(field.FieldId, $"{field.Label} must be <= {max}."));
            }
        }
    }

    private static void ValidateConstraintExpression(
        List<FormValidationError> errors,
        FormField field,
        object? rawValue,
        Dictionary<string, object?> values)
    {
        if (string.IsNullOrWhiteSpace(field.Validation.ConstraintExpression))
        {
            return;
        }

        // Overlay a "." self-reference (and keep $thisFieldId resolving via the field id)
        // without mutating the caller's record values.
        var context = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase)
        {
            ["."] = rawValue,
        };

        if (!Expressions.ExpressionEvaluator.EvaluateBoolean(field.Validation.ConstraintExpression, context))
        {
            errors.Add(new FormValidationError(
                field.FieldId,
                field.Validation.ConstraintMessage ?? $"{field.Label} does not satisfy its constraint."));
        }
    }

    private static void ValidateMediaRules(List<FormValidationError> errors, FormField field, int mediaCount)
    {
        if (field.Validation.MinMediaCount is { } minMedia && mediaCount < minMedia)
        {
            errors.Add(new FormValidationError(field.FieldId, $"{field.Label} requires at least {minMedia} media item(s)."));
        }

        if (field.Validation.MaxMediaCount is { } maxMedia && mediaCount > maxMedia)
        {
            errors.Add(new FormValidationError(field.FieldId, $"{field.Label} allows at most {maxMedia} media item(s)."));
        }
    }

    private static int CountMedia(IEnumerable<FieldMediaAttachment> media, FormField field)
    {
        if (!IsMediaField(field.Type))
        {
            return 0;
        }

        return media.Count(item =>
            string.IsNullOrWhiteSpace(item.FieldId) ||
            string.Equals(item.FieldId, field.FieldId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMediaField(FormFieldType fieldType)
        => fieldType is FormFieldType.Photo or FormFieldType.Video or FormFieldType.Audio or FormFieldType.Signature or FormFieldType.Sketch or FormFieldType.File;

    private static bool ContainsChoice(FormField field, object? rawValue)
    {
        var value = ConvertToString(rawValue);
        return field.Choices.Any(choice => string.Equals(choice.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool AllChoicesAllowed(FormField field, object? rawValue)
        => TryGetStringValues(rawValue, out var values) &&
           values.All(value => field.Choices.Any(choice => string.Equals(choice.Value, value, StringComparison.OrdinalIgnoreCase)));

    private static bool IsMissing(object? value)
    {
        return value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => true,
            JsonElement { ValueKind: JsonValueKind.String } element => string.IsNullOrWhiteSpace(element.GetString()),
            JsonElement { ValueKind: JsonValueKind.Array } element => !element.EnumerateArray().Any(),
            IEnumerable collection when value is not string => !collection.Cast<object?>().Any(),
            _ => false,
        };
    }

    private static bool TryAsDouble(object? value, out double parsed)
    {
        switch (value)
        {
            case null:
                parsed = default;
                return false;
            case double d:
                parsed = d;
                return true;
            case float f:
                parsed = f;
                return true;
            case decimal m:
                parsed = (double)m;
                return true;
            case int i:
                parsed = i;
                return true;
            case long l:
                parsed = l;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } element:
                return element.TryGetDouble(out parsed);
            case JsonElement { ValueKind: JsonValueKind.String } element:
                return double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
            default:
                return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed);
        }
    }

    private static bool TryAsBoolean(object? value)
        => value is bool ||
           value is JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False } ||
           bool.TryParse(ConvertToString(value), out _);

    private static bool TryGetStringValues(object? value, out IReadOnlyList<string> values)
    {
        switch (value)
        {
            case IEnumerable<string> strings:
                values = strings.ToArray();
                return true;
            case JsonElement { ValueKind: JsonValueKind.Array } element:
                values = element.EnumerateArray().Select(ConvertJsonElementToString).ToArray();
                return true;
            default:
                values = [];
                return false;
        }
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (TryAsDouble(left, out var l) && TryAsDouble(right, out var r))
        {
            return Math.Abs(l - r) < 0.000001;
        }

        return string.Equals(ConvertToString(left), ConvertToString(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string ConvertToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            JsonElement element => ConvertJsonElementToString(element),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string ConvertJsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText(),
        };
    }

    private static bool IsRegexMatch(string input, string pattern, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.None, RegexEvaluationTimeout);
        }
        catch (ArgumentException)
        {
            errorMessage = "Validation pattern is invalid.";
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            errorMessage = "Validation pattern timed out.";
            return false;
        }
    }
}

/// <summary>
/// Result of validating a field record.
/// </summary>
public sealed class FormValidationResult
{
    /// <summary>
    /// Initializes a validation result.
    /// </summary>
    /// <param name="errors">Validation errors.</param>
    public FormValidationResult(IReadOnlyList<FormValidationError> errors)
    {
        Errors = errors;
    }

    /// <summary>Validation errors. Empty means the record is valid.</summary>
    public IReadOnlyList<FormValidationError> Errors { get; }

    /// <summary>Whether no validation errors were found.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Field-level validation error.
/// </summary>
/// <param name="FieldId">Field identifier.</param>
/// <param name="Message">Validation message.</param>
public sealed record FormValidationError(string FieldId, string Message);
