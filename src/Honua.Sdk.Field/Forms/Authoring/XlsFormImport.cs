// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Forms.Authoring;

internal sealed class XlsFormImport(XlsFormWorkbook workbook)
{
    private readonly List<XlsFormDiagnostic> _errors = [];
    private readonly List<FormSection> _sections = [];
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<FieldChoice>> _choices = new(StringComparer.Ordinal);
    private List<FormField> _fields = [];
    private XlsFormRow? _group;
    private int _ungrouped;

    public XlsFormResult<FormDefinition> Convert()
    {
        var settings = ReadSettings();
        ReadChoices();
        foreach (var row in workbook.Survey.Where(r => r.Cells.Values.Any(v => !string.IsNullOrWhiteSpace(v))))
        {
            ReadSurveyRow(row);
        }

        if (_group is not null)
        {
            _errors.Error(("survey", _group, "type"), "Group or repeat is not closed.");
        }

        FlushUngrouped();
        CheckReferences();
        CheckCalculationOrder();
        if (_sections.Sum(s => s.Fields.Count) == 0)
        {
            _errors.Error(("survey", XlsFormCells.Row(1), "type"), "The survey must contain at least one supported field.");
        }

        return new XlsFormResult<FormDefinition>
        {
            Diagnostics = _errors,
            Value = _errors.Count > 0 ? null : new FormDefinition
            {
                FormId = settings.Text("form_id"), Name = settings.Text("form_title"),
                Version = settings.Optional("version"), Sections = _sections
            }
        };
    }

    private XlsFormRow ReadSettings()
    {
        var rows = workbook.Settings.Where(r => r.Cells.Values.Any(v => !string.IsNullOrWhiteSpace(v))).ToArray();
        var row = rows.FirstOrDefault() ?? XlsFormCells.Row(2);
        foreach (var settings in rows)
        {
            _errors.CheckColumns(("settings", settings), "form_id", "form_title", "version");
        }

        if (rows.Length > 1)
        {
            _errors.Error(("settings", rows[1], "form_id"), "Only one settings row is supported.");
        }

        if (!XlsFormCells.IsName(row.Text("form_id")))
        {
            _errors.Error(("settings", row, "form_id"), "A form identifier beginning with a letter or underscore is required.");
        }

        if (string.IsNullOrWhiteSpace(row.Text("form_title")))
        {
            _errors.Error(("settings", row, "form_title"), "A form title is required.");
        }

        return row;
    }

    private void ReadChoices()
    {
        foreach (var row in workbook.Choices.Where(r => r.Cells.Values.Any(v => !string.IsNullOrWhiteSpace(v))))
        {
            _errors.CheckColumns(("choices", row), "list_name", "name", "label");
            var list = row.Text("list_name");
            if (!XlsFormCells.IsName(list) || string.IsNullOrWhiteSpace(row.Text("name")))
            {
                _errors.Error(("choices", row, "name"), "A valid list name and nonempty choice name are required.");
                continue;
            }

            if (!_choices.TryGetValue(list, out var choices))
            {
                choices = [];
                _choices.Add(list, choices);
            }

            if (choices.Any(c => c.Value == row.Text("name")))
            {
                _errors.Error(("choices", row, "name"), "Choice names must be unique within a list.");
            }

            choices.Add(new FieldChoice { Value = row.Text("name"), Label = row.Optional("label") ?? row.Text("name") });
        }
    }

    private void ReadSurveyRow(XlsFormRow row)
    {
        var type = row.Text("type").Trim().Replace(' ', '_');
        if (type is "begin_group" or "begin_repeat")
        {
            BeginGroup(row);
            return;
        }

        if (type is "end_group" or "end_repeat")
        {
            EndGroup(row, type);
            return;
        }

        _errors.CheckColumns(("survey", row), "type", "name", "label", "hint", "required", "relevant", "constraint", "constraint_message", "calculation");
        CheckName(row);
        _errors.CheckExpression(row, "relevant");
        _errors.CheckExpression(row, "constraint", type == "integer" ? IntegerConstraint(row) : null);
        _errors.CheckExpression(row, "calculation");
        var fieldType = ResolveType(row);
        var required = ReadRequired(row);
        CheckCalculation(row, type);

        _fields.Add(new FormField
        {
            FieldId = row.Text("name"), Label = row.Optional("label") ?? row.Text("name"), Type = fieldType,
            Required = required is "yes" or "true" or "1", HelpText = row.Optional("hint"),
            RelevanceExpression = row.Optional("relevant"), CalculatedExpression = row.Optional("calculation"),
            Choices = ReadFieldChoices(row), Validation = new FieldValidationRule
            {
                ConstraintExpression = type == "integer" ? IntegerConstraint(row) : row.Optional("constraint"),
                ConstraintMessage = row.Optional("constraint_message")
            }
        });
    }

    private string ReadRequired(XlsFormRow row)
    {
        var required = row.Text("required").Trim();
        if (required.Length > 0 && required is not ("yes" or "true" or "1" or "no" or "false" or "0"))
        {
            _errors.Error(("survey", row, "required"), "Only constant yes/no, true/false or 1/0 values are supported.");
        }

        return required;
    }

    private void CheckCalculation(XlsFormRow row, string type)
    {
        if (type != "calculate" && row.Optional("calculation") is not null)
        {
            _errors.Error(("survey", row, "calculation"), "Calculations are supported only on calculate fields.");
        }

        if (type == "calculate" && row.Optional("calculation") is null)
        {
            _errors.Error(("survey", row, "calculation"), "A calculate field requires an expression.");
        }
    }

