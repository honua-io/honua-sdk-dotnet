# Honua.Sdk.Field

Contracts and pure-.NET helpers for field-data-capture apps built on the Honua
SDK: form definitions and sections, field types and validation rules, calculated
expressions, duplicate detection, and a record-state-machine workflow.
`WorkTaskClient` reads assigned tasks and submits worker commands on servers
that expose the work-task API. Submit independent captured `FieldRecord`s
through the SDK's feature-edit clients.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

Stable releases are on [nuget.org](https://www.nuget.org/packages/Honua.Sdk.Field/) and install anonymously. An unversioned add resolves to the newest stable release (the current SDK version is in the [repo README](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/README.md#status)). Prereleases stay on the authenticated GitHub Packages feed; see [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

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

## XLSForm workbooks

`Honua.Sdk.Field.Forms.Authoring` reads and writes `.xlsx` files and converts
between worksheet rows and `FormDefinition`. Conversion returns a complete
value only when there are no diagnostics. Each diagnostic identifies a sheet,
one-based row and column so an author can correct the workbook.

```csharp
using Honua.Sdk.Field.Forms.Authoring;

using var input = File.OpenRead("inspection.xlsx");
var workbook = XlsFormWorkbookIO.Read(input);
if (workbook.Value is null)
{
    throw new InvalidDataException(workbook.Diagnostics[0].Message);
}

var imported = XlsFormConverter.Import(workbook.Value);
if (imported.Value is null)
{
    foreach (var issue in imported.Diagnostics)
    {
        Console.WriteLine($"{issue.Sheet}:{issue.RowNumber}:{issue.Column}: {issue.Message}");
    }
}
else
{
    var exported = XlsFormConverter.Export(imported.Value);
    if (exported.Value is not null)
    {
        using var output = File.Create("inspection-export.xlsx");
        XlsFormWorkbookIO.Write(output, exported.Value);
    }
}
```

The settings sheet requires `form_id` and `form_title`; `version` is optional.
Supported survey types are text, integer, decimal, date, time, dateTime,
select_one, select_multiple, geopoint, geotrace, geoshape, image, audio, video,
barcode and calculate. Flat groups and repeats become form sections. Static
required values, labels, hints, choice lists, relevance, constraints and
calculations are preserved. Expressions use the field expression syntax,
including `${field_name}` references and the `concat`, `if`, `floor` and
`selected` functions. For example, `selected(${hazards}, 'fire')` matches
the exact, case-sensitive choice code in a multi-select answer. Expressions also accept
XLSForm `div` and `mod`, `true()` and `false()`, and either quote style for string literals. Integer
questions retain an integer constraint when exported as
decimal questions.

Unsupported nonempty columns, dynamic required expressions, nested groups or
repeats, cross-repeat or unknown references and unsupported expression functions produce
diagnostics. Export also reports SDK behavior that has no supported XLSForm
representation, such as provider field bindings, media policies and hierarchical
choices. Service and layer targets are deployment bindings managed by the
caller, separately from the workbook.

Workbook IO treats cell text literally and rejects Excel formulas, external
workbook relationships, duplicate cells and invalid row references. Reads are
bounded to 8 MiB compressed, 32 MiB uncompressed, 128 archive entries, 10,000
rows per sheet, 64 columns and 100,000 cells per sheet. Invalid workbooks return
a workbook diagnostic; stream IO failures propagate to the caller. Writes
validate and buffer the workbook before writing to the caller's stream.

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)

Calculated fields may depend on earlier calculated fields in the same scope; forward and cyclic calculated dependencies are rejected.


## Assigned work

Use `WorkTaskClient` with an authenticated `HttpClient` configured for the
server. Supply the current viewer's exact assignee handle and service ID to
`ListAssignedAsync`. Pass the returned `NextCursor` to read the next page.
Keep local assignment data partitioned by server, service, and signed-in
account. If the viewer's assignee handle is unavailable, skip the listing.

Persist each transition or completion with its command ID, expected version,
and request payload before attempting offline replay. Reuse those values on
retries. The server decides which actions are permitted and returns an
`accepted` or `duplicate` receipt. A `WorkTaskVersionConflictException`
indicates a stale version. Reload before composing a new command.
