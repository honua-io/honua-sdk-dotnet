// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Forms.Authoring;

internal sealed class XlsFormExport
{
    private readonly List<XlsFormDiagnostic> _errors = [];
    private readonly List<XlsFormRow> _survey = [];
    private readonly List<XlsFormRow> _choices = [];

    public XlsFormResult<XlsFormWorkbook> Convert(FormDefinition form)
    {
        foreach (var section in form.Sections)
        {
            _survey.Add(XlsFormCells.Row(_survey.Count + 2, ("type", section.Repeatable ? "begin_repeat" : "begin_group"),
                ("name", section.SectionId), ("label", section.Label), ("hint", section.Description)));
            if (section.Collapsible || section.InitiallyCollapsed)
            {
                _errors.Error(("survey", _survey[^1], "appearance"), "Section collapse behavior cannot be exported.");
            }

            foreach (var field in section.Fields)
            {
                AddField(field);
            }

            _survey.Add(XlsFormCells.Row(_survey.Count + 2, ("type", section.Repeatable ? "end_repeat" : "end_group")));
        }

        var workbook = new XlsFormWorkbook
        {
            Survey = _survey, Choices = _choices,
            Settings = [XlsFormCells.Row(2, ("form_id", form.FormId), ("form_title", form.Name), ("version", form.Version))]
        };
        _errors.AddRange(XlsFormConverter.Import(workbook).Diagnostics);
        if (form.Description is not null || form.Metadata.Count > 0)
        {
            _errors.Error(("settings", workbook.Settings[0], "form_title"), "Form description and metadata cannot be exported.");
        }

        return new XlsFormResult<XlsFormWorkbook> { Value = _errors.Count > 0 ? null : workbook, Diagnostics = _errors };
    }

    private void AddField(FormField field)
    {
        var type = TypeName(field.Type);
        var listName = $"choices_{_survey.Count}";
        if (field.Type is FormFieldType.SingleChoice or FormFieldType.MultipleChoice)
        {
            type += $" {listName}";
            foreach (var choice in field.Choices)
            {
                _choices.Add(XlsFormCells.Row(_choices.Count + 2, ("list_name", listName), ("name", choice.Value), ("label", choice.Label)));
            }
        }

        var row = XlsFormCells.Row(_survey.Count + 2, ("type", type), ("name", field.FieldId), ("label", field.Label),
            ("hint", field.HelpText), ("required", field.Required ? "yes" : null), ("relevant", field.RelevanceExpression),
            ("calculation", field.CalculatedExpression), ("constraint", field.Validation.ConstraintExpression),
            ("constraint_message", field.Validation.ConstraintMessage));
        _survey.Add(row);
        CheckPortableBehavior(field, row);
    }

    private void CheckPortableBehavior(FormField field, XlsFormRow row)
    {
        var validation = field.Validation;
        if (validation != new FieldValidationRule
            {
                ConstraintExpression = validation.ConstraintExpression,
                ConstraintMessage = validation.ConstraintMessage
            })
        {
            _errors.Error(("survey", row, "constraint"), "Additional portable validation rules cannot be exported.");
        }

        if (field.VisibilityRule is not null || field.MediaPolicy is not null || field.ChoiceSetId is not null
            || field.ReferencedFormId is not null || field.SourceFieldName is not null
            || field.Choices.Any(c => c.ParentValue is not null || c.Children.Count > 0))
        {
            _errors.Error(("survey", row, "type"), "Provider bindings, media policies, legacy visibility and hierarchical choices cannot be exported.");
        }

        if (field.Type is not (FormFieldType.SingleChoice or FormFieldType.MultipleChoice) && field.Choices.Count > 0)
        {
            _errors.Error(("survey", row, "type"), "Choices on this field type cannot be exported.");
        }
    }

    private static string TypeName(FormFieldType type) => type switch
    {
        FormFieldType.Text => "text", FormFieldType.Numeric => "decimal", FormFieldType.Date => "date",
        FormFieldType.Time => "time", FormFieldType.DateTime => "dateTime", FormFieldType.SingleChoice => "select_one",
        FormFieldType.MultipleChoice => "select_multiple", FormFieldType.Location => "geopoint",
        FormFieldType.GeoTrace => "geotrace", FormFieldType.GeoShape => "geoshape", FormFieldType.Photo => "image",
        FormFieldType.Audio => "audio", FormFieldType.Video => "video", FormFieldType.Barcode => "barcode",
        FormFieldType.Calculated => "calculate", _ => "unsupported"
    };
}
