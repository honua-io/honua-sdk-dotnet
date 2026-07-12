# Honua .NET CLI

Install the tool, then use `honua doctor` to create a local, sanitized support
bundle:

```bash
dotnet tool install --global Honua.Sdk.Cli
honua doctor --exchange failure.json --classification customer-data \
  --redaction-acknowledged=true --share-with-support=false \
  --output diagnostic-bundle.json
```

See the repository's [diagnostic bundle guide](../../docs/diagnostic-bundles.md)
for the privacy boundary, capability probe, and read-only replay contract.