    private void CheckReferences()
    {
        var scopes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var section in _sections)
        {
            IEnumerable<FormField> visible = section.Repeatable ? section.Fields
                : _sections.Where(s => !s.Repeatable).SelectMany(s => s.Fields);
            var names = visible.Select(f => f.FieldId).ToHashSet(StringComparer.Ordinal);
            foreach (var field in section.Fields)
            {
                scopes.TryAdd(field.FieldId, names);
            }
        }

        foreach (var row in workbook.Survey)
        {
            if (!scopes.TryGetValue(row.Text("name"), out var fields))
            {
                continue;
            }

            foreach (var column in new[] { "relevant", "constraint", "calculation" })
            {
                _errors.CheckReferences(row, (column, fields));
            }
        }
    }

    private void CheckCalculationOrder()
    {
        var outside = _sections.Where(s => !s.Repeatable).SelectMany(s => s.Fields)
            .Where(f => f.CalculatedExpression is null).Select(f => f.FieldId).ToHashSet(StringComparer.Ordinal);
        var rows = workbook.Survey.GroupBy(r => r.Text("name"), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var section in _sections)
        {
            var available = section.Repeatable
                ? section.Fields.Where(f => f.CalculatedExpression is null).Select(f => f.FieldId).ToHashSet(StringComparer.Ordinal)
                : outside;
            foreach (var field in section.Fields.Where(f => f.CalculatedExpression is not null))
            {
                _errors.CheckReferences(rows[field.FieldId], ("calculation", available));
                available.Add(field.FieldId);
            }
        }
    }

    private static string IntegerConstraint(XlsFormRow row)
        => row.Optional("constraint") is { } constraint ? $"floor(.) = . and ({constraint})" : "floor(.) = .";

    private void CheckName(XlsFormRow row)
    {
        var name = row.Text("name");
        if (!XlsFormCells.IsName(name) || !_names.Add(name))
        {
            _errors.Error(("survey", row, "name"), "Field and group names must be valid, globally unique identifiers.");
        }
    }

    private void BeginGroup(XlsFormRow row)
    {
        _errors.CheckColumns(("survey", row), "type", "name", "label", "hint");
        CheckName(row);
        if (_group is not null)
        {
            _errors.Error(("survey", row, "type"), "Nested groups and repeats cannot be represented by the portable form model.");
            return;
        }

        FlushUngrouped();
        _group = row;
    }

    private void EndGroup(XlsFormRow row, string type)
    {
        _errors.CheckColumns(("survey", row), "type");
        if (_group is null || _group.Text("type").Trim().Replace(' ', '_').Replace("begin_", "end_", StringComparison.Ordinal) != type)
        {
            _errors.Error(("survey", row, "type"), "Closing delimiter does not match the open group or repeat.");
            return;
        }

        _sections.Add(new FormSection
        {
            SectionId = _group.Text("name"), Label = _group.Optional("label") ?? _group.Text("name"),
            Description = _group.Optional("hint"), Repeatable = type == "end_repeat", Fields = _fields
        });
        _group = null;
        _fields = [];
    }

    private void FlushUngrouped()
    {
        if (_fields.Count == 0)
        {
            return;
        }

        var id = $"_xlsform_ungrouped_{++_ungrouped}";
        while (workbook.Survey.Any(r => string.Equals(r.Text("name"), id, StringComparison.OrdinalIgnoreCase)))
        {
            id = $"_xlsform_ungrouped_{++_ungrouped}";
        }

        _sections.Add(new FormSection { SectionId = id, Label = "Questions", Fields = _fields });
        _fields = [];
    }

    private FormFieldType ResolveType(XlsFormRow row)
    {
        var tokens = row.Text("type").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var type = tokens.Length == 0 ? string.Empty : tokens[0];
        var result = type switch
        {
            "text" => FormFieldType.Text, "integer" or "decimal" => FormFieldType.Numeric,
            "date" => FormFieldType.Date, "time" => FormFieldType.Time, "dateTime" => FormFieldType.DateTime,
            "select_one" => FormFieldType.SingleChoice, "select_multiple" => FormFieldType.MultipleChoice,
            "geopoint" => FormFieldType.Location, "geotrace" => FormFieldType.GeoTrace, "geoshape" => FormFieldType.GeoShape,
            "image" => FormFieldType.Photo, "audio" => FormFieldType.Audio, "video" => FormFieldType.Video,
            "barcode" => FormFieldType.Barcode, "calculate" => FormFieldType.Calculated,
            _ => (FormFieldType?)null
        };
        if (result is null)
        {
            _errors.Error(("survey", row, "type"), "Unsupported question type.");
        }

        return result ?? FormFieldType.Text;
    }

    private List<FieldChoice> ReadFieldChoices(XlsFormRow row)
    {
        var tokens = row.Text("type").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens[0] is not ("select_one" or "select_multiple"))
        {
            if (tokens.Length > 1)
            {
                _errors.Error(("survey", row, "type"), "Question type parameters are not supported.");
            }

            return [];
        }

        if (tokens.Length != 2 || !_choices.TryGetValue(tokens[1], out var choices) || choices.Count == 0)
        {
            _errors.Error(("survey", row, "type"), "Select questions require exactly one existing, nonempty choice list.");
            return [];
        }

        return choices;
    }
}
