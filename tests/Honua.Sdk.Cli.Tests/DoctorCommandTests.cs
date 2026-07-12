using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Cli;

namespace Honua.Sdk.Cli.Tests;

public sealed class DoctorCommandTests
{
    [Fact]
    public async Task Doctor_RawExchange_EmitsSchemaValidBundleWithoutSecretsOrNulls()
    {
        using TemporaryDirectory temporary = new();
        string capture = Path.Combine(temporary.Path, "capture.json");
        string output = Path.Combine(temporary.Path, "bundle.json");
        File.WriteAllText(capture, """
            {
              "request": {
                "method": "GET",
                "url": "https://alice:password@example.test/api/items/123456?token=raw-query-token",
                "headers": {
                  "authorization": "Bearer raw-auth",
                  "cookie": "raw-cookie",
                  "x-request-id": "req-1"
                }
              },
              "response": {
                "status": 500,
                "mediaType": "application/json",
                "headers": { "content-type": "application/json; boundary=raw-boundary-secret", "set-cookie": "raw-set-cookie" },
                "body": { "message": "failed for person@example.test", "apiKey": "raw-api-key" }
              }
            }
            """);
        using HttpClient client = new(new RecordingHandler(_ => throw new InvalidOperationException()));
        StringWriter stdout = new();
        StringWriter stderr = new();

        int exitCode = await HonuaCli.RunAsync(
            [
                "doctor", "--exchange", capture, "--classification", "customer-data",
                "--redaction-acknowledged=true", "--share-with-support=false",
                "--output", output, "--json"
            ],
            stdout,
            stderr,
            client);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        string artifact = File.ReadAllText(output);
        using JsonDocument document = JsonDocument.Parse(artifact);
        Assert.Empty(DiagnosticSchema.Instance.Validate(document.RootElement));
        Assert.False(document.RootElement.GetProperty("consent").GetProperty("shareWithSupport").GetBoolean());
        Assert.DoesNotContain(": null", artifact, StringComparison.Ordinal);
        foreach (string secret in new[]
        {
            "raw-query-token", "raw-auth", "raw-cookie", "raw-set-cookie", "raw-boundary-secret",
            "raw-api-key", "person@example.test"
        })
        {
            Assert.DoesNotContain(secret, artifact, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, stdout.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(secret, stderr.ToString(), StringComparison.Ordinal);
        }

        JsonElement envelope = document.RootElement.GetProperty("envelopes")[0];
        Assert.Equal("/api/items/{value}", envelope.GetProperty("normalizedPath").GetString());
        Assert.Equal("req-1", envelope.GetProperty("requestHeaders")[0].GetProperty("value").GetString());
        Assert.Equal(64, envelope.GetProperty("responseBody").GetProperty("contentSha256").GetString()!.Length);
    }

    [Theory]
    [InlineData("https://example.test/a/../admin")]
    [InlineData("https://example.test/a/%2e%2e/admin")]
    [InlineData("file:///etc/passwd")]
    public void Normalizer_TraversalAndNonHttpUrls_AreRejected(string value)
    {
        Assert.Throws<DiagnosticSafetyException>(() => DiagnosticSanitizer.NormalizePath(value));
    }

    [Fact]
    public async Task Doctor_CapabilityProbe_IsAnonymousBoundedAndPreservesBasePath()
    {
        using TemporaryDirectory temporary = new();
        string output = Path.Combine(temporary.Path, "bundle.json");
        RecordingHandler handler = new(request =>
        {
            Assert.Equal("https://example.test/honua/api/v1/services?limit=1", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"serverVersion\":\"1.2.3\",\"secret\":\"raw-secret\"}", Encoding.UTF8, "application/json")
            };
            response.Headers.Add("x-request-id", "request-1");
            response.Headers.Add("set-cookie", "raw-cookie");
            return response;
        });
        using HttpClient client = new(handler);

        int exitCode = await HonuaCli.RunAsync(
            [
                "doctor", "--base-url", "https://example.test/honua", "--classification", "internal",
                "--redaction-acknowledged=true", "--share-with-support=false", "--output", output, "--json"
            ],
            TextWriter.Null,
            TextWriter.Null,
            client);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, handler.CallCount);
        string artifact = File.ReadAllText(output);
        Assert.DoesNotContain("raw-secret", artifact, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-cookie", artifact, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(artifact);
        Assert.Empty(DiagnosticSchema.Instance.Validate(document.RootElement));
        Assert.Equal("/honua/api/v1/services?limit={value}",
            document.RootElement.GetProperty("envelopes")[0].GetProperty("normalizedPath").GetString());
    }

