# Sanitized diagnostic bundles (`honua doctor`)

`honua doctor` creates a local, bounded support artifact and never uploads it
automatically. The command uses the canonical
[`diagnostic-bundle.v1` JSON Schema](https://honua.io/schemas/diagnostic-bundle.v1.json),
mirrored byte-for-byte under `schemas/` and pinned to:

- support source commit `0c990fbe8f519a00a57e26dab21cbb8f80d559ea`;
- 6,494 bytes; and
- SHA-256 `4dd7282d17bb417d56f1c3cfa243e03b612a401e5d22be766658849287e431a9`.

The public provenance record is
[`diagnostic-bundle.v1.provenance.json`](https://honua.io/schemas/diagnostic-bundle.v1.provenance.json).
The .NET test lane runs the complete language-neutral support conformance
corpus and fails if the embedded schema, provenance, manifest, or emitted wire
shape drifts.

## Install and capture

The tool ships on the authenticated Honua GitHub Packages feed (not yet on
nuget.org). `dotnet tool install` does not read a repository `NuGet.config`,
so pass the feed with `--add-source`; the feed credentials must already be
configured as described in
[INSTALL.md](../INSTALL.md#install-from-github-packages-current-channel):

```bash
dotnet tool install --global Honua.Sdk.Cli --add-source https://nuget.pkg.github.com/honua-io/index.json
```

Capture the last failing HTTP exchange in a local JSON file. The input may
contain raw values because it is read only into memory and is never copied to
the output:

```json
{
  "request": {
    "method": "GET",
    "url": "https://server.example/api/v1/services/parcels?token=secret",
    "headers": {
      "authorization": "Bearer secret",
      "x-request-id": "request-1"
    }
  },
  "response": {
    "status": 500,
    "mediaType": "application/json",
    "headers": { "content-type": "application/json" },
    "body": { "error": "failed", "apiKey": "secret" }
  }
}
```

Then explicitly choose the classification and both consent values:

```bash
honua doctor \
  --exchange ./failure.json \
  --classification customer-data \
  --redaction-acknowledged=true \
  --share-with-support=false \
  --output ./diagnostic-bundle.json \
  --json
```

Add `--base-url https://server.example/honua` to attempt an anonymous,
credential-free capability probe. The configured base path is preserved and a
probe failure becomes a sanitized envelope without removing the supplied
failing exchange.

Review the local artifact before changing `--share-with-support=true` and
submitting it to support intake. Output is owner-readable/writable only on
platforms that support Unix file permissions. The process summary includes the
SDK version, runtime, target framework, probe outcome, and schema provenance;
those operational fields are not added to the strict v1 bundle because the
canonical schema does not permit them.

## Privacy boundary

The emitter:

- drops authorization, proxy authorization, cookies, API keys, signatures,
  tokens, and every non-allowlisted header;
- removes URL origin and user information, drops sensitive query parameters,
  placeholders all remaining query values, and placeholders unknown path
  segments;
- recursively redacts sensitive JSON keys and free-text credentials, provider
  tokens, AWS keys, JWTs, email addresses, and URL-encoded variants;
- rejects optional bundle identifiers and consent identities containing
  credential-shaped or personal material;
- hashes original in-memory body bytes but persists only a redacted preview,
  original byte count, SHA-256, and truncation/redaction flags; and
- refuses bodies above 25 MiB, probe responses above 256 KiB, previews above
  8 KiB, and bundles above 50 envelopes.

Absent optional properties are omitted rather than serialized as `null`. No raw
body, credential, cookie, configured secret, origin, or customer payload is
written to stdout, stderr, the final artifact, telemetry, or snapshots.

## Read-only replay

Replay one sanitized exchange against a separately configured server:

```bash
honua doctor \
  --replay ./diagnostic-bundle.json \
  --base-url https://server.example \
  --output ./diagnostic-replay.json \
  --timeout-ms 10000 \
  --json
```

Replay validates the entire input before network access, verifies the schema
pin and body hashes, sends no captured headers or query values, omits
credentials, and permits only one bounded `GET` or `HEAD`. It refuses mutation,
subscriptions, job submission, uploads, traversal, placeholder path segments,
non-HTTPS remote origins, unsafe headers, credential-bearing artifacts, hash
drift, malformed schemas, over-budget responses, timeout, and cancellation.
The result is a new schema-valid sanitized bundle; replay never modifies its
input.
