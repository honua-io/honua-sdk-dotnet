# FieldFormConsole

Deterministic console sample for the Honua SDK field form contracts. It exercises
`Honua.Sdk.Field.Forms.FormValidator` and
`Honua.Sdk.Field.Forms.CalculatedFieldEvaluator` against a portable
`FormDefinition` and `FieldRecord` end to end, with no UI framework.

## What it demonstrates

A street-tree inspection form with three sections covering:

- Required fields and string length validation (`firstName`, `lastName`).
- Regex validation (`contactEmail`).
- Numeric range validation (`healthRating`, 1-5).
- Single-choice validation against an allowed list (`species`).
- Conditional visibility: `removalReason` is only required when `healthRating < 2`.
- Two calculated fields: `inspectorName = concat($firstName, ' ', $lastName)` and
  `sampleTotal = sum($leafSamples, $soilSamples)`.

The sample runs two passes:

1. An incomplete record that violates several constraints, printing the
   field-level validation errors.
2. A complete record that validates cleanly, showing the computed display name
   and sample total.

Calculated fields are evaluated before validation, mirroring the order a host
applies them prior to submission.

## Run

```bash
dotnet run --project examples/FieldFormConsole/FieldFormConsole.csproj
```

No live Honua Server is required; the form and records are in-process and
deterministic.

## SDK / mobile boundary

This sample only touches the provider-neutral form, validation, and
calculated-field contracts. Form rendering, media capture, and device input live
in `honua-mobile`.
