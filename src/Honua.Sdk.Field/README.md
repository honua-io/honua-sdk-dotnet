# Honua.Sdk.Field

Contracts and pure-.NET helpers for field-data-capture apps built on the Honua
SDK: form definitions and sections, field types and validation rules, calculated
expressions, duplicate detection, and a record-state-machine workflow. No server
client — consume these models from your app and submit captured `FieldRecord`s
through the SDK's feature-edit clients.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.Field
```

## Quick usage

```csharp
using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;

var form = new FormDefinition
{
    FormId = "inspection",
    Name = "Pole inspection",
    Sections =
    [
        new FormSection
        {
            SectionId = "main",
            Label = "Asset",
            Fields =
            [
                new FormField
                {
                    FieldId = "asset_id",
                    Label = "Asset ID",
                    Type = FormFieldType.Text,
                    Required = true,
                },
                new FormField
                {
                    FieldId = "height_m",
                    Label = "Height (m)",
                    Type = FormFieldType.Numeric,
                    Validation = new FieldValidationRule { MinNumericValue = 0 },
                },
            ],
        },
    ],
};

var record = new FieldRecord
{
    RecordId = Guid.NewGuid().ToString(),
    FormId = form.FormId,
    Values = { ["asset_id"] = "P-1024", ["height_m"] = 12.5 },
};

var result = FormValidator.Validate(form, record);
if (result.IsValid)
{
    RecordWorkflow.Transition(record, RecordStatus.Submitted);
}
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
