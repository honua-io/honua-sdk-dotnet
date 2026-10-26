// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Abstractions.Authentication;
using Honua.Sdk.GeoServices.FeatureServer;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

/// <summary>
/// Transport guards on <see cref="ArcGisSourceCredentialHandler"/>: the plain-HTTP credential guard,
/// redirect suppression on the default transport, and binding the credential to the source origin.
/// </summary>
public sealed class ArcGisSourceCredentialHandlerSecurityTests
{
    private static readonly Uri HttpsSource = new("https://source.example.com/arcgis/rest/services/");

    public static TheoryData<ArcGisSourceCredential> Credentials() => new()
    {
        new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Token, Token = "abc123" },
        new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Basic, Username = "gis-user", Password = "hunter2" },
        new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Bearer, BearerToken = "xyz789" },
    };

    [Theory]
    [MemberData(nameof(Credentials))]
    public async Task PlainHttpNonLoopbackRequest_WithCredential_Throws(ArcGisSourceCredential credential)
    {
        var inner = new CapturingHandler();
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler);

        // Same type and message as HonuaAuthHandler<TOptions> for the same send-time refusal.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => http.GetAsync(new Uri("http://source.example.com/arcgis/rest/services/Parcels/FeatureServer/0?f=json")));

        Assert.Equal(HonuaAuthenticationSupport.InsecureTransportRejectedMessage, exception.Message);
        Assert.Null(inner.Captured);
    }

    [Theory]
    [MemberData(nameof(Credentials))]
    public void PlainHttpNonLoopbackSourceBaseAddress_WithCredential_ThrowsAtConstruction(ArcGisSourceCredential credential)
    {
        var bound = credential with { SourceBaseAddress = new Uri("http://source.example.com/arcgis/") };

        var exception = Assert.Throws<HonuaConfigurationException>(() => new ArcGisSourceCredentialHandler(bound, new CapturingHandler()));

        Assert.Equal(HonuaAuthenticationSupport.InsecureTransportRejectedMessage, exception.Message);
    }

    [Fact]
    public async Task PlainHttpNonLoopbackRequest_WithoutCredential_IsAllowed()
    {
        var inner = new CapturingHandler();
        using var handler = new ArcGisSourceCredentialHandler(new ArcGisSourceCredential { Mode = ArcGisCredentialMode.None }, inner);
        using var http = new HttpClient(handler);

        using var response = await http.GetAsync(new Uri("http://source.example.com/arcgis/rest/services?f=json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(inner.Captured!.Headers.Authorization);
    }

    [Theory]
    [InlineData("http://localhost:6080/arcgis/rest/services/Parcels/FeatureServer/0?f=json")]
    [InlineData("http://127.0.0.1:6080/arcgis/rest/services/Parcels/FeatureServer/0?f=json")]
    [InlineData("http://[::1]:6080/arcgis/rest/services/Parcels/FeatureServer/0?f=json")]
    public async Task PlainHttpLoopbackRequest_WithCredential_IsAllowed(string address)
    {
        var inner = new CapturingHandler();
        var credential = new ArcGisSourceCredential
        {
            Mode = ArcGisCredentialMode.Token,
            Token = "abc123",
            SourceBaseAddress = new Uri(address),
        };
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler);

        await http.GetAsync(new Uri(address));

        Assert.Contains("token=abc123", inner.Captured!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://attacker.example/arcgis/rest/services/Parcels/FeatureServer/0?f=json")]
    [InlineData("http://source.example.com/arcgis/rest/services/Parcels/FeatureServer/0?f=json")]
    [InlineData("https://source.example.com:8443/arcgis/rest/services/Parcels/FeatureServer/0?f=json")]
    [InlineData("https://sub.source.example.com/arcgis/rest/services/Parcels/FeatureServer/0?f=json")]
    public async Task DifferentOriginRequest_DoesNotCarryCredential(string address)
    {
        foreach (var credential in new[]
        {
            new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Token, Token = "abc123", SourceBaseAddress = HttpsSource },
            new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Basic, Username = "u", Password = "p", SourceBaseAddress = HttpsSource },
            new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Bearer, BearerToken = "xyz789", SourceBaseAddress = HttpsSource },
        })
        {
            var inner = new CapturingHandler();
            using var handler = new ArcGisSourceCredentialHandler(credential, inner);
            using var http = new HttpClient(handler);

            await http.GetAsync(new Uri(address));

            Assert.DoesNotContain("token=", inner.Captured!.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Null(inner.Captured.Headers.Authorization);
        }
    }

    [Theory]
    [MemberData(nameof(Credentials))]
    public async Task WithoutSourceBaseAddress_RejectedPlainHttpRequest_DoesNotBindOrigin(ArcGisSourceCredential credential)
    {
        var inner = new CapturingHandler();
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => http.GetAsync(new Uri("http://source.example.com/arcgis/rest/services?f=json")));
        Assert.Null(inner.Captured);

        // The HTTPS retry the refusal asks for must still carry the credential.
        await http.GetAsync(new Uri("https://source.example.com/arcgis/rest/services?f=json"));
        var captured = inner.Captured!;
        Assert.True(
            captured.Headers.Authorization is not null ||
            captured.RequestUri!.Query.Contains("token=abc123", StringComparison.Ordinal),
            "the HTTPS retry must carry the credential");
    }

    [Fact]
    public async Task WithoutSourceBaseAddress_CredentialIsBoundToFirstRequestOrigin()
    {
        var inner = new CapturingHandler();
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Bearer, BearerToken = "xyz789" };
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler);

        await http.GetAsync(new Uri("https://source.example.com/arcgis/rest/services?f=json"));
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "xyz789"), inner.Captured!.Headers.Authorization);

        await http.GetAsync(new Uri("https://attacker.example/steal"));
        Assert.Null(inner.Captured!.Headers.Authorization);

        await http.GetAsync(new Uri("https://source.example.com/arcgis/rest/services/Parcels/FeatureServer?f=json"));
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "xyz789"), inner.Captured!.Headers.Authorization);
    }

    [Fact]
    public async Task SameOriginHttpsRequest_GetsToken()
    {
        var inner = new CapturingHandler();
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Token, Token = "a b&c", SourceBaseAddress = HttpsSource };
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler) { BaseAddress = HttpsSource };

        await http.GetAsync(new Uri("Parcels/FeatureServer/0?f=json", UriKind.Relative));

        Assert.Equal("https://source.example.com/arcgis/rest/services/Parcels/FeatureServer/0?f=json&token=a%20b%26c", inner.Captured!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task SameOriginHttpsRequest_GetsBasicAndBearerHeaders()
    {
        var basicInner = new CapturingHandler();
        using (var handler = new ArcGisSourceCredentialHandler(
            new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Basic, Username = "gis-user", Password = "hunter2", SourceBaseAddress = HttpsSource },
            basicInner))
        using (var http = new HttpClient(handler))
        {
            await http.GetAsync(new Uri("https://source.example.com:443/arcgis/rest/services?f=json"));
        }

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("gis-user:hunter2"));
        Assert.Equal(new AuthenticationHeaderValue("Basic", expected), basicInner.Captured!.Headers.Authorization);

        var bearerInner = new CapturingHandler();
        using (var handler = new ArcGisSourceCredentialHandler(
            new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Bearer, BearerToken = "xyz789", SourceBaseAddress = HttpsSource },
            bearerInner))
        using (var http = new HttpClient(handler))
        {
            await http.GetAsync(new Uri("https://SOURCE.example.com/arcgis/rest/services?f=json"));
        }

        Assert.Equal(new AuthenticationHeaderValue("Bearer", "xyz789"), bearerInner.Captured!.Headers.Authorization);
    }

    [Fact]
    public void InvalidSourceBaseAddress_Throws()
    {
        var credential = new ArcGisSourceCredential
        {
            Mode = ArcGisCredentialMode.Token,
            Token = "abc123",
            SourceBaseAddress = new Uri("ftp://source.example.com/"),
        };

        Assert.Throws<HonuaConfigurationException>(() => new ArcGisSourceCredentialHandler(credential, new CapturingHandler()));
    }

    [Fact]
    public async Task DefaultTransport_DoesNotFollowRedirect()
    {
        // Real loopback sockets: the source answers 302 to a second listener. With redirects disabled
        // the 302 surfaces to the caller and the redirect target is never contacted.
        using var target = new TcpListener(IPAddress.Loopback, 0);
        target.Start();
        var targetPort = ((IPEndPoint)target.LocalEndpoint).Port;
        using var source = new TcpListener(IPAddress.Loopback, 0);
        source.Start();
        var sourcePort = ((IPEndPoint)source.LocalEndpoint).Port;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // CA2025: serverTask is awaited below, before the listener and token source are disposed.
#pragma warning disable CA2025
        var serverTask = ServeSingleRedirectAsync(source, $"http://127.0.0.1:{targetPort}/collect", cts.Token);
#pragma warning restore CA2025

        var credential = new ArcGisSourceCredential
        {
            Mode = ArcGisCredentialMode.Token,
            Token = "abc123",
            SourceBaseAddress = new Uri($"http://127.0.0.1:{sourcePort}/"),
        };
        using var handler = new ArcGisSourceCredentialHandler(credential);
        using var http = new HttpClient(handler);

        using var response = await http.GetAsync(new Uri($"http://127.0.0.1:{sourcePort}/arcgis/rest/services?f=json"), cts.Token);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var requestLine = await serverTask;
        Assert.Contains("token=abc123", requestLine, StringComparison.Ordinal);
        Assert.False(target.Pending(), "redirect target must not be contacted");
    }

    private static async Task<string> ServeSingleRedirectAsync(TcpListener listener, string location, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
            // Discard the remaining request headers; only the request line is asserted.
        }

        var response = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 302 Found\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return requestLine;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly List<HttpResponseMessage> _responses = [];

        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            _responses.Add(response);
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var response in _responses)
                {
                    response.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
