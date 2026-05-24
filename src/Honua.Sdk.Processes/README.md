# Honua.Sdk.Processes

Browser-safe OGC API Processes REST client and shared job models for Honua
Console hosts.

Install directly when a browser or server-side host only needs process/job
REST:

```bash
dotnet add package Honua.Sdk.Processes
```

```csharp
builder.Services.AddHonuaProcesses(o =>
{
    o.BaseAddress = new Uri("https://your-honua-server");
    o.BearerTokenProvider = tokenProvider.GetAccessTokenAsync;
});
```

Use `IHonuaProcessesClient` in Blazor Web hosts for process discovery, async job
submission, polling, dismissal, and result retrieval. Native hosts that need
full gRPC job lifecycle access can also use the same model package through
`Honua.Sdk.Grpc`'s process client.
