# Honua.Sdk.Grpc

gRPC client for Honua native services. Provides typed FeatureService queries,
server-side streaming for large result sets, apply-edits with spatial filters
and statistics, and ProcessService job lifecycle access for native hosts.

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

Native Console and MAUI hosts can also resolve `IHonuaProcessGrpcClient` to
validate, dry-run, submit, stream, cancel, and inspect ProcessService jobs while
reusing the shared job models from `Honua.Sdk.Processes`.

```csharp
using Honua.Sdk.Processes.Models;

var process = provider.GetRequiredService<IHonuaProcessGrpcClient>();

var plan = new HonuaAnalysisPlan
{
    PlanId = "plan-1",
    WorkflowFamily = "analyze",
    Outputs = ["summary"],
    Steps =
    [
        new HonuaPlanStep
        {
            StepId = "buffer",
            Kind = "geometry.buffer",
            Inputs = new Dictionary<string, string>
            {
                ["distance"] = "25"
            }
        }
    ]
};

var validation = await process.ValidatePlanAsync(plan, cancellationToken);
var job = await process.SubmitJobAsync(plan, cancellationToken: cancellationToken);

await foreach (var evt in process.ExecutePlanStreamAsync(plan, cancellationToken: cancellationToken))
{
    Console.WriteLine($"{evt.EventType}: {evt.Progress?.ProgressPercent}");
}
```

`HonuaProcessGrpcClient` uses the same `HonuaGrpcClientOptions` auth,
deadline, retry, and `PrimaryHttpMessageHandlerFactory` behavior as the
FeatureService client. Browser hosts should use `Honua.Sdk.Processes` REST
instead of native gRPC.

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Console client contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/console-client-contracts.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
