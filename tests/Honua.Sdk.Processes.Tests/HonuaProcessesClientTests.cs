// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Processes.Exceptions;
using Honua.Sdk.Processes.Extensions;
using Honua.Sdk.Processes.Models;
using Honua.Sdk.Processes.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Processes.Tests;

public sealed class HonuaProcessesClientTests
{
    private const string CanonicalProcessId = "honua-geoprocessing";

    [Fact]
    public async Task AuthHandler_UsesCredentialProvidersPerRequest()
    {
        var apiKeyCalls = 0;
        var bearerTokenCalls = 0;
        var capturedCredentials = new List<(string? ApiKey, string? Authorization)>();

        var options = Options.Create(new HonuaProcessesClientOptions
        {
            BaseAddress = new Uri("https://localhost:5001"),
            ApiKeyProvider = _ => Task.FromResult<string?>($"process-key-{++apiKeyCalls}"),
            BearerTokenProvider = _ => Task.FromResult<string?>($"process-token-{++bearerTokenCalls}")
        });

        var authHandler = new HonuaProcessesAuthHandler(options)
        {
            InnerHandler = new MockHttpHandler(request =>
            {
                request.Headers.TryGetValues("X-API-Key", out var apiValues);
                capturedCredentials.Add((apiValues?.SingleOrDefault(), request.Headers.Authorization?.ToString()));
                return JsonResponse(ProcessListJson);
            })
        };

        using var http = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("https://localhost:5001")
        };
        var client = new HonuaProcessesClient(http);

        await client.ListProcessesAsync();
        await client.ListProcessesAsync();

