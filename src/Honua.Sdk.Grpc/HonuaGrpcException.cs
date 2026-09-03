// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Grpc.Core;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Exception thrown when a gRPC call to the Honua server fails.
/// </summary>
public sealed class HonuaGrpcException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    public HonuaGrpcException()
        : this(StatusCode.Unknown, "gRPC request failed.")
    {
    }

    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    /// <param name="message">The error detail message.</param>
    public HonuaGrpcException(string message)
        : this(StatusCode.Unknown, message)
    {
    }

    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    /// <param name="message">The error detail message.</param>
    /// <param name="innerException">The original exception.</param>
    public HonuaGrpcException(string message, Exception innerException)
        : this(StatusCode.Unknown, message, innerException)
    {
    }

    /// <summary>
    /// The gRPC status code.
    /// </summary>
    public StatusCode StatusCode { get; }

    /// <inheritdoc />
    public override Honua.Sdk.Abstractions.HonuaFailureReceipt? FailureReceipt { get; }

    /// <inheritdoc />
    /// <remarks>gRPC is not an HTTP transport, so no HTTP status is surfaced; use <see cref="StatusCode"/>.</remarks>
    public override int? HttpStatus => null;

    /// <inheritdoc />
    public override Honua.Sdk.Abstractions.HonuaProblemDetails? ProblemDetails =>
        new Honua.Sdk.Abstractions.HonuaProblemDetails
        {
            Title = StatusCode.ToString(),
            Detail = Message,
        };

    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    /// <param name="statusCode">The gRPC status code.</param>
    /// <param name="message">The error detail message.</param>
    /// <param name="innerException">The original exception, if any.</param>
    public HonuaGrpcException(StatusCode statusCode, string message, Exception? innerException = null)
        : this(statusCode, message, innerException, null, null)
    {
    }

    /// <summary>Creates a new gRPC exception with its protocol metadata intact.</summary>
    /// <param name="statusCode">The gRPC status code.</param>
    /// <param name="message">The error detail message.</param>
    /// <param name="innerException">The original exception, if any.</param>
    /// <param name="initialMetadata">Initial response metadata.</param>
    /// <param name="trailingMetadata">Terminal response trailers.</param>
    internal HonuaGrpcException(
        StatusCode statusCode,
        string message,
        Exception? innerException,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? initialMetadata,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? trailingMetadata)
        : base($"gRPC {statusCode}: {message}", innerException)
    {
        StatusCode = statusCode;
        FailureReceipt = Honua.Sdk.Abstractions.HonuaFailureReceiptFactory.FromGrpc(
            (int)statusCode,
            initialMetadata,
            trailingMetadata);
    }

    internal static HonuaGrpcException FromRpcException(
        RpcException exception,
        Metadata? initialMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new HonuaGrpcException(
            exception.StatusCode,
            exception.Status.Detail,
            exception,
            CopyMetadata(initialMetadata),
            CopyMetadata(exception.Trailers));
    }

    private static Dictionary<string, IReadOnlyList<string>> CopyMetadata(Metadata? metadata)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (metadata is null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (Metadata.Entry entry in metadata)
        {
            string value = entry.IsBinary
                ? Convert.ToBase64String(entry.ValueBytes)
                : entry.Value;
            if (!result.TryGetValue(entry.Key, out List<string>? values))
            {
                values = [];
                result[entry.Key] = values;
            }

            values.Add(value);
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    internal static async Task<Metadata?> TryGetInitialMetadataAsync(Task<Metadata>? responseHeaders)
    {
        if (responseHeaders is null)
        {
            return null;
        }

        try
        {
            return await responseHeaders.ConfigureAwait(false);
        }
        catch (RpcException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
