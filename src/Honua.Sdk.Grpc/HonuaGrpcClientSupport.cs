// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.Grpc;

internal static class HonuaGrpcClientSupport
{
    public static ServiceConfig BuildServiceConfig(
        HonuaGrpcClientOptions options,
        string serviceName,
        IReadOnlyList<string> retryableMethods)
    {
        var serviceConfig = new ServiceConfig();

        if (options.EnableRetry && retryableMethods.Count > 0)
        {
            var methodConfig = new MethodConfig
            {
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = options.MaxRetryAttempts,
                    InitialBackoff = TimeSpan.FromMilliseconds(500),
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes =
                    {
                        StatusCode.Unavailable,
                        StatusCode.Internal
                    }
                }
            };

            foreach (var method in retryableMethods)
            {
                methodConfig.Names.Add(new MethodName { Service = serviceName, Method = method });
            }

            serviceConfig.MethodConfigs.Add(methodConfig);
        }

        return serviceConfig;
    }

    public static async Task<Metadata> BuildMetadataAsync(
        HonuaGrpcClientOptions options,
        string providerName,
        string methodName,
        Metadata? metadataOverride,
        CancellationToken cancellationToken)
    {
        if (metadataOverride is not null)
        {
            return metadataOverride;
        }

        var metadata = new Metadata();
        var context = HonuaAuthenticationSupport.CreateGrpcRequest(options, providerName, methodName);
        var apiKey = await HonuaAuthenticationSupport.ResolveApiKeyAsync(options, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(apiKey))
        {
            metadata.Add("x-api-key", apiKey);
        }

        var accessToken = await HonuaAuthenticationSupport.ResolveAccessTokenAsync(
            options,
            context,
            cancellationToken).ConfigureAwait(false);
        if (accessToken is not null && !string.IsNullOrWhiteSpace(accessToken.Token))
        {
            var tokenType = string.IsNullOrWhiteSpace(accessToken.TokenType) ? "Bearer" : accessToken.TokenType;
            metadata.Add("authorization", $"{tokenType} {accessToken.Token}");
        }

        if (options.EnableCompressionNegotiation && !string.IsNullOrWhiteSpace(options.AcceptedCompressionEncodings))
        {
            metadata.Add("grpc-accept-encoding", options.AcceptedCompressionEncodings);
        }

        await HonuaAuthenticationSupport.EmitCredentialAppliedDiagnosticAsync(
            options,
            context,
            hasApiKey: !string.IsNullOrWhiteSpace(apiKey),
            authorizationScheme: accessToken?.TokenType,
            hasAuthorization: accessToken is not null && !string.IsNullOrWhiteSpace(accessToken.Token),
            cancellationToken).ConfigureAwait(false);

        return metadata;
    }

    public static DateTime CreateDeadline(HonuaGrpcClientOptions options)
        => DateTime.UtcNow.Add(options.Timeout);

    public static void ValidateAuthenticationTransport(HonuaGrpcClientOptions options, Uri address)
    {
        if (!HasCredentials(options))
        {
            return;
        }

        if (HonuaGrpcClientOptions.RequiresHttpsForAuthentication(address))
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException(
                "Refusing to send gRPC credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }
    }

    public static bool HasCredentials(HonuaGrpcClientOptions options)
        => HonuaAuthenticationSupport.HasCredentialSource(options);

    public static Uri ResolveChannelAddress(GrpcChannel channel)
    {
        if (Uri.TryCreate(channel.Target, UriKind.Absolute, out var targetAddress) &&
            IsHttpOrHttps(targetAddress))
        {
            return targetAddress;
        }

        var originalAddress = typeof(GrpcChannel)
            .GetProperty("Address", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(channel) as Uri;

        if (originalAddress is not null && IsHttpOrHttps(originalAddress))
        {
            return originalAddress;
        }

        throw new Honua.Sdk.Abstractions.HonuaConfigurationException(
            "Honua gRPC preconfigured channel target must expose an HTTP or HTTPS address when credentials are configured.");
    }

    private static bool IsHttpOrHttps(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
