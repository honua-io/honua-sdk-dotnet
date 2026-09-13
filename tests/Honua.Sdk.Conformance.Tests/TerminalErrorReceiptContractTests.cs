// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Abstractions;

namespace Honua.Sdk.Conformance.Tests;

public sealed class TerminalErrorReceiptContractTests
{
    private static readonly Manifest Contract = JsonSerializer.Deserialize<Manifest>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "terminal-error-receipts.v1.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public static TheoryData<string, FailureClass> DotNetCells()
    {
        TheoryData<string, FailureClass> cells = [];
        foreach (string path in Contract.SdkPaths.Where(path => path.Sdk == "dotnet").Select(path => path.Id))
        {
            foreach (FailureClass failure in Contract.FailureClasses)
            {
                cells.Add(path, failure);
            }
        }

        return cells;
    }

    [Fact]
    public void Contract_has_exactly_40_global_and_15_dotnet_cells()
    {
        Assert.Equal(40, Contract.ExpectedCellCount);
        Assert.Equal(40, Contract.SdkPaths.Count * Contract.FailureClasses.Count);
        Assert.Equal(15, Contract.SdkPaths.Count(path => path.Sdk == "dotnet") * Contract.FailureClasses.Count);
    }

    [Theory]
    [MemberData(nameof(DotNetCells))]
    public async Task Receipt_preserves_each_dotnet_transport_failure_cell(string path, FailureClass failure)
    {
        const string correlationId = "contract-correlation-id";
        HonuaFailureReceipt receipt;

        if (path == "dotnet-grpc")
        {
            var initial = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-test-initial"] = [failure.Id],
                ["authorization"] = ["secret"]
            };
            var trailing = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["honua-correlation-id"] = [correlationId],
                ["honua-error-kind"] = [failure.Kind],
                ["honua-error-code"] = [failure.Code],
                ["honua-retryable"] = [failure.Retryable ? "true" : "false"]
            };
            if (failure.RetryAfterSeconds is { } retryAfter)
            {
                trailing["retry-after"] = [retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            }
            if (failure.Errors is { ValueKind: JsonValueKind.Array } errors)
            {
                trailing["honua-error-details"] = [errors.GetRawText()];
            }

            receipt = HonuaFailureReceiptFactory.FromGrpc(failure.GrpcStatus.Number, initial, trailing);
            Assert.Null(receipt.TransportStatus);
            Assert.Equal(failure.GrpcStatus.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), receipt.ProtocolCode);
            Assert.NotEmpty(receipt.ProtocolMetadata.Initial);
            Assert.NotEmpty(receipt.ProtocolMetadata.Trailing);
        }
        else
        {
            bool geoServices = path == "dotnet-geoservices";
            using var response = new HttpResponseMessage(geoServices ? HttpStatusCode.OK : (HttpStatusCode)failure.HttpStatus)
            {
                Content = new StringContent(BuildBody(failure, geoServices, correlationId))
            };
            response.Headers.Add("X-Correlation-ID", correlationId);
            response.Headers.TryAddWithoutValidation("Authorization", "secret");
            if (failure.RetryAfterSeconds is { } retryAfter)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfter));
            }

            receipt = HonuaFailureReceiptFactory.FromHttpResponse(
                response,
                await response.Content.ReadAsStringAsync(),
                geoServices ? failure.GeoServicesCode : null);
            Assert.Equal(geoServices ? 200 : failure.HttpStatus, receipt.TransportStatus);
            Assert.Equal(geoServices ? failure.GeoServicesCode.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, receipt.ProtocolCode);
            Assert.NotEmpty(receipt.ProtocolMetadata.Initial);
            Assert.Empty(receipt.ProtocolMetadata.Trailing);
        }

        Assert.Equal(ParseKind(failure.Kind), receipt.Kind);
        Assert.Equal(failure.Code, receipt.Code);
        Assert.Equal(failure.Retryable, receipt.Retryable);
        Assert.Equal(failure.RetryAfterSeconds, receipt.RetryAfter?.TotalSeconds);
        Assert.Equal(correlationId, receipt.CorrelationId);
        Assert.Equal(path == "dotnet-geoservices" ? 0 : failure.Errors?.GetArrayLength() ?? 0, receipt.FieldErrors.Count);
        Assert.DoesNotContain(receipt.ProtocolMetadata.Initial.Keys, key => key.Equals("authorization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Authentication_remains_distinct_from_authorization()
    {
        FailureClass authorization = Contract.FailureClasses.Single(failure => failure.Id == "authz-denied");
        using var response = new HttpResponseMessage((HttpStatusCode)authorization.AuthenticationRequired!.HttpStatus)
        {
            Content = new StringContent("{}")
        };

        HonuaFailureReceipt receipt = HonuaFailureReceiptFactory.FromHttpResponse(response, "{}");

        Assert.Equal(HonuaFailureKind.Authentication, receipt.Kind);
        Assert.Equal("authentication_required", receipt.Code);
        Assert.NotEqual(HonuaFailureKind.Authorization, receipt.Kind);
    }

    private static string BuildBody(FailureClass failure, bool geoServices, string correlationId)
    {
        object payload = new
        {
            kind = failure.Kind,
            code = failure.Code,
            correlationId,
            retryable = failure.Retryable,
            retryAfterSeconds = failure.RetryAfterSeconds,
            errors = failure.Errors,
            message = failure.Detail
        };
        if (!geoServices)
        {
            return JsonSerializer.Serialize(payload);
        }

        return JsonSerializer.Serialize(new
        {
            error = new
            {
                code = failure.GeoServicesCode,
                details = new[] { $"Correlation ID: {correlationId}" },
                retryable = failure.Retryable,
                retryAfterSeconds = failure.RetryAfterSeconds,
                message = failure.Detail
            }
        });
    }

    private static HonuaFailureKind ParseKind(string kind) => kind switch
    {
        "authorization" => HonuaFailureKind.Authorization,
        "not-found" => HonuaFailureKind.NotFound,
        "validation" => HonuaFailureKind.Validation,
        "conflict" => HonuaFailureKind.Conflict,
        "throttled" => HonuaFailureKind.Throttled,
        _ => HonuaFailureKind.Unknown
    };

    public sealed record Manifest(int ExpectedCellCount, IReadOnlyList<SdkPath> SdkPaths, IReadOnlyList<FailureClass> FailureClasses);
    public sealed record SdkPath(string Id, string Sdk);
    public sealed record FailureClass(
        string Id,
        int HttpStatus,
        int GeoServicesCode,
        GrpcStatus GrpcStatus,
        string Kind,
        string Code,
        bool Retryable,
        double? RetryAfterSeconds,
        string Detail,
        JsonElement? Errors,
        AuthenticationRequired? AuthenticationRequired);
    public sealed record GrpcStatus(string Name, int Number);
    public sealed record AuthenticationRequired(int HttpStatus, GrpcStatus GrpcStatus, string Kind, string Code);
}
