# Process authoring & ArcObjects-GP migration

This guide covers authoring Honua geoprocessing (GP) processes in C# with the
`Honua.Sdk.Processes` authoring API, testing them locally with the in-process
harness, mapping common ArcObjects / ArcPy GP idioms onto Honua processes, and
federating an existing compiled .NET GP service during a phased migration.

Honua runs geoprocessing as [OGC API Processes](https://ogcapi.ogc.org/processes/).
A process declares typed inputs and outputs and is executed as an asynchronous
job. Honua GP executors are themselves C#, so for .NET shops the migration path
is **reimplement-as-Honua-process**: compiled ArcObjects assemblies cannot be
source-translated, so the logic is re-expressed against the Honua process model.
Federation bridges the gap while that port is in progress.

## When to use what

| Goal | Use |
|---|---|
| Describe a process (id, inputs, outputs) for the catalog | `HonuaProcessAuthoring.DefineProcess(...)` -> `HonuaProcessDefinition` |
| Build a multi-step analysis plan to submit | `HonuaProcessAuthoring.DefinePlan(...)` -> `HonuaProcessExecuteRequest` |
| Write and unit-test executor logic locally | `IHonuaProcessExecutor` + `HonuaProcessTestHarness` |
| Submit a job to a running server | `IHonuaProcessesClient` (see [README](../src/Honua.Sdk.Processes/README.md)) |

## Authoring a process definition

`HonuaProcessBuilder` produces a `HonuaProcessDefinition` and projects it to the
server's OGC API Processes description shape (the body served at
`GET /ogc/processes/processes/{id}`) via `ToOgcDescription()`.

```csharp
using Honua.Sdk.Processes.Authoring;

var definition = HonuaProcessAuthoring.DefineProcess("geometry.buffer")
    .WithTitle("Buffer")
    .WithDescription("Creates a polygon at a specified distance around each input geometry.")
    .WithCategory("geometry")
    .AddInput("wkb", HonuaProcessParameterValueType.Wkb, p => p
        .WithDisplayName("Input Geometry").Required())
    .AddInput("srid", HonuaProcessParameterValueType.Srid, p => p
        .WithDisplayName("Spatial Reference").Required())
    .AddInput("distance", HonuaProcessParameterValueType.FloatingPoint, p => p
        .WithDisplayName("Buffer Distance").Required())
    .AddInput("geodesic", HonuaProcessParameterValueType.Flag, p => p
        .WithDisplayName("Geodesic").WithDefault("false"))
    .AddOutput("outputFeatureLayer", HonuaProcessArtifactKind.FeatureLayer)
    .Build();

// Serialize to the server's OGC process-description format.
var ogc = definition.ToOgcDescription();
```

### Parameter value types

Each `HonuaProcessParameterValueType` projects to a JSON Schema fragment that
matches what Honua Server emits:

| Value type | Schema `type` | `contentMediaType` |
|---|---|---|
| `Text` | `string` | — |
| `WholeNumber` | `integer` | — |
| `FloatingPoint` | `number` | — |
| `Flag` | `boolean` | — |
| `Wkb` | `string` | `application/wkb` |
| `WkbArray` | `array` | `application/wkb` |
| `Srid` | `integer` | — |
| `LayerId` | `string` | — |

## Authoring an analysis plan

The canonical `honua-geoprocessing` process executes a multi-step plan (a DAG).
`HonuaAnalysisPlanBuilder` authors that plan and wraps it in an execute request.
It validates that every `dependsOn` references a declared step and rejects
duplicate or self-referential steps.

```csharp
var request = HonuaProcessAuthoring.DefinePlan("parcels-near-roads")
    .WithWorkflowFamily("analyze")
    .WithOutputs("featureLayer")
    .AddStep("query", "queryFeatures", s => s.WithInput("layerId", "parcels"))
    .AddGeoprocessStep("buffer", "geometry.buffer", s => s
        .WithInput("distance", 100)     // IFormattable overload -> invariant culture
        .DependsOn("query"))
    .BuildExecuteRequest();

// Submit through the REST client against the canonical process.
var job = await processes.SubmitJobAsync("honua-geoprocessing", request, ct);
```

## Testing an executor locally

