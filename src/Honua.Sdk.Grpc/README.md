# Honua.Sdk.Grpc

gRPC client for the Honua FeatureService. Provides typed point queries, server-side
streaming for large result sets, and apply-edits with spatial filters and statistics.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.Grpc
```

## Quick usage

```csharp
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Grpc.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaGrpc(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

using var client = provider.GetRequiredService<IHonuaGrpcClient>();

var response = await client.QueryFeaturesAsync(
    new QueryFeaturesRequest
    {
        ServiceId = "parcels",
        LayerId = 0,
        Where = "city = 'Boulder'",
        OutFields = ["objectid", "parcel_no"],
        ResultRecordCount = 500,
    },
    cancellationToken);

await foreach (var page in client.QueryFeaturesStreamAsync(
    new QueryFeaturesRequest { ServiceId = "parcels", LayerId = 0 },
    cancellationToken))
{
    Console.WriteLine($"Page with {page.Features.Count} features");
}
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
