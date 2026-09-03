// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Admin.Exceptions;

namespace Honua.Sdk.Admin;

internal static class AdminHttpHelper
{
    public static Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string body,
        string fallbackMessage,
        bool inspectGeoServicesErrorEnvelope = false)
    {
        if (response.IsSuccessStatusCode)
        {
            if (inspectGeoServicesErrorEnvelope)
            {
                EnsureGeoServicesEnvelopeSucceeded(response, body);
            }

            return Task.CompletedTask;
        }

        var message = TryExtractErrorMessage(body, preferGeoServicesError: inspectGeoServicesErrorEnvelope) ??
            response.ReasonPhrase ??
            fallbackMessage;
        throw new HonuaAdminApiException(
            response.StatusCode,
            message,
            body,
            HonuaFailureReceiptFactory.FromHttpResponse(response, body));
    }

    public static void EnsureEnvelopeSucceeded(HttpResponseMessage response, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (doc.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                var message = TryExtractErrorMessage(body) ?? "API response indicated failure.";
                throw new HonuaAdminApiException(
                    response.StatusCode,
                    message,
                    body,
                    HonuaFailureReceiptFactory.FromHttpResponse(response, body));
            }
        }
        catch (JsonException)
        {
            // Not JSON or invalid JSON envelope, ignore.
        }
    }

    public static string? TryExtractErrorMessage(string body, bool preferGeoServicesError = false)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (preferGeoServicesError &&
                TryGetErrorObjectMessage(doc.RootElement, out var preferredErrorMessage))
            {
                return preferredErrorMessage;
            }

            if (doc.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }

            if (TryGetErrorObjectMessage(doc.RootElement, out var errorMessage))
            {
                return errorMessage;
            }
        }
        catch (JsonException)
        {
            // Not JSON, that's fine.
            return null;
        }

        return HonuaProblemDetailsParser.TryParse(body, out var problem)
            ? problem?.Detail ?? problem?.Title
            : null;
    }

    private static void EnsureGeoServicesEnvelopeSucceeded(HttpResponseMessage response, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("error", out var errorElement) ||
                errorElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var message = TryGetString(errorElement, "message") ?? "Geocoding service returned an error.";
            var responseStatus = response.StatusCode;
            int? protocolCode = null;
            if (errorElement.TryGetProperty("code", out var codeProperty) &&
                codeProperty.TryGetInt32(out var errorCode))
            {
                protocolCode = errorCode;
                if (errorCode is >= 100 and <= 599)
                {
                    responseStatus = (HttpStatusCode)errorCode;
                }
            }

            throw new HonuaAdminApiException(
                responseStatus,
                message,
                body,
                HonuaFailureReceiptFactory.FromHttpResponse(response, body, protocolCode));
        }
        catch (JsonException)
        {
            // Not JSON, ignore.
        }
    }

    private static bool TryGetErrorObjectMessage(JsonElement root, out string? message)
    {
        message = null;
        if (root.TryGetProperty("error", out var errorElement) &&
            errorElement.ValueKind == JsonValueKind.Object)
        {
            message = TryGetString(errorElement, "message");
            return message is not null;
        }

        return false;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
