using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Field.Forms;

namespace Honua.Sdk.Field.Tests;

public sealed class SourceSchemaFormMapperTests
{
    [Fact]
    public void CreateForm_MapsEditableSourceFieldsAndDomains()
    {
        var source = new SourceDescriptor
        {
            Id = "inspections",
            Protocol = FeatureProtocolIds.GeoServicesFeatureServer,
            Locator = new SourceLocator { ServiceId = "svc-inspections", LayerId = 2 },
            Schema = new SourceSchema
            {
                ObjectIdField = "OBJECTID",
                Fields =
                [
                    new SourceField { Name = "OBJECTID", Alias = "Object ID", Type = "esriFieldTypeOID", Editable = false },
                    new SourceField { Name = "asset_id", Alias = "Asset ID", Type = "esriFieldTypeString", Required = true, Length = 32 },
                    new SourceField { Name = "score", Alias = "Score", Type = "esriFieldTypeDouble", Nullable = false },
                    new SourceField
                    {
                        Name = "condition",
                        Alias = "Condition",
                        Type = "esriFieldTypeString",
                        Domain = JsonSerializer.SerializeToElement(new
                        {
                            type = "codedValue",
                            codedValues = new[]
                            {
                                new { code = "good", name = "Good" },
                                new { code = "needsRepair", name = "Needs repair" },
                            },
                        }),
                    },
                ],
            },
        };

        var form = FieldFormSchemaMapper.CreateForm(source, new FieldFormSchemaMapperOptions
        {
            FormId = "inspection-form",
            Name = "Inspection Form",
            Version = "2026.04",
        });

        Assert.Equal("inspection-form", form.FormId);
        Assert.Equal("svc-inspections", form.Target!.ServiceId);
        Assert.Equal(2, form.Target.LayerId);
        var fields = Assert.Single(form.Sections).Fields;
        Assert.DoesNotContain(fields, field => field.FieldId == "OBJECTID");
        Assert.Equal(FormFieldType.Text, fields.Single(field => field.FieldId == "asset_id").Type);
        Assert.Equal(32, fields.Single(field => field.FieldId == "asset_id").Validation.MaxLength);
        Assert.True(fields.Single(field => field.FieldId == "score").Required);

        var condition = fields.Single(field => field.FieldId == "condition");
        Assert.Equal(FormFieldType.SingleChoice, condition.Type);
        Assert.Equal(["good", "needsRepair"], condition.Choices.Select(choice => choice.Value));
    }
}
