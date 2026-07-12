# Diagnostic bundle contract mirror

The files in this directory mirror Honua Support's canonical public
`diagnostic-bundle.v1` schema, provenance, and language-neutral conformance
corpus byte-for-byte. The source of truth remains `honua-io/honua-support` at
the commit and SHA-256 recorded in
`diagnostic-bundle.v1.provenance.json`.

`Honua.Sdk.Cli` embeds the exact schema bytes and refuses to emit a bundle when
the byte pin or instance validation fails. The CLI test lane executes every
valid and invalid corpus case so contract drift fails before publishing the
tool.
