// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text;
using System.Text.Json;
using Honua.Sdk.Processes;
using Honua.Sdk.Processes.Exceptions;
using Honua.Sdk.Processes.Extensions;
using Honua.Sdk.Processes.Models;
using Microsoft.Extensions.DependencyInjection;

if (!Uri.TryCreate(Environment.GetEnvironmentVariable("HONUA_BASE_URL"), UriKind.Absolute, out var baseAddress))
{
    await Console.Error.WriteLineAsync("Set HONUA_BASE_URL to a live Honua Server URL. See this sample's README.");
    return 2;
}

var services = new ServiceCollection();
services.AddHonuaProcesses(options =>
{
    options.BaseAddress = baseAddress;
    options.ApiKey = Environment.GetEnvironmentVariable("HONUA_API_KEY");
    options.BearerToken = Environment.GetEnvironmentVariable("HONUA_BEARER_TOKEN");
    options.Timeout = TimeSpan.FromMinutes(2);
});
await using var provider = services.BuildServiceProvider();
var processes = provider.GetRequiredService<IHonuaProcessesClient>();
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};
Console.CancelKeyPress += cancelHandler;

try
{
    // EPSG:4326 buffer distances are degrees, not meters. This is a planar 0.01-degree example.
    using var bufferInputs = JsonDocument.Parse("""
        {"wkb":{"type":"Point","coordinates":[-157.86,21.31]},"srid":4326,"distance":0.01}
        """);
    var buffer = await processes.ExecuteSyncAsync("geometry.buffer", RawRequest(bufferInputs.RootElement),
        TimeSpan.FromMinutes(2), cancellationToken: cancellation.Token);
    using var bufferJson = JsonDocument.Parse(buffer.RawContent);
    var bufferFeature = bufferJson.RootElement;
    if (bufferFeature.GetProperty("type").GetString() != "Feature"
        || bufferFeature.GetProperty("geometry").GetProperty("type").GetString() != "Polygon")
    {
        throw new InvalidDataException("The buffer process did not return a GeoJSON polygon Feature.");
    }

    // overlay.clip preserves target-layer attributes and clips to the actual buffer polygon.
    // geometry.clip instead uses the clipping geometry's bounding envelope.
    using var clipLayer = JsonDocument.Parse($$"""
        {"type":"FeatureCollection","features":[{{bufferFeature.GetRawText()}}]}
        """);
    const string inputLayer = """
        {"type":"FeatureCollection","features":[
          {"type":"Feature","properties":{"name":"Honolulu example area"},"geometry":
            {"type":"Polygon","coordinates":[[[-157.88,21.29],[-157.84,21.29],[-157.84,21.33],[-157.88,21.33],[-157.88,21.29]]]}}
        ]}
        """;
    var clipRequest = new HonuaProcessExecuteRequest
    {
        Response = "raw",
        Inputs = HonuaProcessExecuteInputs.FromDirectInputs(new Dictionary<string, JsonElement>
        {
            ["input"] = JsonSerializer.SerializeToElement(DataUri(inputLayer)),
            ["clip"] = JsonSerializer.SerializeToElement(DataUri(clipLayer.RootElement.GetRawText()))
        })
    };
    var clipped = await processes.ExecuteSyncAsync("overlay.clip", clipRequest,
        TimeSpan.FromMinutes(2), cancellationToken: cancellation.Token);
    using var clipJson = JsonDocument.Parse(clipped.RawContent);
    if (clipJson.RootElement.GetProperty("type").GetString() != "FeatureCollection"
        || clipJson.RootElement.GetProperty("features").GetArrayLength() == 0)
    {
        throw new InvalidDataException("The clip process did not return a nonempty GeoJSON FeatureCollection.");
    }

    var outputDirectory = Path.Combine(Environment.CurrentDirectory, $"buffer-clip-{Guid.NewGuid():N}");
    Directory.CreateDirectory(outputDirectory);
    await SaveOutputAsync("buffer", buffer, outputDirectory, cancellation.Token);
    await SaveOutputAsync("clip", clipped, outputDirectory, cancellation.Token);
    return 0;
}
catch (HonuaProcessesException ex)
{
    await Console.Error.WriteLineAsync($"Process request failed ({(int)ex.StatusCode}), job {ex.JobId ?? "not observed"}: {ex.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    await Console.Error.WriteLineAsync("Waiting cancelled. Any admitted server job may still be running.");
    return 1;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}

static HonuaProcessExecuteRequest RawRequest(JsonElement inputs) => new()
{
    Response = "raw",
    Inputs = HonuaProcessExecuteInputs.FromDirectInputs(
        inputs.EnumerateObject().ToDictionary(property => property.Name, property => property.Value))
};

static string DataUri(string geoJson)
    => "data:application/geo+json;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(geoJson));

static async Task SaveOutputAsync(string name, HonuaProcessExecutionResponse result, string directory, CancellationToken cancellationToken)
{
    var path = Path.Combine(directory, name + ".geojson");
    await File.WriteAllBytesAsync(path, result.RawContent.ToArray(), cancellationToken);
    await Console.Out.WriteLineAsync(
        $"{name}: {new Uri(path).AbsoluteUri} ({result.MediaType}); server job: {result.Job?.JobId ?? "synchronous response"}".AsMemory(), cancellationToken);
}
