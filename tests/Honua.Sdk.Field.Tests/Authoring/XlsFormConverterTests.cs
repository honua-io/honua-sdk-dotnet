using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Forms.Authoring;
using Honua.Sdk.Field.Forms.Expressions;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Tests.Authoring;

public sealed class XlsFormConverterTests
{
    [Fact]
    public void Import_PreservesFieldOrderAndRepeatBoundaries()
    {
        var result = XlsFormConverter.Import(Workbook(
            Row(2, ("type", "text"), ("name", "before")),
            Row(3, ("type", "begin_repeat"), ("name", "members"), ("label", "Members")),
            Row(4, ("type", "text"), ("name", "member")),
            Row(5, ("type", "end_repeat")),
            Row(6, ("type", "geopoint"), ("name", "after"))));

        Assert.Empty(result.Diagnostics);
        var form = Assert.IsType<FormDefinition>(result.Value);
        Assert.Equal(["before", "member", "after"], form.Sections.SelectMany(s => s.Fields).Select(f => f.FieldId));
        Assert.True(form.Sections[1].Repeatable);
        Assert.False(form.Sections[0].Repeatable);
        Assert.False(form.Sections[2].Repeatable);
    }

    [Fact]
    public void Import_PreservesValidationAndExpressionBehavior()
    {
        var result = XlsFormConverter.Import(Workbook(
            Row(2, ("type", "integer"), ("name", "age"), ("required", "yes"),
                ("constraint", ". >= 18"), ("constraint_message", "Adults only")),
            Row(3, ("type", "text"), ("name", "details"), ("relevant", "${age} >= 18")),
            Row(4, ("type", "calculate"), ("name", "next_age"), ("calculation", "${age} + 1"))));

        var fields = Assert.IsType<FormDefinition>(result.Value).Sections[0].Fields;
        Assert.True(fields[0].Required);
        Assert.Equal("Adults only", fields[0].Validation.ConstraintMessage);
        Assert.False(ExpressionEvaluator.EvaluateBoolean(fields[0].Validation.ConstraintExpression, new Dictionary<string, object?> { ["."] = 18.5 }));
        Assert.True(ExpressionEvaluator.EvaluateBoolean(fields[0].Validation.ConstraintExpression, new Dictionary<string, object?> { ["."] = 18 }));
        Assert.Equal("${age} >= 18", fields[1].RelevanceExpression);
        Assert.Equal("${age} + 1", fields[2].CalculatedExpression);
    }

