# Honua.Sdk.Processes

Browser-safe OGC API Processes REST client and shared job models for Honua
Console hosts.

Install directly when a browser or server-side host only needs process/job
REST:

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
