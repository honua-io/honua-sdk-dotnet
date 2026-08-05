# Honua.Sdk.Processes

Browser-safe OGC API Processes REST client and shared job models for Honua
Console hosts.

## Install

Install directly when a browser or server-side host only needs process/job
REST.

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk.Processes --source honua
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