        Assert.Collection(
            capturedCredentials,
            first =>
            {
                Assert.Equal("process-key-1", first.ApiKey);
                Assert.Equal("Bearer process-token-1", first.Authorization);
            },
            second =>
            {
                Assert.Equal("process-key-2", second.ApiKey);
                Assert.Equal("Bearer process-token-2", second.Authorization);
            });
    }

    [Fact]
    public async Task AuthHandler_RejectsCredentialProvidersOverRemoteHttp()
    {
        var providerCalled = false;
        var options = Options.Create(new HonuaProcessesClientOptions
        {
            BaseAddress = new Uri("http://example.com"),
            ApiKeyProvider = _ =>
            {
                providerCalled = true;
                return Task.FromResult<string?>("process-key");
            }
        });

        var authHandler = new HonuaProcessesAuthHandler(options)
        {
            InnerHandler = new MockHttpHandler(_ => JsonResponse(ProcessListJson))
        };

        using var http = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://example.com")
        };
        var client = new HonuaProcessesClient(http);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListProcessesAsync());
        Assert.False(providerCalled);
    }

    [Fact]
    public async Task SubmitJobAsync_PostsExecutionRequestWithAsyncPreference()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var http = CreateHttpClient(async request =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
            return await JsonResponse(JobStatusJson).ConfigureAwait(false);
        });
        var client = new HonuaProcessesClient(http);

        var result = await client.SubmitJobAsync(CanonicalProcessId, CreateExecuteRequest());

        Assert.Equal("job-1", result.JobId);
        Assert.Equal("accepted", result.Status);
        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("/ogc/processes/processes/honua-geoprocessing/execution", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("respond-async", captured?.Headers.GetValues("Prefer").Single());
        Assert.Contains("\"planId\":\"plan-1\"", body, StringComparison.Ordinal);
        Assert.Contains("\"workflowFamily\":\"analyze\"", body, StringComparison.Ordinal);
        Assert.Contains("\"outputs\":[\"featureLayer\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"geoprocess\"", body, StringComparison.Ordinal);
        Assert.Contains("\"processId\":\"geometry.buffer\"", body, StringComparison.Ordinal);
        Assert.Contains("\"wkb\":\"AAAA\"", body, StringComparison.Ordinal);
        Assert.Contains("\"srid\":\"4326\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitJobAsync_PostsDirectProcessInputsWithoutPlanWrapper()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var http = CreateHttpClient(async request =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
            return await JsonResponse(JobStatusJson).ConfigureAwait(false);
        });
        var client = new HonuaProcessesClient(http);

        var result = await client.SubmitJobAsync(
            "geometry.buffer",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["wkb"] = JsonSerializer.SerializeToElement("AAAA"),
                ["srid"] = JsonSerializer.SerializeToElement(4326),
                ["distance"] = JsonSerializer.SerializeToElement(25.5)
            });

        Assert.Equal("job-1", result.JobId);
        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("/ogc/processes/processes/geometry.buffer/execution", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("respond-async", captured?.Headers.GetValues("Prefer").Single());
        using var document = JsonDocument.Parse(body ?? throw new InvalidOperationException("Request body was not captured."));
        var root = document.RootElement;
        var inputs = root.GetProperty("inputs");
        Assert.False(inputs.TryGetProperty("plan", out _));
        Assert.Equal("AAAA", inputs.GetProperty("wkb").GetString());
        Assert.Equal(4326, inputs.GetProperty("srid").GetInt32());
        Assert.Equal(25.5, inputs.GetProperty("distance").GetDouble());
        Assert.Equal("document", root.GetProperty("response").GetString());
    }

    [Fact]
    public async Task JobLifecycleMethods_UseCanonicalOgcProcessesRoutes()
    {
        var paths = new List<(HttpMethod Method, string Path)>();
        using var http = CreateHttpClient(request =>
        {
            paths.Add((request.Method, request.RequestUri?.PathAndQuery ?? string.Empty));
            var response = request.RequestUri?.PathAndQuery switch
            {
                "/ogc/processes/jobs?limit=5" => JsonResponse(JobListJson),
                "/ogc/processes/jobs/job-1" when request.Method == HttpMethod.Get => JsonResponse(JobStatusJson),
                "/ogc/processes/jobs/job-1/results" => JsonResponse(JobResultsJson),
                "/ogc/processes/jobs/job-1" when request.Method == HttpMethod.Delete => JsonResponse(CancelledJobStatusJson),
                _ => JsonResponse("""{"title":"unexpected route","detail":"Route was not handled."}""", HttpStatusCode.NotFound)
            };
            return response;
        });
        var client = new HonuaProcessesClient(http);

        var jobs = await client.ListJobsAsync(5);
        var status = await client.GetJobAsync("job-1");
        var results = await client.GetJobResultsAsync("job-1");
        var dismissed = await client.DismissJobAsync("job-1");

        Assert.Single(jobs.Jobs);
        Assert.Equal("job-1", status.JobId);
        Assert.True(results.Outputs.ContainsKey("summary"));
        Assert.Equal("dismissed", dismissed.Status);
        Assert.Equal(
            [
                (HttpMethod.Get, "/ogc/processes/jobs?limit=5"),
                (HttpMethod.Get, "/ogc/processes/jobs/job-1"),
                (HttpMethod.Get, "/ogc/processes/jobs/job-1/results"),
                (HttpMethod.Delete, "/ogc/processes/jobs/job-1")
            ],
            paths);
    }

    [Fact]
    public async Task ErrorResponses_ThrowStructuredProblem()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "type": "https://honua.io/problems/process-plan-invalid",
              "title": "Invalid process plan",
              "detail": "Plan step buffer has no input.",
              "status": 400
            }
            """,
            HttpStatusCode.BadRequest));
        var client = new HonuaProcessesClient(http);

        var ex = await Assert.ThrowsAsync<HonuaProcessesException>(() =>
            client.SubmitJobAsync(CanonicalProcessId, CreateExecuteRequest()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Invalid process plan", ex.ProblemTitle);
        Assert.Equal("Plan step buffer has no input.", ex.ProblemDetail);
        Assert.Contains("Plan step", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHonuaProcesses_ConfiguresHttpClientTimeoutAndOptions()
    {
        var timeout = TimeSpan.FromSeconds(42);
        var services = new ServiceCollection();
        services.AddHonuaProcesses(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = Assert.IsType<HonuaProcessesClient>(provider.GetRequiredService<IHonuaProcessesClient>());

        Assert.Equal(timeout, GetHttpClient(client).Timeout);
    }

    [Fact]
    public void AddHonuaProcesses_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<Honua.Sdk.Abstractions.HonuaConfigurationException>(() =>
            services.AddHonuaProcesses(options =>
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

    private static HonuaProcessExecuteRequest CreateExecuteRequest()
        => new()
        {
            Inputs = HonuaProcessExecuteInputs.FromPlan(
                new HonuaAnalysisPlan
                {
                    PlanId = "plan-1",
                    SpecVersion = "spec/v1",
                    WorkflowFamily = "analyze",
                    Outputs = ["featureLayer"],
                    Steps =
                    [
                        new HonuaPlanStep
                        {
                            StepId = "buffer",
                            Kind = "geoprocess",
                            ProcessId = "geometry.buffer",
                            Inputs = new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["wkb"] = "AAAA",
                                ["srid"] = "4326",
                                ["distance"] = "25"
                            }
                        }
                    ]
                })
        };

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership transfers to the HttpClient pipeline, which disposes the response.")]
    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }

    private static HttpClient GetHttpClient(object client)
    {
        var field = client.GetType().GetField("_http", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HttpClient>(value);
    }

    private const string ProcessListJson = """
        {
          "processes": [
            {
              "id": "honua-geoprocessing",
              "title": "Analysis Plan",
              "version": "1.0.0",
              "jobControlOptions": ["async-execute"],
              "outputTransmission": ["value"],
              "links": []
            }
          ],
          "links": []
        }
        """;

    private const string JobStatusJson = """
        {
          "processID": "honua-geoprocessing",
          "type": "process",
          "jobID": "job-1",
          "status": "accepted",
          "progress": 0,
          "links": []
        }
        """;

    private const string CancelledJobStatusJson = """
        {
          "processID": "honua-geoprocessing",
          "type": "process",
          "jobID": "job-1",
          "status": "dismissed",
          "progress": 0,
          "links": []
        }
        """;

    private const string JobListJson = """
        {
          "jobs": [
            {
              "processID": "honua-geoprocessing",
              "type": "process",
              "jobID": "job-1",
              "status": "running",
              "progress": 50,
              "links": []
            }
          ],
          "links": []
        }
        """;

    private const string JobResultsJson = """
        {
          "summary": {
            "value": "Analysis complete."
          }
        }
        """;
}