    [Theory]
    [InlineData("type", "unsupported")]
    [InlineData("required", "${other} = 1")]
    [InlineData("relevant", "if(true, true, unsupported())")]
    [InlineData("relevant", "selected(${answer})")]
    [InlineData("relevant", "selected(${answer}, 'x', 'y')")]
    [InlineData("relevant", "true(1)")]
    [InlineData("relevant", "div = 1")]
    [InlineData("relevant", "1 mod mod")]
    [InlineData("constraint", ". >=")]
    [InlineData("relevant", "${missing} = 1")]
    [InlineData("relevant", ". = 1")]
    [InlineData("appearance", "hidden")]
    public void Import_UnsupportedBehaviorRejectsWholeFormWithCellLocation(string column, string value)
    {
        var cells = new Dictionary<string, string> { ["type"] = "text", ["name"] = "answer", [column] = value };
        var result = XlsFormConverter.Import(Workbook(new XlsFormRow { RowNumber = 7, Cells = cells }));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Sheet == "survey" && d.RowNumber == 7 && d.Column == column);
    }

    [Theory]
    [InlineData("relevant")]
    [InlineData("constraint")]
    [InlineData("calculation")]
    public void Import_RejectsExpressionsWhoseEvaluationTreeExceedsRuntimeDepth(string column)
    {
        var result = XlsFormConverter.Import(Workbook(Row(2,
            ("type", column == "calculation" ? "calculate" : "text"), ("name", "answer"),
            (column, string.Join(" and ", Enumerable.Repeat("true", 70))))));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 2 && d.Column == column && d.Message.Contains("deep", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_ValidatesGeneratedIntegerConstraintLength()
    {
        var result = XlsFormConverter.Import(Workbook(Row(2, ("type", "integer"), ("name", "answer"),
            ("constraint", "true" + new string(' ', 7996)))));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 2 && d.Column == "constraint" && d.Message.Contains("length", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_RejectsProviderBindingsInsteadOfDroppingThem()
    {
        var form = Assert.IsType<FormDefinition>(XlsFormConverter.Import(Workbook(
            Row(2, ("type", "text"), ("name", "answer")))).Value);
        var field = form.Sections[0].Fields[0] with { SourceFieldName = "provider_column" };
        form = form with { Sections = [form.Sections[0] with { Fields = [field] }] };
        var result = XlsFormConverter.Export(form);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Sheet == "survey" && d.Column == "type");
    }

    [Theory]
    [InlineData("begin_group", "end_repeat")]
    [InlineData("begin_repeat", "end_group")]
    public void Import_RejectsMismatchedGroupDelimiters(string start, string end)
    {
        var result = XlsFormConverter.Import(Workbook(
            Row(2, ("type", start), ("name", "group")), Row(3, ("type", end))));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 3);
    }

    [Fact]
    public void Import_RejectsNestedAndUnclosedGroups()
    {
        var result = XlsFormConverter.Import(Workbook(
            Row(2, ("type", "begin_group"), ("name", "outer")),
            Row(3, ("type", "begin_group"), ("name", "inner"))));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 3);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 2);
    }

    [Theory]
    [InlineData("outside", "${member} = 1", 2)]
    [InlineData("member", "${outside} = 1", 4)]
    public void Import_RejectsReferencesAcrossRepeatBoundaries(string name, string expression, int rowNumber)
    {
        var outside = Row(2, ("type", "integer"), ("name", "outside"));
        var member = Row(4, ("type", "integer"), ("name", "member"));
        var target = name == "outside" ? outside : member;
        var cells = target.Cells.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
        cells["relevant"] = expression;
        target = target with { Cells = cells };
        var result = XlsFormConverter.Import(Workbook(
            name == "outside" ? target : outside,
            Row(3, ("type", "begin_repeat"), ("name", "members")),
            name == "member" ? target : member,
            Row(5, ("type", "end_repeat"))));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == rowNumber && d.Column == "relevant");
    }

    [Fact]
    public void Import_MissingChoiceListReportsSurveyCell()
    {
        var result = XlsFormConverter.Import(Workbook(Row(2, ("type", "select_one missing"), ("name", "choice"))));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 2 && d.Column == "type");
    }

    [Fact]
    public void ImportExportImport_PreservesSupportedSemantics()
    {
        var workbook = Workbook(
            Row(2, ("type", "select_one colors"), ("name", "color"), ("required", "yes")),
            Row(3, ("type", "begin_repeat"), ("name", "members")),
            Row(4, ("type", "select_multiple colors"), ("name", "likes")),
            Row(5, ("type", "end_repeat")));
        workbook = workbook with { Choices = [Row(2, ("list_name", "colors"), ("name", "red"), ("label", "Red"))] };
        var first = Assert.IsType<FormDefinition>(XlsFormConverter.Import(workbook).Value);
        var exported = Assert.IsType<XlsFormWorkbook>(XlsFormConverter.Export(first).Value);
        var second = Assert.IsType<FormDefinition>(XlsFormConverter.Import(exported).Value);
        Assert.Equal(first.FormId, second.FormId);
        Assert.Equal(first.Name, second.Name);
        Assert.Equal(first.Version, second.Version);
        Assert.Equal(first.Sections.Select(s => s.Repeatable), second.Sections.Select(s => s.Repeatable));
        Assert.Equal(first.Sections.SelectMany(s => s.Fields), second.Sections.SelectMany(s => s.Fields), new FieldComparer());
    }

    [Fact]
    public void ImportExportImport_SelectedControlsRequiredQuestion()
    {
        var workbook = Workbook(
            Row(2, ("type", "select_multiple risks"), ("name", "hazards")),
            Row(3, ("type", "text"), ("name", "details"), ("required", "yes"),
                ("relevant", "selected(${hazards}, 'fire')"))) with
        {
            Choices = [
                Row(2, ("list_name", "risks"), ("name", "fire"), ("label", "Fire")),
                Row(3, ("list_name", "risks"), ("name", "wildfire"), ("label", "Wildfire")),
            ]
        };
        var imported = Assert.IsType<FormDefinition>(XlsFormConverter.Import(workbook).Value);
        var exported = Assert.IsType<XlsFormWorkbook>(XlsFormConverter.Export(imported).Value);
        var reimported = Assert.IsType<FormDefinition>(XlsFormConverter.Import(exported).Value);
        foreach (var form in new[] { imported, reimported })
        {
            var record = new FieldRecord { RecordId = "inspection", FormId = form.FormId };
            record.Values["hazards"] = new[] { "wildfire" };
            Assert.True(FormValidator.Validate(form, record).IsValid);
            record.Values["hazards"] = new[] { "fire", "wildfire" };
            Assert.False(FormValidator.Validate(form, record).IsValid);
            record.Values["details"] = "Observed";
            Assert.True(FormValidator.Validate(form, record).IsValid);
        }
    }

    [Fact]
    public void ImportExportImport_PreservesXlsFormOperatorsAndQuotedText()
    {
        const string rule = "if(${div} div 3 mod 2 = 1 and ${crew} = \"O'Brien mod true()\", true(), false())";
        var workbook = Workbook(
            Row(2, ("type", "decimal"), ("name", "div")),
            Row(3, ("type", "text"), ("name", "crew")),
            Row(4, ("type", "text"), ("name", "details"), ("required", "yes"), ("relevant", rule)));
        var imported = Assert.IsType<FormDefinition>(XlsFormConverter.Import(workbook).Value);
        var exported = Assert.IsType<XlsFormWorkbook>(XlsFormConverter.Export(imported).Value);
        Assert.Equal(rule, exported.Survey.Single(row => row.Cells.GetValueOrDefault("name") == "details").Cells["relevant"]);
        var reimported = Assert.IsType<FormDefinition>(XlsFormConverter.Import(exported).Value);

        foreach (var form in new[] { imported, reimported })
        {
            var record = new FieldRecord { RecordId = "inspection", FormId = form.FormId };
            record.Values["div"] = 9;
            record.Values["crew"] = "O'Brien mod true()";
            Assert.False(FormValidator.Validate(form, record).IsValid);
            record.Values["div"] = 6;
            Assert.True(FormValidator.Validate(form, record).IsValid);
        }
    }

    [Fact]
    public void WorkbookRoundTrip_PreservesSubmissionValidation()
    {
        var source = Workbook(
            Row(2, ("type", "integer"), ("name", "age"), ("label", "Age"), ("required", "yes"),
                ("constraint", ". >= 18"), ("constraint_message", "Adults only")),
            Row(3, ("type", "text"), ("name", "details"), ("hint", "Describe the inspection"),
                ("required", "yes"), ("relevant", "${age} >= 21")),
            Row(4, ("type", "calculate"), ("name", "next_age"), ("calculation", "${age} + 1")));
        using var stream = new MemoryStream();
        XlsFormWorkbookIO.Write(stream, source);
        stream.Position = 0;
        var read = Assert.IsType<XlsFormWorkbook>(XlsFormWorkbookIO.Read(stream).Value);
        var first = Assert.IsType<FormDefinition>(XlsFormConverter.Import(read).Value);
        var exported = Assert.IsType<XlsFormWorkbook>(XlsFormConverter.Export(first).Value);
        var second = Assert.IsType<FormDefinition>(XlsFormConverter.Import(exported).Value);

        foreach (var form in new[] { first, second })
        {
            var record = new FieldRecord { RecordId = "inspection-1", FormId = form.FormId };
            Assert.False(FormValidator.Validate(form, record).IsValid);
            record.Values["age"] = 18.5;
            Assert.False(FormValidator.Validate(form, record).IsValid);
            record.Values["age"] = 18;
            Assert.True(FormValidator.Validate(form, record).IsValid);
            record.Values["age"] = 21;
            Assert.False(FormValidator.Validate(form, record).IsValid);
            record.Values["details"] = "Inspected";
            Assert.True(FormValidator.Validate(form, record).IsValid);
        }
    }

    [Theory]
    [InlineData("first", "${second} + 1")]
    [InlineData("first", "${first} + 1")]
    public void Import_RejectsCalculatedDependenciesBeforeEvaluation(string name, string expression)
    {
        var result = XlsFormConverter.Import(Workbook(
            Row(2, ("type", "calculate"), ("name", name), ("calculation", expression)),
            Row(3, ("type", "calculate"), ("name", "second"), ("calculation", "1"))));
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 2 && d.Column == "calculation");
    }

    [Fact]
    public void Import_CalculatedDependenciesEvaluateInOrder()
    {
        var form = Assert.IsType<FormDefinition>(XlsFormConverter.Import(Workbook(
            Row(2, ("type", "calculate"), ("name", "first"), ("calculation", "1")),
            Row(3, ("type", "calculate"), ("name", "second"), ("calculation", "${first} + 1")))).Value);
        var record = new FieldRecord { RecordId = "calculated", FormId = form.FormId };
        CalculatedFieldEvaluator.ApplyCalculatedFields(form, record);
        Assert.Equal(2d, record.Values["second"]);
    }

    private sealed class FieldComparer : IEqualityComparer<FormField>
    {
        public bool Equals(FormField? x, FormField? y) => x?.FieldId == y?.FieldId && x?.Type == y?.Type
            && x?.Required == y?.Required && x?.Label == y?.Label && x?.HelpText == y?.HelpText
            && x?.Validation == y?.Validation && x?.RelevanceExpression == y?.RelevanceExpression
            && x?.CalculatedExpression == y?.CalculatedExpression
            && x?.Choices.SequenceEqual(y?.Choices ?? []) == true;
        public int GetHashCode(FormField obj) => StringComparer.Ordinal.GetHashCode(obj.FieldId);
    }

    private static XlsFormWorkbook Workbook(params XlsFormRow[] survey) => new()
    {
        Survey = survey,
        Settings = [Row(2, ("form_id", "survey"), ("form_title", "Survey"), ("version", "1"))]
    };

    private static XlsFormRow Row(int number, params (string Key, string Value)[] cells) => new()
    {
        RowNumber = number, Cells = cells.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal)
    };
}
