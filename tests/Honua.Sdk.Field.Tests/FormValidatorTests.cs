using System.Globalization;
using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Tests;

public sealed class FormValidatorTests
{
    [Fact]
    public void Validate_ReturnsErrors_WhenRequiredOrTypeRulesFail()
    {
        var form = new FormDefinition
        {
            FormId = "inspection",
            Name = "Inspection",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField { FieldId = "asset_id", Label = "Asset ID", Type = FormFieldType.Text, Required = true },
                        new FormField
                        {
                            FieldId = "score",
                            Label = "Score",
                            Type = FormFieldType.Numeric,
                            Validation = new FieldValidationRule { MinNumericValue = 1, MaxNumericValue = 5 },
                        },
                        new FormField
                        {
                            FieldId = "site_url",
                            Label = "Site URL",
                            Type = FormFieldType.Hyperlink,
                            Required = true,
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r1",
            FormId = "inspection",
            Values =
            {
                ["score"] = 7,
                ["site_url"] = "not-a-url",
            },
        };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldId == "asset_id");
        Assert.Contains(result.Errors, error => error.FieldId == "score");
        Assert.Contains(result.Errors, error => error.FieldId == "site_url");
    }

    [Fact]
    public void CalculatedFieldEvaluator_ComputesConcatAndSum()
    {
        var form = new FormDefinition
        {
            FormId = "survey",
            Name = "Survey",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField { FieldId = "first", Label = "First", Type = FormFieldType.Text },
                        new FormField { FieldId = "second", Label = "Second", Type = FormFieldType.Text },
                        new FormField { FieldId = "a", Label = "A", Type = FormFieldType.Numeric },
                        new FormField { FieldId = "b", Label = "B", Type = FormFieldType.Numeric },
                        new FormField { FieldId = "display", Label = "Display", Type = FormFieldType.Calculated, CalculatedExpression = "concat($first,'-', $second)" },
                        new FormField { FieldId = "total", Label = "Total", Type = FormFieldType.Calculated, CalculatedExpression = "sum($a,$b)" },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r2",
            FormId = "survey",
            Values =
            {
                ["first"] = "alpha",
                ["second"] = "beta",
                ["a"] = 3,
                ["b"] = 5,
            },
        };

        CalculatedFieldEvaluator.ApplyCalculatedFields(form, record);

        Assert.Equal("alpha-beta", record.Values["display"]);
        Assert.Equal(8d, record.Values["total"]);
    }

