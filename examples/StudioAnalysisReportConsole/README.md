# StudioAnalysisReportConsole

Demonstrates the Console Studio analysis-report read client
(`Honua.Sdk.Studio.IHonuaStudioReportsClient`): retrieving the structured report
envelope and rendering it to Markdown for a completed geoprocessing job.

```bash
dotnet run --project examples/StudioAnalysisReportConsole/StudioAnalysisReportConsole.csproj
```

By default the sample runs in `simulated` mode against an in-process handler, so
it is runnable with no live server. Set `HONUA_STUDIO_MODE=server` to call a real
Honua Server.

## Environment variables

| Variable | Purpose | Default |
| --- | --- | --- |
| `HONUA_STUDIO_MODE` | `simulated` (in-process) or `server` (live) | `simulated` |
| `HONUA_STUDIO_SERVER_URL` | Honua server base address | `https://localhost:5001` |
| `HONUA_STUDIO_JOB_ID` | Completed job whose report to fetch | `job-demo` |
| `HONUA_STUDIO_API_KEY` | API key auth (optional) | unset |
| `HONUA_STUDIO_BEARER_TOKEN` | OAuth/OIDC or service-account bearer token (optional) | unset |

Loopback HTTP is allowed for local development; authenticated non-local targets
must use HTTPS.

## Host registration

Both the Blazor Web host and the optional .NET MAUI Blazor Hybrid host register
the same client through `AddHonuaStudio(...)`; only auth and transport differ.

Blazor Web (delegated bearer token or BFF):

```csharp
builder.Services.AddHonuaStudio(o =>
{
    o.BaseAddress = new Uri(serverUrl);
    o.BearerTokenProvider = tokenProvider.GetAccessTokenAsync;
});
```

Native MAUI (client-certificate / mTLS transport configured by the host):

```csharp
builder.Services.AddHonuaStudio(o =>
{
    o.BaseAddress = new Uri(serverUrl);
    o.PrimaryHttpMessageHandlerFactory = () => CreateMutualTlsHandler(profile);
});
```

The report DTOs (`HonuaAnalysisReport`, the `kind`-discriminated section
hierarchy, `HonuaRenderedReport`) live in `Honua.Sdk.Abstractions`
(`Honua.Sdk.Abstractions.Studio`), so browser and native hosts share one set.
