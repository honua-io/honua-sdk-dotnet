// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Default <see cref="IHonuaFeatureGateway"/> that composes the registered feature
/// query and attachment providers and routes each operation to the first provider
/// that advertises support for it.
/// </summary>
/// <remarks>
/// Attachment operations resolve over the first attachment-capable provider (for
/// example the GeoServices FeatureServer client) even when the primary feature
/// transport is gRPC, which exposes no attachment RPCs (native gRPC parity is
/// tracked in geospatial-grpc#43). Query operations run on the
/// primary provider unless the request needs a facet — a provider-neutral time
/// filter or a grouped-statistics <c>having</c> clause — the primary provider does
/// not advertise, in which case the gateway transparently routes the query to a
/// provider that does.
/// </remarks>
public sealed class HonuaFeatureGateway : IHonuaFeatureGateway
{
    private readonly List<IHonuaFeatureQueryClient> _queryClients;
    private readonly List<IHonuaFeatureAttachmentClient> _attachmentClients;
    private readonly IHonuaFeatureQueryClient _primaryQueryClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureGateway"/> class.
    /// </summary>
    /// <param name="queryClients">
    /// Registered feature query providers, ordered by preference. The first entry is
    /// treated as the primary provider; later entries are fallbacks for facets the
    /// primary provider does not support.
    /// </param>
    /// <param name="attachmentClients">
    /// Registered feature attachment providers, ordered by preference. Attachment
    /// operations resolve to the first provider that advertises support for the
    /// requested operation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="queryClients"/> or <paramref name="attachmentClients"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when no query providers are supplied.
    /// </exception>
    public HonuaFeatureGateway(
        IEnumerable<IHonuaFeatureQueryClient> queryClients,
        IEnumerable<IHonuaFeatureAttachmentClient> attachmentClients)
    {
        ArgumentNullException.ThrowIfNull(queryClients);
        ArgumentNullException.ThrowIfNull(attachmentClients);

        _queryClients = queryClients.Where(static c => c is not IHonuaFeatureGateway).ToList();
        _attachmentClients = attachmentClients.Where(static c => c is not IHonuaFeatureGateway).ToList();

        if (_queryClients.Count == 0)
        {
            throw new ArgumentException(
                "The feature gateway requires at least one feature query provider.",
                nameof(queryClients));
        }

        _primaryQueryClient = _queryClients[0];
    }

    /// <inheritdoc />
    public string ProviderName => "gateway";

    /// <inheritdoc cref="IHonuaFeatureQueryClient.QueryCapabilities" />
    public FeatureQueryCapabilities QueryCapabilities => new()
    {
        SupportsTimeFilter = _queryClients.Any(static c => c.QueryCapabilities.SupportsTimeFilter),
        SupportsStatistics = _queryClients.Any(static c => c.QueryCapabilities.SupportsStatistics),
        SupportsGroupBy = _queryClients.Any(static c => c.QueryCapabilities.SupportsGroupBy),
        SupportsHaving = _queryClients.Any(static c => c.QueryCapabilities.SupportsHaving),
        NativeSurface = "feature gateway (capability-routed)",
    };

    /// <inheritdoc />
    public FeatureAttachmentCapabilities AttachmentCapabilities => new()
    {
        SupportsList = _attachmentClients.Any(static c => c.AttachmentCapabilities.SupportsList),
        SupportsDownload = _attachmentClients.Any(static c => c.AttachmentCapabilities.SupportsDownload),
        SupportsAdd = _attachmentClients.Any(static c => c.AttachmentCapabilities.SupportsAdd),
        SupportsUpdate = _attachmentClients.Any(static c => c.AttachmentCapabilities.SupportsUpdate),
        SupportsDelete = _attachmentClients.Any(static c => c.AttachmentCapabilities.SupportsDelete),
        NativeSurface = "feature gateway (capability-routed)",
        UnsupportedReason = _attachmentClients.Count == 0
            ? "No attachment-capable feature provider is registered. Enable the GeoServices FeatureServer client (UseGeoServices) or another attachment provider."
            : null,
    };

    /// <inheritdoc />
    public Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveQueryClient(request).QueryAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveQueryClient(request).QueryPagesAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FeatureAttachmentInfo>> ListAttachmentsAsync(
        FeatureAttachmentListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveAttachmentClient(static c => c.SupportsList, "list")
            .ListAttachmentsAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureAttachmentContent> DownloadAttachmentAsync(
        FeatureAttachmentDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveAttachmentClient(static c => c.SupportsDownload, "download")
            .DownloadAttachmentAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> AddAttachmentAsync(
        FeatureAttachmentAddRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveAttachmentClient(static c => c.SupportsAdd, "add")
            .AddAttachmentAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> UpdateAttachmentAsync(
        FeatureAttachmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveAttachmentClient(static c => c.SupportsUpdate, "update")
            .UpdateAttachmentAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> DeleteAttachmentAsync(
        FeatureAttachmentDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveAttachmentClient(static c => c.SupportsDelete, "delete")
            .DeleteAttachmentAsync(request, cancellationToken);
    }

    private IHonuaFeatureQueryClient ResolveQueryClient(FeatureQueryRequest request)
    {
        var needsTimeFilter = request.TimeFilter is not null;
        var needsHaving = !string.IsNullOrWhiteSpace(request.Having);

        if (!needsTimeFilter && !needsHaving)
        {
            return _primaryQueryClient;
        }

        if (IsQueryFacetSupported(_primaryQueryClient.QueryCapabilities, needsTimeFilter, needsHaving))
        {
            return _primaryQueryClient;
        }

        foreach (var client in _queryClients)
        {
            if (IsQueryFacetSupported(client.QueryCapabilities, needsTimeFilter, needsHaving))
            {
                return client;
            }
        }

        var facet = (needsTimeFilter, needsHaving) switch
        {
            (true, true) => "a time filter and a grouped-statistics having clause",
            (true, false) => "a time filter",
            _ => "a grouped-statistics having clause",
        };
        throw new NotSupportedException(
            $"No registered feature query provider supports {facet}. " +
            "Enable the GeoServices FeatureServer client (UseGeoServices) or another provider whose " +
            "QueryCapabilities advertise the required facet.");
    }

    private static bool IsQueryFacetSupported(FeatureQueryCapabilities capabilities, bool needsTimeFilter, bool needsHaving)
        => (!needsTimeFilter || capabilities.SupportsTimeFilter)
            && (!needsHaving || capabilities.SupportsHaving);

    private IHonuaFeatureAttachmentClient ResolveAttachmentClient(
        Func<FeatureAttachmentCapabilities, bool> supports,
        string operation)
    {
        foreach (var client in _attachmentClients)
        {
            if (supports(client.AttachmentCapabilities))
            {
                return client;
            }
        }

        throw new NotSupportedException(
            $"No registered feature provider supports attachment {operation}. " +
            "Enable the GeoServices FeatureServer client (UseGeoServices) or another attachment-capable provider; " +
            "the gRPC FeatureService does not expose attachment RPCs.");
    }
}
