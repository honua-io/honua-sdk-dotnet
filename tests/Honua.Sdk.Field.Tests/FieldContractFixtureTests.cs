using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Tests;

public sealed class FieldContractFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    [Fact]
    public void FormFixture_DeserializesAndValidatesVisibilityRules()
    {
        var form = JsonSerializer.Deserialize<FormDefinition>(ReadFixture("field-form-contract.json"), JsonOptions)
            ?? throw new InvalidOperationException("Field form fixture was empty.");

        Assert.Equal("inspection", form.FormId);
        var main = Assert.Single(form.Sections);
        Assert.Equal(4, main.Fields.Count);
        Assert.Equal(FormFieldType.SingleChoice, main.Fields[1].Type);

        var record = new FieldRecord
        {
            RecordId = "r-fixture",
            FormId = form.FormId,
            Values =
            {
                ["asset_id"] = "A-100",
                ["condition"] = "needsRepair",
                ["repair_notes"] = string.Empty,
            },
            Media =
            [
                new FieldMediaAttachment { AttachmentId = "photo-1", FieldId = "photos", MediaType = FieldMediaType.Photo, FileName = "asset.jpg" },
            ],
        };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldId == "repair_notes");
        Assert.Contains(result.Errors, error => error.FieldId == "photos");
    }

    [Fact]
    public void FormFixture_RoundTripsWithStableCamelCaseShape()
    {
        var form = JsonSerializer.Deserialize<FormDefinition>(ReadFixture("field-form-contract.json"), JsonOptions)
            ?? throw new InvalidOperationException("Field form fixture was empty.");

        var json = JsonSerializer.Serialize(form, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("formId", out _));
        Assert.True(root.TryGetProperty("sections", out var sections));
        Assert.True(sections[0].GetProperty("fields")[0].TryGetProperty("fieldId", out _));
    }

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Json", name));
}
