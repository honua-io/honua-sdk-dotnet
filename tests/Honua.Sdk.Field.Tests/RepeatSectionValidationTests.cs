using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Tests;

public sealed class RepeatSectionValidationTests
{
    private static FormDefinition Form() => new()
    {
        FormId = "pole",
        Name = "Pole inspection",
        Sections =
        [
            new FormSection
            {
                SectionId = "header",
                Label = "Header",
                Fields = [new FormField { FieldId = "poleId", Label = "Pole ID", Type = FormFieldType.Text, Required = true }],
            },
            new FormSection
            {
                SectionId = "attachments",
                Label = "Attachments",
                Repeatable = true,
                Fields =
                [
                    new FormField { FieldId = "kind", Label = "Kind", Type = FormFieldType.Text, Required = true },
                    new FormField { FieldId = "height", Label = "Height", Type = FormFieldType.Numeric },
                ],
            },
        ],
    };

    private static FieldRepeatInstance Row(params (string Key, object? Value)[] values)
    {
        var instance = new FieldRepeatInstance();
        foreach (var (key, value) in values)
        {
            instance.Values[key] = value;
        }

        return instance;
    }

    [Fact]
    public void Validate_ReportsRowErrors_WithIndexedFieldIds()
    {
        var record = new FieldRecord { RecordId = "r1", FormId = "pole" };
        record.Values["poleId"] = "P-7";
        record.Repeats["attachments"] =
        [
            Row(("kind", "transformer"), ("height", 9)),
            Row(("height", "not-a-number")), // kind missing (required) + height non-numeric
        ];

        var result = FormValidator.Validate(Form(), record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldId == "attachments[1].kind" && e.Message.Contains("required"));
        Assert.Contains(result.Errors, e => e.FieldId == "attachments[1].height" && e.Message.Contains("numeric"));
        Assert.DoesNotContain(result.Errors, e => e.FieldId.StartsWith("attachments[0]", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NoRows_RaisesNoRepeatErrors()
    {
        var record = new FieldRecord { RecordId = "r1", FormId = "pole" };
        record.Values["poleId"] = "P-7";

        var result = FormValidator.Validate(Form(), record);

        Assert.True(result.IsValid); // zero repeat rows is allowed; scalar fields satisfied
    }

    [Fact]
    public void Validate_RepeatFieldsAreNotValidatedAtTopLevel()
    {
        // 'kind' is required but lives in the repeatable section. With no rows it
        // must not surface as a top-level required error.
        var record = new FieldRecord { RecordId = "r1", FormId = "pole" };
        record.Values["poleId"] = "P-7";

        var result = FormValidator.Validate(Form(), record);

        Assert.DoesNotContain(result.Errors, e => e.FieldId == "kind");
    }

    [Fact]
    public void CalculatedFields_EvaluatePerRow_AgainstRowValues()
    {
        var form = new FormDefinition
        {
            FormId = "f",
            Name = "f",
            Sections =
            [
                new FormSection
                {
                    SectionId = "people",
                    Label = "People",
                    Repeatable = true,
                    Fields =
                    [
                        new FormField { FieldId = "first", Label = "First", Type = FormFieldType.Text },
                        new FormField { FieldId = "last", Label = "Last", Type = FormFieldType.Text },
                        new FormField { FieldId = "full", Label = "Full", Type = FormFieldType.Calculated, CalculatedExpression = "concat($first, ' ', $last)" },
                    ],
                },
            ],
        };

        var record = new FieldRecord { RecordId = "r1", FormId = "f" };
        record.Repeats["people"] =
        [
            Row(("first", "Ada"), ("last", "Lovelace")),
            Row(("first", "Alan"), ("last", "Turing")),
        ];

        CalculatedFieldEvaluator.ApplyCalculatedFields(form, record);

        Assert.Equal("Ada Lovelace", record.Repeats["people"][0].Values["full"]);
        Assert.Equal("Alan Turing", record.Repeats["people"][1].Values["full"]);
    }

    [Fact]
    public void Visibility_WithinARow_UsesRowValues()
    {
        var form = new FormDefinition
        {
            FormId = "f",
            Name = "f",
            Sections =
            [
                new FormSection
                {
                    SectionId = "items",
                    Label = "Items",
                    Repeatable = true,
                    Fields =
                    [
                        new FormField { FieldId = "hasDetail", Label = "Has detail?", Type = FormFieldType.YesNo },
                        new FormField
                        {
                            FieldId = "detail",
                            Label = "Detail",
                            Type = FormFieldType.Text,
                            Required = true,
                            VisibilityRule = new FieldVisibilityRule
                            {
                                DependsOnFieldId = "hasDetail",
                                Operator = ComparisonOperator.Equals,
                                MatchValue = true,
                            },
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord { RecordId = "r1", FormId = "f" };
        record.Repeats["items"] =
        [
            Row(("hasDetail", false)),               // detail hidden -> not required
            Row(("hasDetail", true)),                // detail shown but missing -> required error
        ];

        var result = FormValidator.Validate(form, record);

        Assert.Contains(result.Errors, e => e.FieldId == "items[1].detail");
        Assert.DoesNotContain(result.Errors, e => e.FieldId == "items[0].detail");
    }
}
