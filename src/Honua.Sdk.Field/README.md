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

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk.Field --source honua
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

## Local project packages

`Honua.Sdk.Field.Projects.FieldProjectPackage` is the portable no-cloud
handoff model for field project packages. It groups SDK source descriptors,
forms, source/form bindings, offline artifact references, media policy, record
lifecycle policy, and local task packets so mobile or desktop runtimes can
import a project from local files without cloud discovery or a hosted designer.

```csharp
using Honua.Sdk.Field.Projects;

var package = FieldProjectPackage.ParseJson(File.ReadAllText("field-project.json"));
var validation = package.Validate();

if (!validation.IsValid)
{
    foreach (var issue in validation.Issues)
    {
        Console.WriteLine($"{issue.Path}: {issue.Message}");
    }
}
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
