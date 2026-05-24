// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes.Tests;

public sealed class ConsoleJobFixtureTests
{
    private static readonly string[] ExpectedOgcProcessesPaths =
    [
        "/ogc/processes",
        "/ogc/processes/conformance",
        "/ogc/processes/jobs",
        "/ogc/processes/jobs/{jobId}",
        "/ogc/processes/jobs/{jobId}/results",
        "/ogc/processes/openapi.json",
        "/ogc/processes/processes",
        "/ogc/processes/processes/{processId}",
        "/ogc/processes/processes/{processId}/execution"
    ];

    [Fact]
    public void JobsFixture_DeserializesThroughProcessesJsonContext()
    {
        using var document = LoadFixture("jobs.v1.json");
        var root = document.RootElement;

        Assert.Equal("honua.console-jobs.v1", root.GetProperty("schemaVersion").GetString());

        var processes = JsonSerializer.Deserialize(
            root.GetProperty("processes").GetRawText(),
            ProcessesJsonContext.Default.HonuaProcessList)
            ?? throw new InvalidOperationException("Processes fixture was empty.");
        var submitted = JsonSerializer.Deserialize(
            root.GetProperty("submittedJob").GetRawText(),
            ProcessesJsonContext.Default.HonuaProcessJobStatus)
            ?? throw new InvalidOperationException("Submitted job fixture was empty.");
        var jobs = JsonSerializer.Deserialize(
            root.GetProperty("jobs").GetRawText(),
            ProcessesJsonContext.Default.HonuaProcessJobList)
            ?? throw new InvalidOperationException("Job list fixture was empty.");
        var result = JsonSerializer.Deserialize(
            root.GetProperty("result").GetRawText(),
            ProcessesJsonContext.Default.HonuaProcessResults)
            ?? throw new InvalidOperationException("Job result fixture was empty.");

        Assert.Equal("honua.analysis", processes.Processes.Single().Id);
        Assert.Equal("job-accepted", submitted.JobId);
        Assert.Contains(jobs.Jobs, job => job.Status == "running");
        Assert.Contains(jobs.Jobs, job => job.Status == "successful");
        Assert.Contains(jobs.Jobs, job => job.Status == "failed");
        Assert.Contains(jobs.Jobs, job => job.Status == "dismissed");
        Assert.True(result.Outputs.ContainsKey("summary"));
        Assert.True(result.Outputs.ContainsKey("artifact"));
    }

    [Fact]
    public void OpenApiPathsFixture_MatchesPinnedOgcProcessesSnapshot()
    {
        using var document = LoadFixture("ogc-processes-openapi-paths.v1.json");
        var root = document.RootElement;

        Assert.Equal("honua.console-ogc-processes-openapi-paths.v1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("honua-server/src/Honua.Server/ogc-processes-openapi.json", root.GetProperty("source").GetString());

        var paths = root.GetProperty("paths")
            .EnumerateArray()
            .Select(path => path.GetString())
            .OfType<string>()
            .ToArray();

        Assert.Equal(ExpectedOgcProcessesPaths, paths);
    }

    private static JsonDocument LoadFixture(string name)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "contracts", "fixtures", "console", name)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Honua.Sdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the honua-sdk-dotnet repository root.");
    }
}
