// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Collections.Concurrent;
using System.Net;
using Honua.Sdk.OgcFeatures.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.OgcFeatures.Tests;

/// <summary>
/// Regression coverage for issue #214: the custom <c>X-API-Key</c> header must
/// never be forwarded to a cross-origin redirect target. The default primary
/// HTTP handler disables auto-redirect, so a <c>30x</c> response surfaces to the
/// caller instead of being silently followed with credentials attached.
/// </summary>
public sealed class RedirectApiKeyLeakTests
{
    [Fact]
    public void DefaultPrimaryHandler_DisablesAutoRedirect()
    {
        using var handler = Honua.Sdk.Abstractions.HonuaHttpHandlerDefaults.CreateNoRedirectPrimaryHandler();

        var socketsHandler = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(socketsHandler.AllowAutoRedirect);
    }

    [Fact]
    public async Task ConfiguredClient_DoesNotFollowRedirect_AndDoesNotLeakApiKey()
    {
        // "Attacker" origin records every header it receives.
        var receivedApiKeys = new ConcurrentBag<string?>();
        using var attacker = new LoopbackServer(context =>
        {
            receivedApiKeys.Add(context.Request.Headers["X-API-Key"]);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
        });

        // Legit origin issues a cross-origin 302 to the attacker.
        using var legit = new LoopbackServer(context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.Found;
            context.Response.Headers["Location"] = attacker.BaseAddress.ToString();
        });

        var services = new ServiceCollection();
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = legit.BaseAddress;
            options.ApiKey = "super-secret-key";
            // Keep the test deterministic — no retry pipeline wrapping the response.
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        // The typed client registration uses the client type name as the logical name.
        using var client = factory.CreateClient(nameof(HonuaOgcFeaturesClient));
        client.BaseAddress = legit.BaseAddress;

        using var response = await client.GetAsync("collections");

        // The redirect is surfaced, NOT auto-followed.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        // The attacker origin must never have been contacted, and certainly must
        // never have received the API key.
        Assert.Empty(receivedApiKeys);
    }

    private sealed class LoopbackServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public LoopbackServer(Action<HttpListenerContext> handle)
        {
            // Bind to an ephemeral loopback port. Loopback HTTP is permitted by
            // the auth handler, so the X-API-Key header is attached on send.
            var port = GetFreePort();
            BaseAddress = new Uri($"http://127.0.0.1:{port}/");
            _listener = new HttpListener();
            _listener.Prefixes.Add(BaseAddress.ToString());
            _listener.Start();
            _loop = Task.Run(() => AcceptLoop(handle, _cts.Token));
        }

        public Uri BaseAddress { get; }

        private async Task AcceptLoop(Action<HttpListenerContext> handle, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                try
                {
                    handle(context);
                }
                finally
                {
                    context.Response.Close();
                }
            }
        }

        private static int GetFreePort()
        {
            using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                _loop.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                // Best-effort shutdown.
            }

            _listener.Close();
            _cts.Dispose();
        }
    }
}