    [Fact]
    public async Task Doctor_MissingConsent_FailsWithoutArtifactOrSecretEcho()
    {
        using TemporaryDirectory temporary = new();
        string capture = Path.Combine(temporary.Path, "capture.json");
        string output = Path.Combine(temporary.Path, "bundle.json");
        File.WriteAllText(capture, "{\"request\":{\"method\":\"GET\",\"url\":\"https://example.test/api?token=raw-secret\"}}");
        StringWriter stdout = new();
        StringWriter stderr = new();
        using HttpClient client = new(new RecordingHandler(_ => throw new InvalidOperationException()));

        int exitCode = await HonuaCli.RunAsync(
            [
                "doctor", "--exchange", capture, "--classification", "internal",
                "--redaction-acknowledged=true", "--output", output
            ],
            stdout,
            stderr,
            client);

        Assert.Equal(2, exitCode);
        Assert.False(File.Exists(output));
        Assert.DoesNotContain("raw-secret", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("raw-secret", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replay_Get_StripsQueryAndCapturedHeadersAndEmitsNewValidBundle()
    {
        using TemporaryDirectory temporary = new();
        string source = Path.Combine(temporary.Path, "source.json");
        string output = Path.Combine(temporary.Path, "replay.json");
        File.WriteAllText(source, """
            {
              "schemaVersion": "1.0",
              "contentClassification": "public",
              "consent": { "redactionAcknowledged": true, "shareWithSupport": true },
              "envelopes": [{ "method": "GET", "normalizedPath": "/api/v1/services?limit={value}" }]
            }
            """);
        RecordingHandler handler = new(request =>
        {
            Assert.Equal("https://new.example.test/honua/api/v1/services", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"services\":[]}", Encoding.UTF8, "application/json")
            };
        });
        using HttpClient client = new(handler);

        int exitCode = await HonuaCli.RunAsync(
            ["doctor", "--replay", source, "--base-url", "https://new.example.test/honua", "--output", output],
            TextWriter.Null,
            TextWriter.Null,
            client);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, handler.CallCount);
        using JsonDocument replay = JsonDocument.Parse(File.ReadAllBytes(output));
        Assert.Empty(DiagnosticSchema.Instance.Validate(replay.RootElement));
    }

    [Fact]
    public async Task Replay_Mutation_IsRejectedBeforeNetwork()
    {
        using TemporaryDirectory temporary = new();
        string source = Path.Combine(temporary.Path, "source.json");
        string output = Path.Combine(temporary.Path, "replay.json");
        File.WriteAllText(source, """
            {
              "schemaVersion": "1.0",
              "contentClassification": "internal",
              "consent": { "redactionAcknowledged": true, "shareWithSupport": true },
              "envelopes": [{ "method": "POST", "normalizedPath": "/api/v1/applyEdits" }]
            }
            """);
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using HttpClient client = new(handler);

        int exitCode = await HonuaCli.RunAsync(
            ["doctor", "--replay", source, "--base-url", "https://example.test", "--output", output],
            TextWriter.Null,
            TextWriter.Null,
            client);

        Assert.Equal(1, exitCode);
        Assert.Equal(0, handler.CallCount);
        Assert.False(File.Exists(output));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        internal int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response(request));
        }
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "honua-doctor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
