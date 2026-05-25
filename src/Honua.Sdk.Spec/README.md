# Honua.Sdk.Spec

REST and SSE client for the Honua spec workspace API. Supports validating spec
DSL text or canonical JSON documents, compiling them into plans with cost
estimates, streaming apply events, cancelling in-flight apply runs, and
retrieving cached artifacts by content hash.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.Spec
```

## Quick usage

```csharp
using Honua.Sdk.Spec;
using Honua.Sdk.Spec.Extensions;
using Honua.Sdk.Spec.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaSpec(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaSpecClient>();

var validation = await client.ValidateAsync(
    new SpecValidateRequest
    {
        Text = File.ReadAllText("pipeline.spec"),
        IncludeCanonicalJson = true,
    },
    cancellationToken);

if (validation.IsValid)
{
    var document = new SpecDocumentRequest
    {
        GrammarVersion = "1.0",
        ProcessFamilyVersion = "1.0",
        Nodes = [/* canonical nodes */],
    };

    await using var stream = await client.ApplyAsync(document, cancellationToken);
    string? completedHash = null;
    await foreach (var ev in stream.Events.WithCancellation(cancellationToken))
    {
        Console.WriteLine($"{ev.Kind} {ev.NodeId}");
        if (completedHash is null &&
            ev.Kind == SpecApplyEventKind.Succeeded &&
            ev.ContentHash is not null)
        {
            completedHash = ev.ContentHash;
        }
    }

    if (completedHash is { } hash)
    {
        var artifact = await client.GetArtifactAsync(hash, cancellationToken);
        Console.WriteLine($"{artifact.ContentHash}: {artifact.ContentType} ({artifact.Content.Length} bytes)");
    }
}
```

`GetArtifactAsync` calls `GET /v1/spec/artifact/{hash}` and returns
`HonuaSpecArtifact` with the raw bytes, response content type, and
`X-Spec-Content-Hash` echo. Successful responses are treated as binary payloads
and buffered into `HonuaSpecArtifact.Content`; problem-details bodies are read
only for non-success responses. Use this for bounded, content-hash-addressed
cache artifacts, not unbounded archive or publish/download flows.

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Spec workspace contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/spec-workspace-contracts.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