    [Fact]
    public void Validate_RequiredMultipleChoiceRejectsEmptyCollection()
    {
        var form = new FormDefinition
        {
            FormId = "inspection",
            Name = "Inspection",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "tags",
                            Label = "Tags",
                            Type = FormFieldType.MultipleChoice,
                            Required = true,
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r-empty-tags",
            FormId = "inspection",
            Values =
            {
                ["tags"] = Array.Empty<string>(),
            },
        };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldId == "tags");
    }

    [Fact]
    public void Validate_WhenVisibilityRuleComparesNullToZero_FieldRemainsHidden()
    {
        var form = new FormDefinition
        {
            FormId = "inspection",
            Name = "Inspection",
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
                            VisibilityRule = new FieldVisibilityRule
                            {
                                DependsOnFieldId = "score",
                                Operator = ComparisonOperator.Equals,
                                MatchValue = 0,
                            },
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r-null-score",
            FormId = "inspection",
            Values =
            {
                ["score"] = null,
            },
        };

        var result = FormValidator.Validate(form, record);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, error => error.FieldId == "followup");
    }

    [Fact]
    public void Validate_WithInvalidRegexPattern_DoesNotThrow()
    {
        var form = new FormDefinition
        {
            FormId = "inspection",
            Name = "Inspection",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "code",
                            Label = "Code",
                            Type = FormFieldType.Text,
                            Validation = new FieldValidationRule
                            {
                                RegexPattern = "(",
                            },
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r-invalid-regex",
            FormId = "inspection",
            Values =
            {
                ["code"] = "ABC",
            },
        };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldId == "code");
    }

    [Fact]
    public async Task Validate_WithCatastrophicRegexInput_CompletesQuickly()
    {
        var form = new FormDefinition
        {
            FormId = "inspection",
            Name = "Inspection",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "code",
                            Label = "Code",
                            Type = FormFieldType.Text,
                            Validation = new FieldValidationRule
                            {
                                RegexPattern = "^(a+)+$",
                            },
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r-redos",
            FormId = "inspection",
            Values =
            {
                ["code"] = new string('a', 4000) + "!",
            },
        };

        var validationTask = Task.Run(() => FormValidator.Validate(form, record));
        var result = await validationTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldId == "code");
    }

    [Fact]
    public void Validate_MediaCountUsesPortableAttachmentMetadata()
    {
        var form = new FormDefinition
        {
            FormId = "inspection",
            Name = "Inspection",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "photos",
                            Label = "Photos",
                            Type = FormFieldType.Photo,
                            Required = true,
                            Validation = new FieldValidationRule { MinMediaCount = 2 },
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r-photo",
            FormId = "inspection",
            Media =
            [
                new FieldMediaAttachment { AttachmentId = "a1", FieldId = "photos", MediaType = FieldMediaType.Photo, FileName = "asset.jpg" },
            ],
        };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldId == "photos");
    }

    [Fact]
    public void Validate_MediaFieldRejectsTooManyAttachments()
    {
        var form = new FormDefinition
        {
            FormId = "inspection",
            Name = "Inspection",
            Sections =
            [
                new FormSection
                {
                    SectionId = "main",
                    Label = "Main",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "photos",
                            Label = "Photos",
                            Type = FormFieldType.Photo,
                            Validation = new FieldValidationRule { MinMediaCount = 1, MaxMediaCount = 2 },
                            MediaPolicy = new FieldMediaCapturePolicy
                            {
                                AllowedContentTypes = ["image/jpeg"],
                                MaxAttachmentBytes = 5_000_000,
                                RequiresFaceBlur = true,
                                CaptureLocation = true,
                            },
                        },
                    ],
                },
            ],
        };

        var record = new FieldRecord
        {
            RecordId = "r-media",
            FormId = "inspection",
            Media =
            [
                new FieldMediaAttachment { AttachmentId = "p1", FieldId = "photos", MediaType = FieldMediaType.Photo },
                new FieldMediaAttachment { AttachmentId = "p2", FieldId = "photos", MediaType = FieldMediaType.Photo },
                new FieldMediaAttachment { AttachmentId = "p3", FieldId = "photos", MediaType = FieldMediaType.Photo },
            ],
        };

        var result = FormValidator.Validate(form, record);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldId == "photos" && error.Message.Contains("at most 2", StringComparison.Ordinal));
    }

    [Fact]
    public void FieldRecord_PortableAdvancedValues_CanBeStoredInRecord()
    {
        var scannedAt = DateTimeOffset.Parse("2026-05-23T08:00:00Z", CultureInfo.InvariantCulture);
        var record = new FieldRecord
        {
            RecordId = "r-advanced",
            FormId = "incident",
            Values =
            {
                ["address"] = new FieldAddressValue
                {
                    Formatted = "100 Main St, Honolulu, HI 96813",
                    Locality = "Honolulu",
                    Region = "HI",
                    PostalCode = "96813",
                    Location = new FieldGeoPoint(21.3069, -157.8583, 3),
                },
                ["asset_link"] = new FieldRecordLinkValue
                {
                    FormId = "asset-inspection",
                    SourceId = "assets",
                    RecordId = "asset-100",
                    Label = "Asset 100",
                },
                ["barcode"] = new FieldBarcodeValue
                {
                    Value = "ASSET-100",
                    Format = "QR_CODE",
                    ScannedAtUtc = scannedAt,
                },
            },
            Media =
            [
                new FieldMediaAttachment
                {
                    AttachmentId = "video-1",
                    FieldId = "walkthrough",
                    MediaType = FieldMediaType.Video,
                    Duration = TimeSpan.FromSeconds(15),
                    GpsTrack = new FieldGpsTrackReference
                    {
                        TrackId = "track-1",
                        FileName = "track.geojson",
                        ContentType = "application/geo+json",
                        PointCount = 12,
                    },
                    Sha256 = "0123456789abcdef",
                },
            ],
        };

        Assert.IsType<FieldAddressValue>(record.Values["address"]);
        Assert.IsType<FieldRecordLinkValue>(record.Values["asset_link"]);
        Assert.Equal(scannedAt, Assert.IsType<FieldBarcodeValue>(record.Values["barcode"]).ScannedAtUtc);
        Assert.Equal("track-1", record.Media[0].GpsTrack?.TrackId);
    }
}
