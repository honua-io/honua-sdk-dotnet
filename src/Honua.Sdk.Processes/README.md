# Honua.Sdk.Processes

Browser-safe OGC API Processes REST client and shared job models for Honua
Console hosts.

## Install

Install directly when a browser or server-side host only needs process/job
REST.

Stable releases are on [nuget.org](https://www.nuget.org/packages/Honua.Sdk.Processes/) and install anonymously. An unversioned add resolves to the newest stable release (the current SDK version is in the [repo README](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/README.md#status)). Prereleases stay on the authenticated GitHub Packages feed; see [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet add package Honua.Sdk.Processes
```

```csharp
using Honua.Sdk.Processes.Extensions;

builder.Services.AddHonuaProcesses(o =>
{
    o.BaseAddress = new Uri("https://your-honua-server");
    o.BearerTokenProvider = tokenProvider.GetAccessTokenAsync;
});
```

Use `IHonuaProcessesClient` in Blazor Web hosts for process discovery, async job
submission, polling, dismissal, and result retrieval. Native hosts that need
gRPC job lifecycle access can also use the same model package through
`Honua.Sdk.Grpc`'s process client.

## REST surface

The client targets the OGC API Processes surface under `/ogc/processes`.

| SDK method | HTTP contract |
|---|---|
| `GetLandingPageAsync` | `GET /ogc/processes?f=json` |
| `GetConformanceAsync` | `GET /ogc/processes/conformance?f=json` |
| `ListProcessesAsync` | `GET /ogc/processes/processes` |
| `GetProcessAsync` | `GET /ogc/processes/processes/{processId}` |
| `ExecuteSyncAsync` | Describe, execute once, and wait for results within one elapsed timeout; omit `Prefer` for advertised sync, otherwise `respond-async` |
| `WaitForTerminalAsync` | Poll `GET /ogc/processes/jobs/{jobId}` within one elapsed timeout; return only on success |
| `SubmitJobAsync` | `POST /ogc/processes/processes/{processId}/execution` with `Prefer: respond-async` |
| `ListJobsAsync` | `GET /ogc/processes/jobs` with optional `limit` |
| `GetJobAsync` | `GET /ogc/processes/jobs/{jobId}` |
| `DismissJobAsync` | `DELETE /ogc/processes/jobs/{jobId}` |
| `GetJobResultsAsync` | `GET /ogc/processes/jobs/{jobId}/results` |

`HonuaProcessJobStatus` maps OGC wire fields such as `processID`, `jobID`,
`status`, and `progress` to typed SDK properties. Document-mode results are
returned as `HonuaProcessResults.Outputs`, a JSON extension-data dictionary
keyed by output identifier. Use `HonuaProcessExecuteInputs.FromPlan(...)` for
the canonical `honua-geoprocessing` `inputs.plan` contract. Use the dictionary
`SubmitJobAsync` overload for advertised concrete processes such as
`geometry.buffer`; those values serialize directly under `inputs` with no
`plan` property.

The examples below use the current Honua Server canonical process id,
`honua-geoprocessing`. The client accepts any process id advertised by the
server's process list.

```csharp
using System.Text.Json;
using Honua.Sdk.Processes.Models;

var job = await processes.SubmitJobAsync(
    "honua-geoprocessing",
    new HonuaProcessExecuteRequest
    {
        Inputs = HonuaProcessExecuteInputs.FromPlan(
            new HonuaAnalysisPlan
            {
                PlanId = "plan-1",
                WorkflowFamily = "analyze",
                Outputs = ["featureLayer"],
                Steps =
                [
                    new HonuaPlanStep
                    {
                        StepId = "buffer",
                        Kind = "geoprocess",
                        ProcessId = "geometry.buffer",
                        Inputs = new Dictionary<string, string>
                        {
                            ["wkb"] = "AAAA",
                            ["srid"] = "4326",
                            ["distance"] = "25"
                        }
                    }
                ]
            })
    },
    cancellationToken);

var status = await processes.GetJobAsync(job.JobId, cancellationToken);
var results = await processes.GetJobResultsAsync(job.JobId, cancellationToken);
```

Concrete processes can also accept their advertised inputs directly, without a
`plan` wrapper:

```csharp
var bufferJob = await processes.SubmitJobAsync(
    "geometry.buffer",
    new Dictionary<string, JsonElement>
    {
        ["wkb"] = JsonSerializer.SerializeToElement("AAAA"),
        ["srid"] = JsonSerializer.SerializeToElement(4326),
        ["distance"] = JsonSerializer.SerializeToElement(25.5)
    },
    cancellationToken);
```

Non-success responses throw `HonuaProcessesException`. When the server returns
problem-details JSON, the exception preserves `StatusCode`, `ProblemType`,
`ProblemTitle`, `ProblemDetail`, and the raw `ResponseBody`.

`ExecuteSyncAsync` accepts a required `timeout` and an optional `pollingInterval`
(default one second). It preserves 200 document/raw results and waits for 201/202
job responses without resubmitting. `HonuaProcessExecutionResponse.Document` holds
document outputs; `RawContent` and `MediaType` preserve arbitrary raw bytes and
multipart framing. `Job` holds the successful terminal job when one was returned.
Failed/dismissed jobs and elapsed timeouts throw `HonuaProcessesException`, with
`JobId` when observed; caller cancellation remains `OperationCanceledException`.
Timeout/cancellation stops local waiting without dismissing the remote job.
The helper submits only when `sync-execute` or `async-execute` is advertised;
missing, malformed, or unsupported mode metadata fails before submission.

The shipped `HonuaProcessesClient` implements both helpers. To preserve existing
custom `IHonuaProcessesClient` implementations, the new interface methods have
default bodies that throw `NotSupportedException` unless the client implements
them. Existing lifecycle methods retain their contracts.

See the [buffer → layer clip example](https://github.com/honua-io/honua-sdk-dotnet/tree/trunk/examples/GeoprocessingBufferClipConsole)
and [bounded execution snippet](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/process-authoring.md#bounded-rest-execution).

## Authoring processes and plans

The `Honua.Sdk.Processes.Authoring` namespace adds a fluent API for authoring
Honua geoprocessing processes in C# and unit-testing executor logic locally,
without a running server.

```csharp
using Honua.Sdk.Processes.Authoring;

// Describe a process for the catalog.
var definition = HonuaProcessAuthoring.DefineProcess("geometry.buffer")
    .WithTitle("Buffer")
    .AddInput("wkb", HonuaProcessParameterValueType.Wkb, p => p.Required())
    .AddInput("distance", HonuaProcessParameterValueType.FloatingPoint, p => p.Required())
    .AddOutput("outputFeatureLayer", HonuaProcessArtifactKind.FeatureLayer)
    .Build();

var ogcDescription = definition.ToOgcDescription(); // server process-description shape

// Author a multi-step plan for the canonical honua-geoprocessing process.
var request = HonuaProcessAuthoring.DefinePlan("plan-1")
    .AddGeoprocessStep("buffer", "geometry.buffer", s => s.WithInput("distance", 100))
    .BuildExecuteRequest();

// Unit-test an IHonuaProcessExecutor in-process.
var harness = new HonuaProcessTestHarness(myExecutor, definition);
var run = await harness.RunAsync(new Dictionary<string, string>
{
    ["wkb"] = "AAAA",
    ["distance"] = "100",
});
Assert.True(run.Succeeded);
```

See [`docs/process-authoring.md`](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/process-authoring.md)
for the full guide, including the ArcObjects/ArcPy GP migration mapping and the
federate-bridge approach.
