// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Shared GeoServices HTTP helpers: form-POST, GET, and the GeoServices
/// <c>{ error: { code, message, details[] } }</c> envelope handling used by all
/// GeoServices clients (FeatureServer, NAServer routing, ImageServer, GeometryServer).
/// </summary>
internal static class GeoServicesHttp
{
    internal static async Task<string> GetStringAsync(
        HttpClient http, string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    internal static async Task<string> PostFormAsync(
        HttpClient http,
        string path,
        IEnumerable<(string Key, string? Value)> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!)));

        using var response = await http.PostAsync(CreateRequestUri(path), content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    internal static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    internal static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            if (!string.IsNullOrWhiteSpace(body) && TryExtractGeoServicesError(body, response) is { } error)
            {
                throw error;
            }

            return;
        }

        var errorMessage = TryExtractErrorMessage(body) ?? response.ReasonPhrase ?? "GeoServices request failed";
        throw new HonuaFeatureServerException(
            response.StatusCode,
            errorMessage,
            body,
            null,
            null,
            failureReceipt: Honua.Sdk.Abstractions.HonuaFailureReceiptFactory.FromHttpResponse(response, body));
    }

    internal static HonuaFeatureServerException? TryExtractGeoServicesError(string body, HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var errorElement) ||
                errorElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var message = "GeoServices returned an error.";
            int? geoServicesCode = null;
            IReadOnlyList<string>? details = null;
            var httpCode = response.StatusCode;
            if (errorElement.TryGetProperty("message", out var msgProp) &&
                msgProp.ValueKind == JsonValueKind.String)
            {
                message = msgProp.GetString() ?? message;
            }

            if (errorElement.TryGetProperty("code", out var codeProp) &&
                codeProp.TryGetInt32(out var errorCode))
            {
                geoServicesCode = errorCode;
                httpCode = MapErrorCodeToStatus(errorCode, response.StatusCode);
            }

            if (errorElement.TryGetProperty("details", out var detailsProp) &&
                detailsProp.ValueKind == JsonValueKind.Array)
            {
                var detailList = new List<string>();
                foreach (var item in detailsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        detailList.Add(item.GetString()!);
                    }
                }

                details = detailList;
            }

            return new HonuaFeatureServerException(
                httpCode,
                message,
                body,
                geoServicesCode,
                details,
                Honua.Sdk.Abstractions.HonuaFailureReceiptFactory.FromHttpResponse(response, body, geoServicesCode));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps a GeoServices <c>error.code</c> to an <see cref="HttpStatusCode"/> only when it is a
    /// valid HTTP status (100-599). GeoServices codes are an independent code space (e.g. 1000,
    /// 4001) and must not be blindly cast — doing so produces nonsensical <see cref="HttpStatusCode"/>
    /// values that break consumers branching on status for retry/auth. Out-of-range codes keep the
    /// transport status; the Esri code is still exposed via <c>GeoServicesErrorCode</c>.
    /// </summary>
    internal static HttpStatusCode MapErrorCodeToStatus(int errorCode, HttpStatusCode transportStatus)
        => errorCode is >= 100 and <= 599 ? (HttpStatusCode)errorCode : transportStatus;

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.Object &&
                errorElement.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }

            if (doc.RootElement.TryGetProperty("message", out var topMsg) &&
                topMsg.ValueKind == JsonValueKind.String)
            {
                return topMsg.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
