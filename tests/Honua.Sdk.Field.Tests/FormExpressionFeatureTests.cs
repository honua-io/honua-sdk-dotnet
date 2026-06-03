using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Tests;

public sealed class FormExpressionFeatureTests
{
    private static FormDefinition SingleFieldForm(FormField field) => new()
    {
        FormId = "f",
        Name = "f",
        Sections = [new FormSection { SectionId = "main", Label = "Main", Fields = [field] }],
    };

    // ---- constraint expressions ----

    [Fact]
    public void Constraint_PassesWhenSatisfied()
    {
        var form = SingleFieldForm(new FormField
        {
            FieldId = "score",
            Label = "Score",
            Type = FormFieldType.Numeric,
            Validation = new FieldValidationRule { ConstraintExpression = ". >= 0 and . <= 100" },
        });

        var record = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["score"] = 50 } };

        var result = FormValidator.Validate(form, record);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Constraint_FailsWhenViolated_WithDefaultMessage()
    {
        var form = SingleFieldForm(new FormField
        {
            FieldId = "score",
            Label = "Score",
            Type = FormFieldType.Numeric,
            Validation = new FieldValidationRule { ConstraintExpression = ". >= 0 and . <= 100" },
        });

        var record = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["score"] = 150 } };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldId == "score" && e.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Constraint_FailsWithCustomMessage()
    {
        var form = SingleFieldForm(new FormField
        {
            FieldId = "score",
            Label = "Score",
            Type = FormFieldType.Numeric,
            Validation = new FieldValidationRule
            {
                ConstraintExpression = ". <= 100",
                ConstraintMessage = "Score cannot exceed 100.",
            },
        });

        var record = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["score"] = 150 } };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.FieldId == "score" && e.Message == "Score cannot exceed 100.");
    }

    [Fact]
    public void Constraint_CanReferenceOtherFields()
    {
        var form = new FormDefinition
        {
            FormId = "f",
            Name = "f",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField { FieldId = "min", Label = "Min", Type = FormFieldType.Numeric },
                        new FormField
                        {
                            FieldId = "max",
                            Label = "Max",
                            Type = FormFieldType.Numeric,
                            Validation = new FieldValidationRule { ConstraintExpression = ". >= $min", ConstraintMessage = "Max must be >= Min." },
                        },
                    ],
                },
            ],
        };

        var bad = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["min"] = 10, ["max"] = 5 } };
        var good = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["min"] = 10, ["max"] = 15 } };

        Assert.False(FormValidator.Validate(form, bad).IsValid);
        Assert.True(FormValidator.Validate(form, good).IsValid);
    }

    [Fact]
    public void Constraint_DoesNotMutateRecordValues()
    {
        var form = SingleFieldForm(new FormField
        {
            FieldId = "score",
            Label = "Score",
            Type = FormFieldType.Numeric,
            Validation = new FieldValidationRule { ConstraintExpression = ". <= 100" },
        });

        var record = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["score"] = 50 } };

        FormValidator.Validate(form, record);

        Assert.False(record.Values.ContainsKey("."));
    }

    // ---- boolean relevance ----

    [Fact]
    public void Relevance_CompoundExpression_ShowsAndRequires()
    {
        var form = new FormDefinition
        {
            FormId = "f",
            Name = "f",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField { FieldId = "type", Label = "Type", Type = FormFieldType.Text },
                        new FormField { FieldId = "score", Label = "Score", Type = FormFieldType.Numeric },
                        new FormField
                        {
                            FieldId = "followup",
                            Label = "Follow-up",
                            Type = FormFieldType.Text,
                            Required = true,
                            RelevanceExpression = "${type}='incident' and ${score}>5",
                        },
                    ],
                },
            ],
        };

        // Both conditions true -> field is relevant and required -> missing -> error.
        var visible = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["type"] = "incident", ["score"] = 8 } };
        Assert.Contains(FormValidator.Validate(form, visible).Errors, e => e.FieldId == "followup");

        // One condition false -> field hidden -> no required error.
        var hidden = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["type"] = "incident", ["score"] = 2 } };
        Assert.DoesNotContain(FormValidator.Validate(form, hidden).Errors, e => e.FieldId == "followup");
    }

    [Fact]
    public void Relevance_SupersedesVisibilityRule()
    {
        // RelevanceExpression is always false; VisibilityRule would otherwise show the field.
        var form = new FormDefinition
        {
            FormId = "f",
            Name = "f",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField { FieldId = "score", Label = "Score", Type = FormFieldType.Numeric },
                        new FormField
                        {
                            FieldId = "followup",
                            Label = "Follow-up",
                            Type = FormFieldType.Text,
                            Required = true,
                            RelevanceExpression = "false",
                            VisibilityRule = new FieldVisibilityRule
                            {
                                DependsOnFieldId = "score",
                                Operator = ComparisonOperator.GreaterThan,
                                MatchValue = 0,
                            },
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["score"] = 100 } };

        Assert.DoesNotContain(FormValidator.Validate(form, record).Errors, e => e.FieldId == "followup");
    }

    // ---- geometry field types ----

    [Fact]
    public void GeometryFieldTypes_Exist_AndAreAppendedAfterLocation()
    {
        Assert.True((int)FormFieldType.GeoShape > (int)FormFieldType.Location);
        Assert.True((int)FormFieldType.GeoTrace > (int)FormFieldType.GeoShape);
    }

    [Fact]
    public void GeometryField_DoesNotBreakValidation()
    {
        var form = SingleFieldForm(new FormField { FieldId = "area", Label = "Area", Type = FormFieldType.GeoShape });
        var record = new FieldRecord { RecordId = "r", FormId = "f", Values = { ["area"] = "POLYGON((0 0,1 0,1 1,0 0))" } };

        Assert.True(FormValidator.Validate(form, record).IsValid);
    }
}
