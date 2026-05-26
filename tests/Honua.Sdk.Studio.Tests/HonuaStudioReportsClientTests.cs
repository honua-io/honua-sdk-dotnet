// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Honua.Sdk.Abstractions.Studio;
using Honua.Sdk.Studio.Exceptions;
using Honua.Sdk.Studio.Extensions;
using Honua.Sdk.Studio.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Studio.Tests;

public sealed class HonuaStudioReportsClientTests
{
    [Fact]
    public async Task GetReportAsync_DeserializesEnvelopeAndEverySectionKind()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(ReadFixture("analysis-report.v1.json"));
        });
        var client = new HonuaStudioReportsClient(http);

        var report = await client.GetReportAsync("job-7f3c");

        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("/api/v1/analysis/reports/job-7f3c", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(HonuaReportingContract.ContractVersionV1, report.ReportContractVersion);
        Assert.Equal("analytics", report.ProcessFamily);
        Assert.Equal(HonuaNarrativeMode.LlmAssisted, report.NarrativeMode);
        Assert.Equal("Flood buffer impact summary", report.Summary.Title);

        // Every modeled section kind round-trips to its concrete type.
        Assert.Contains(report.Sections, s => s is HonuaHeadingSection { Level: 1 });
        Assert.Contains(report.Sections, s => s is HonuaParagraphSection);
        Assert.Contains(report.Sections, s => s is HonuaKeyMetricSection { Unit: "people" });
        var table = Assert.IsType<HonuaTableSection>(report.Sections.Single(s => s is HonuaTableSection));
        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(3, table.TruncatedRowCount);
        var chart = Assert.IsType<HonuaChartSection>(report.Sections.Single(s => s is HonuaChartSection));
        Assert.Equal(HonuaReportChartKind.Bar, chart.ChartKind);
        Assert.Equal([320d, 185d], chart.Series.Single().Values);
        Assert.Contains(report.Sections, s => s is HonuaMapEmbedSection { MapPackageUri: "honua://map-packages/map-7f3c" });
        Assert.Contains(report.Sections, s => s is HonuaNarrativeSection { Mode: HonuaNarrativeMode.LlmAssisted });
        Assert.Contains(report.Sections, s => s is HonuaProvenanceFooterSection);

        Assert.Equal(2, report.Provenance.Sources.Count);
        Assert.Equal("dataset:flood-extent", report.Provenance.Sources[0].SourceId);
    }

    [Fact]
    public async Task GetReportAsync_ToleratesNewFieldsOnKnownSection()
    {
        // A newer server adds an unmodeled field to a known section kind. The
        // extra field is ignored on read (additive forward-compatibility) and
        // the section still binds to its concrete type.
        const string json = """
            {
              "reportId": "r1", "reportContractVersion": "honua.report.v1",
              "jobId": "j1", "resultPackageId": "rp1", "processId": "p", "processFamily": "f",
              "templateId": "t", "templateVersion": "1.0.0",
              "summary": { "title": "t" },
              "sections": [ { "kind": "heading", "text": "H", "level": 2, "anchor": "intro" } ],
              "narrativeMode": "deterministic",
              "provenance": { "sources": [], "processDefinitions": [] },
              "generatedAt": "2026-05-24T18:00:00Z"
            }
            """;
        using var http = CreateHttpClient(_ => JsonResponse(json));
        var client = new HonuaStudioReportsClient(http);

        var report = await client.GetReportAsync("j1");

        var heading = Assert.IsType<HonuaHeadingSection>(report.Sections.Single());
        Assert.Equal(2, heading.Level);
    }

    [Fact]
    public async Task GetReportAsync_UnmodeledSectionKind_SurfacesContractDrift()
    {
        // An entirely unmodeled section kind within a supported contract version
        // is genuine drift: it must surface loudly as a contract exception, not
        // be silently dropped.
        const string json = """
            {
              "reportId": "r1", "reportContractVersion": "honua.report.v1",
              "jobId": "j1", "resultPackageId": "rp1", "processId": "p", "processFamily": "f",
              "templateId": "t", "templateVersion": "1.0.0",
              "summary": { "title": "t" },
              "sections": [ { "kind": "callout", "text": "new kind" } ],
              "narrativeMode": "deterministic",
              "provenance": { "sources": [], "processDefinitions": [] },
              "generatedAt": "2026-05-24T18:00:00Z"
            }
            """;
        using var http = CreateHttpClient(_ => JsonResponse(json));
        var client = new HonuaStudioReportsClient(http);

        var ex = await Assert.ThrowsAsync<HonuaStudioContractException>(() => client.GetReportAsync("j1"));
        Assert.Equal("GetReport", ex.Operation);
    }

    [Theory]
    [InlineData(HonuaReportRenderFormat.Markdown, "md", "text/markdown")]
    [InlineData(HonuaReportRenderFormat.Html, "html", "text/html")]
    public async Task RenderReportAsync_RequestsFormatAndReturnsBody(
        HonuaReportRenderFormat format,
        string expectedQueryValue,
        string mediaType)
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return TextResponse("# Flood Buffer Impact", mediaType);
        });
        var client = new HonuaStudioReportsClient(http);

        var rendered = await client.RenderReportAsync("job-7f3c", format);

        Assert.Equal($"/api/v1/analysis/reports/job-7f3c/render?format={expectedQueryValue}", captured?.RequestUri?.PathAndQuery);
        Assert.Contains(captured!.Headers.Accept, h => h.MediaType == mediaType);
        Assert.Equal(format, rendered.Format);
        Assert.Equal(mediaType, rendered.MediaType);
        Assert.Equal("job-7f3c", rendered.JobId);
        Assert.Equal("# Flood Buffer Impact", rendered.Content);
    }

    [Fact]
    public async Task GetReportAsync_ProblemResponse_ThrowsApiException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "type": "https://honua.io/problems/not-found",
              "title": "Not Found",
              "status": 404,
              "detail": "No completed report exists for job 'job-missing'."
            }
            """,
            HttpStatusCode.NotFound));
        var client = new HonuaStudioReportsClient(http);

        var ex = await Assert.ThrowsAsync<HonuaStudioApiException>(() => client.GetReportAsync("job-missing"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Not Found", ex.ProblemTitle);
        Assert.Contains("job-missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReportAsync_MalformedSuccessBody_ThrowsContractException()
    {
        using var http = CreateHttpClient(_ => JsonResponse("not-json", HttpStatusCode.OK));
        var client = new HonuaStudioReportsClient(http);

        var ex = await Assert.ThrowsAsync<HonuaStudioContractException>(() => client.GetReportAsync("job-7f3c"));

        Assert.Equal("GetReport", ex.Operation);
    }

    [Fact]
    public async Task GetReportAsync_BlankJobId_Throws()
    {
        using var http = CreateHttpClient(_ => JsonResponse("{}"));
        var client = new HonuaStudioReportsClient(http);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetReportAsync("   "));
    }

    [Fact]
    public void AddHonuaStudio_ConfiguresHttpClientTimeoutAndResolvesClient()
    {
        var timeout = TimeSpan.FromSeconds(42);
        var services = new ServiceCollection();
        services.AddHonuaStudio(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<HonuaStudioReportsClient>(provider.GetRequiredService<IHonuaStudioReportsClient>());
    }

    [Fact]
    public void AddHonuaStudio_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<Honua.Sdk.Abstractions.HonuaConfigurationException>(() =>
            services.AddHonuaStudio(options =>
            {
                options.BaseAddress = new Uri("https://localhost:5001");
                options.Timeout = TimeSpan.FromMilliseconds(10);
            }));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler))
        {
            BaseAddress = new Uri("https://server.example")
        };

    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => Task.FromResult(CreateJsonResponse(json, statusCode));

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return response;
    }

    private static Task<HttpResponseMessage> TextResponse(string text, string mediaType)
        => Task.FromResult(CreateTextResponse(text, mediaType));

    private static HttpResponseMessage CreateTextResponse(string text, string mediaType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(text, Encoding.UTF8)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = "utf-8" };
        return response;
    }

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Join(FindRepoRoot(), "contracts", "fixtures", "console", name));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Honua.Sdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the honua-sdk-dotnet repository root.");
    }
}
