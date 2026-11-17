---
type: guide
title: "Import and export XLSForm workbooks from the command line"
description: "honua forms import turns an XLSForm workbook into a draft form for a layer; honua forms export downloads a published form as a workbook; --local converts without a server."
resource: "https://www.nuget.org/packages/Honua.Sdk.Cli/"
tags: [forms, xlsform, cli, field]
---
# XLSForm import and export (`honua forms`)

The `honua` .NET tool (package `Honua.Sdk.Cli`) imports and exports form
packages as XLSForm `.xlsx` workbooks. It uses the same converter as the server
and Console (`Honua.Sdk.Field.Forms.Authoring`), so a workbook the CLI accepts
locally converts the same way on the server, which also checks each question
against the target layer's attributes.

```bash
dotnet tool install --global Honua.Sdk.Cli
honua forms --help
```

## Server commands

The server URL comes from `--base-url` or `HONUA_BASE_URL` and must use HTTPS
(plain HTTP is accepted only for `localhost`). The admin API key comes from the
`HONUA_API_KEY` environment variable and is sent as the `X-API-Key` header. The
key is never accepted on the command line, so it does not land in shell
history. Redirects are not followed.

### Import

```bash
export HONUA_BASE_URL="https://your-honua-server"
export HONUA_API_KEY="<admin-api-key>"
honua forms import inspection.xlsx --service parcels --layer 0
```

The command posts the workbook to
`POST /api/v1/admin/forms/packages/import?serviceId=<id>&layerId=<id>` as the
multipart part `file`. Questions bind to the layer attribute of the same name.
On success it prints the new draft and exits 0:

```text
formId=inspection
version=3
status=draft
```

The import creates a draft only. Validate and publish it through the forms
admin API or Console.

When the server rejects the workbook, no draft is created. The command prints
the reason and every blocking cell to standard error, then exits 1:

```text
honua forms import failed: HTTP 400: The workbook is not an XLSForm survey a Honua form package can represent.
survey row 4, column type: ...
```

### Export

```bash
honua forms export inspection -o inspection.xlsx
```

The command downloads the current published version from
`GET /api/v1/admin/forms/packages/{formId}/export`. Without `-o` it writes
`<formId>.xlsx` in the current directory, with `.`, `/` and `\` in the id
replaced by `_`. A response that is not an XLSX workbook (for example a proxy
sign-in page) is not saved and exits 1. A form that uses behavior XLSForm
cannot represent returns HTTP 422 with diagnostics; the command prints them,
writes no file and exits 1. An unknown or unpublished form returns HTTP 404.

## Local conversion (`--local`)

`--local` converts on your machine without a server or API key:

```bash
# Workbook to form definition JSON (stdout, or a file with -o)
honua forms import inspection.xlsx --local -o inspection.form.json

# Form definition JSON back to a workbook
honua forms export inspection.form.json --local -o inspection.xlsx
```

The JSON is a `Honua.Sdk.Field.Forms.FormDefinition` with camelCase property
names and string enum values. Local import checks the XLSForm itself; it does
not check the questions against a layer's attributes, which only the server
import does. Use local conversion to check a workbook before uploading it or
to keep forms under version control.

## Options and exit codes

| Option | Meaning |
|---|---|
| `--service <id>` | Import only. The service that owns the target layer. |
| `--layer <id>` | Import only. The integer layer id questions bind to. |
| `-o`, `--output <file>` | Destination file. Required to be non-empty when present. Export defaults to `<formId>.xlsx`; local import defaults to stdout. A server import does not take this option. |
| `--base-url <url>` | Server base URL (or `HONUA_BASE_URL`). |
| `--timeout-ms <n>` | Request timeout, 1 to 300000 ms (default 60000). |
| `--local` | Convert without a server; cannot be combined with the server options. |
| `--json` | Print one `honua.forms-result.v1` JSON object on stdout, including diagnostics. A local import without `-o` prints the form definition JSON itself. |

| Exit code | Meaning |
|---|---|
| 0 | Imported, exported or converted. |
| 1 | Rejected with diagnostics, or the server, network or file operation failed. |
| 2 | Invalid arguments. Nothing was sent. |

Workbooks are limited to 8 MiB, the same limit the server applies.