Implement `IHonuaProcessExecutor` — the authoring-side mirror of the server's
`IJobExecutor` — so logic written against this SDK ports directly into a
server-hosted executor. Inputs arrive as a flattened `string` dictionary
(matching the server's job parameter contract); progress, logs, and artifacts
flow through `IHonuaProcessExecutionContext`.

```csharp
public sealed class BufferExecutor : IHonuaProcessExecutor
{
    public string ProcessId => "geometry.buffer";

    public async Task<HonuaProcessJobResult> ExecuteAsync(
        HonuaProcessJobInput job,
        IHonuaProcessExecutionContext context,
        CancellationToken cancellationToken)
    {
        await context.ReportProgressAsync(0, "starting", cancellationToken);
        var distance = double.Parse(job.GetRequired("distance"), CultureInfo.InvariantCulture);
        // ... run buffer, publish artifact ...
        await context.PublishArtifactAsync("layer://buffered", cancellationToken);
        return HonuaProcessJobResult.Success();
    }
}
```

`HonuaProcessTestHarness` runs the executor in-process. When a
`HonuaProcessDefinition` is supplied, it validates required inputs before
execution and captures everything the executor reported:

```csharp
var harness = new HonuaProcessTestHarness(new BufferExecutor(), definition);

var run = await harness.RunAsync(new Dictionary<string, string>
{
    ["wkb"] = "AAAA",
    ["srid"] = "4326",
    ["distance"] = "100",
});

Assert.True(run.Succeeded);
Assert.Single(run.Artifacts);
Assert.Equal(100, run.Progress[^1].PercentComplete);
```

You can also drive the harness directly from an authored plan step:

```csharp
var plan = HonuaProcessAuthoring.DefinePlan("p")
    .AddGeoprocessStep("buffer", "geometry.buffer", s => s.WithInput("distance", 100))
    .Build();

var run = await harness.RunAsync(plan.Steps[0]);
```

## ArcObjects / ArcPy GP idiom → Honua process mapping

Compiled ArcObjects GP tools cannot be source-translated; the migration is a
reimplementation against the Honua process model. The table maps the idioms a
.NET / ArcPy GP author reaches for onto their Honua equivalents.

| ArcObjects / ArcPy idiom | Honua process equivalent |
|---|---|
| `IGPFunction` / `IGPFunction2` tool class | `IHonuaProcessExecutor` (server-side: `IJobExecutor`) |
| `Execute(IArray parameters, ITrackCancel, IGPEnvironmentManager, IGPMessages)` | `ExecuteAsync(HonuaProcessJobInput, IHonuaProcessExecutionContext, CancellationToken)` |
| `ParameterInfo` / `IGPParameter` collection | `HonuaProcessBuilder.AddInput(...)` declarations |
| `IGPParameter.DataType` (`GPFeatureLayer`, `GPLong`, `GPDouble`, `GPBoolean`, `GPSpatialReference`) | `HonuaProcessParameterValueType` (`LayerId`, `WholeNumber`, `FloatingPoint`, `Flag`, `Srid`) |
| Output parameter (`Direction = esriGPParameterDirectionOutput`) | `HonuaProcessBuilder.AddOutput(...)` + `HonuaProcessJobResult.Outputs` |
| `IGPMessages.AddMessage(...)` | `IHonuaProcessExecutionContext.AppendLogAsync(...)` |
| `IStepProgressor` / `IGPMessages` percent updates | `IHonuaProcessExecutionContext.ReportProgressAsync(percent, phase)` |
| `ITrackCancel.Continue()` polling | `CancellationToken` / `ThrowIfCancellationRequested()` |
| ModelBuilder model (chained tools) | `HonuaAnalysisPlanBuilder` plan with `dependsOn` steps |
| `arcpy.Buffer_analysis(in, out, dist)` one-shot call | one `AddGeoprocessStep("buffer", "geometry.buffer", ...)` in a plan |
| Tool returns derived feature class | output artifact of kind `FeatureLayer` |
| `arcpy.env.outputCoordinateSystem` and friends | step inputs (`srid`) / `HonuaProcessExecutionContext` metadata |
| `GPToolbox` (.tbx) grouping of tools | the process catalog at `/ogc/processes/processes` |
| Synchronous in-process tool execution | asynchronous OGC job (`jobControlOptions: ["async-execute"]`) |

### Worked example

ArcPy:

```python
arcpy.analysis.Buffer("parcels", "parcels_buf", "100 Meters")
```

Honua (authored plan + executor logic):

```csharp
var plan = HonuaProcessAuthoring.DefinePlan("buffer-parcels")
    .AddStep("query", "queryFeatures", s => s.WithInput("layerId", "parcels"))
    .AddGeoprocessStep("buffer", "geometry.buffer", s => s
        .WithInput("distance", 100)
        .DependsOn("query"))
    .Build();
```

The buffer logic itself lives in a `geometry.buffer` `IHonuaProcessExecutor`,
unit-tested with `HonuaProcessTestHarness` and then registered with the server's
geoprocessing runtime as an `IJobExecutor`.

## Federating an existing compiled .NET GP service

While ArcObjects tools are being reimplemented, the existing compiled .NET GP
service keeps running and is **federated** behind Honua so callers see one
process catalog. The bridge is operational, not a code change in this SDK:

1. **Wrap the legacy service in OGC API Processes.** Expose the legacy GP
   service's tools through an OGC API Processes facade (each legacy tool becomes
   a process with the same input/output contract you would declare with
   `HonuaProcessBuilder`). Author the *descriptions* with this SDK so the
   federated processes advertise the same schema shape as native ones.
2. **Register the facade as a federated process source** in Honua Server so its
   processes appear alongside native ones in `/ogc/processes/processes`. Honua
   routes execution for those ids to the legacy facade.
3. **Migrate tool-by-tool.** As each ArcObjects tool is reimplemented as a
   native `IJobExecutor`, flip its process id from the federated facade to the
   native executor. The process id and I/O contract are unchanged, so callers
   and authored plans are unaffected.
4. **Decommission the facade** once every tool has a native executor.

Authoring descriptions for both the federated and native versions with the same
`HonuaProcessBuilder` code keeps the contract identical across the cutover, which
is what makes the tool-by-tool swap transparent to clients.

> Scope note: a codemod that automatically translates compiled .NET / ArcObjects
> tools is **not** feasible and is out of scope. Federation plus manual
> reimplementation is the supported path.

## See also

- [`Honua.Sdk.Processes` README](../src/Honua.Sdk.Processes/README.md) — REST
  client surface for submitting and polling jobs.
- [Geometry analysis](geometry-analysis.md) — client-side geometry helpers.
