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
using var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaGrpcClient>();

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

Native Console and MAUI hosts can also resolve `IHonuaProcessGrpcClient` for
the server-backed ProcessService path: validate plans, run dry runs, submit
jobs, inspect job state, retrieve results, and cancel jobs while reusing the
shared job models from `Honua.Sdk.Processes`.

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
var dryRun = await process.DryRunPlanAsync(plan, cancellationToken);
var job = await process.SubmitJobAsync(plan, cancellationToken: cancellationToken);
var status = await process.GetJobAsync(job.JobId, cancellationToken);

Console.WriteLine($"{validation.Valid}: {dryRun.Valid}: {status.Status}");
```

`HonuaProcessGrpcClient` uses the same `HonuaGrpcClientOptions` auth,
deadline, retry, and `PrimaryHttpMessageHandlerFactory` behavior as the
FeatureService client. Browser hosts should use `Honua.Sdk.Processes` REST
instead of native gRPC.

`ExecutePlanAsync` and `ExecutePlanStreamAsync` are thin wrappers for the
ProcessService proto methods. Current Honua Server deployments may return
`Unimplemented` for those calls until synchronous execute and execute-stream
support lands; use `SubmitJobAsync`, `GetJobAsync`, `GetJobResultAsync`, and
`CancelJobAsync` for current server contract coverage.

DI-resolved `IHonuaGrpcClient` and `IHonuaProcessGrpcClient` instances are
container-owned. Let the service provider dispose the underlying channel at
shutdown; manually constructed concrete clients remain responsible for their
own disposal.

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Console client contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/console-client-contracts.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
