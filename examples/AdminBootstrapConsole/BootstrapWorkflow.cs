using System.Globalization;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Models;

namespace AdminBootstrapConsole;

public sealed class BootstrapWorkflow(
    IHonuaAdminClient adminClient,
    IHonuaGrpcClient grpcClient,
    BootstrapOptions options)
{
    private readonly IHonuaAdminClient _adminClient = adminClient;
    private readonly IHonuaGrpcClient _grpcClient = grpcClient;
    private readonly BootstrapOptions _options = options;

    public async Task<BootstrapRunSummary> RunAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        await WriteStepHeaderAsync(output, "Preflight", includeLeadingBlankLine: false);

        var compatibility = await _adminClient.CheckCompatibilityAsync(cancellationToken);
        await output.WriteLineAsync(
            $"Server: {_options.ServerUri} | Version: {compatibility.ServerVersion} | Release channel: {compatibility.ReleaseChannel}");

        if (!compatibility.IsSupported)
        {
            throw new BootstrapCompatibilityException(compatibility);
        }

        await output.WriteLineAsync("Compatibility check passed.");

        var connection = await EnsureConnectionAsync(output, cancellationToken);
        var table = await DiscoverTableAsync(output, connection.Connection.ConnectionId.ToString("D"), cancellationToken);
        var layer = await EnsureLayerAsync(output, connection.Connection.ConnectionId.ToString("D"), table, cancellationToken);
        var configuration = await ConfigureAsync(output, connection.Connection.ConnectionId.ToString("D"), layer.Layer, cancellationToken);
        var verificationFields = SelectVerificationFields(table);
        var verification = await VerifyAsync(output, configuration.Layer, verificationFields, cancellationToken);

        await WriteStepHeaderAsync(output, "Summary");
        await output.WriteLineAsync(
            $"Connection: {connection.Connection.Name} ({(connection.Created ? "created" : "reused")})");
        await output.WriteLineAsync(
            $"Layer: {configuration.Layer.LayerName} on service '{configuration.Layer.ServiceName}' " +
            $"({(layer.Published ? "published" : "reused")})");
        await output.WriteLineAsync(
            $"Protocols: {string.Join(", ", configuration.ServiceSettings.EnabledProtocols)}");
        await output.WriteLineAsync(
            verification.Features.Count == 0
                ? "Verification: query succeeded with 0 rows."
                : $"Verification: query succeeded with {verification.Features.Count} row(s).");

        return new BootstrapRunSummary(
            compatibility,
            connection.Connection,
            table,
            configuration.Layer,
            configuration.ServiceSettings,
            verification,
            verificationFields,
            connection.Created,
            layer.Published,
            configuration.ProtocolsUpdated);
    }

    private async Task<ConnectionOutcome> EnsureConnectionAsync(TextWriter output, CancellationToken cancellationToken)
    {
        await WriteStepHeaderAsync(output, "Connection");

        var request = _options.ToCreateConnectionRequest();
        var draftTest = await _adminClient.TestDraftConnectionAsync(request, cancellationToken);

        if (!draftTest.IsHealthy)
        {
            throw new HonuaAdminOperationException(
                $"Draft connection test failed: {draftTest.Message ?? "No detail returned."}",
                "TestDraftConnection");
        }

        await output.WriteLineAsync(
            $"Draft connection test passed for '{_options.ConnectionName}' targeting {_options.DbHost}:{_options.DbPort}/{_options.DbName}.");

        var existing = (await _adminClient.ListConnectionsAsync(cancellationToken))
            .FirstOrDefault(connection => string.Equals(connection.Name, _options.ConnectionName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            var created = await _adminClient.CreateConnectionAsync(request, cancellationToken);
            await output.WriteLineAsync(
                $"Created connection '{created.Name}' ({created.ConnectionId:D}).");
            return new ConnectionOutcome(created, Created: true);
        }

        if (!MatchesConfiguredConnection(existing))
        {
            throw new HonuaAdminOperationException(
                $"Connection '{existing.Name}' already exists but points to " +
                $"{existing.Username}@{existing.Host}:{existing.Port}/{existing.DatabaseName} " +
                $"(sslRequired={existing.SslRequired}, sslMode={existing.SslMode}). " +
                "Use a different connection name or align the sample configuration.",
                "CreateConnection");
        }

        var health = await _adminClient.TestConnectionAsync(existing.ConnectionId.ToString("D"), cancellationToken);
        if (!health.IsHealthy)
        {
            throw new HonuaAdminOperationException(
                $"Existing connection '{existing.Name}' failed its health check: {health.Message ?? "No detail returned."}",
                "TestConnection");
        }

        await output.WriteLineAsync(
            $"Reused connection '{existing.Name}' ({existing.ConnectionId:D}).");
        return new ConnectionOutcome(existing, Created: false);
    }

    private async Task<TableInfo> DiscoverTableAsync(TextWriter output, string connectionId, CancellationToken cancellationToken)
    {
        await WriteStepHeaderAsync(output, "Discovery");

        var discovery = await _adminClient.DiscoverTablesAsync(connectionId, cancellationToken);
        var table = discovery.Tables.FirstOrDefault(candidate =>
            string.Equals(candidate.Schema, _options.Schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Table, _options.Table, StringComparison.OrdinalIgnoreCase));

        if (table is null)
        {
            var discoveredTables = discovery.Tables
                .Select(candidate => $"{candidate.Schema}.{candidate.Table}")
                .Take(8)
                .ToArray();
            var discoveredSummary = discoveredTables.Length == 0
                ? "no spatial tables discovered"
                : string.Join(", ", discoveredTables);

            throw new HonuaAdminOperationException(
                $"Table '{_options.Schema}.{_options.Table}' was not discovered. Discovered: {discoveredSummary}.",
                "DiscoverTables");
        }

        if (string.IsNullOrWhiteSpace(table.GeometryColumn) ||
            string.IsNullOrWhiteSpace(table.GeometryType) ||
            table.Srid is null)
        {
            throw new HonuaAdminOperationException(
                $"Table '{table.Schema}.{table.Table}' is missing geometry metadata required for publish.",
                "DiscoverTables");
        }

        _ = GetSinglePrimaryKey(table);

        await output.WriteLineAsync(
            $"Found table '{table.Schema}.{table.Table}' with geometry column '{table.GeometryColumn}', " +
            $"geometry type '{table.GeometryType}', SRID {table.Srid}, estimated rows {table.EstimatedRows?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}.");

        return table;
    }

    private async Task<LayerOutcome> EnsureLayerAsync(
        TextWriter output,
        string connectionId,
        TableInfo table,
        CancellationToken cancellationToken)
    {
        await WriteStepHeaderAsync(output, "Publish");

        var layers = await _adminClient.ListLayersAsync(connectionId, _options.ServiceName, cancellationToken);

        var sameSourceLayer = layers.FirstOrDefault(candidate =>
            string.Equals(candidate.Schema, table.Schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Table, table.Table, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.ServiceName, _options.ServiceName, StringComparison.OrdinalIgnoreCase));

        var sameNameLayer = layers.FirstOrDefault(candidate =>
            string.Equals(candidate.LayerName, _options.LayerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.ServiceName, _options.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (sameNameLayer is not null &&
            (sameSourceLayer is null || sameNameLayer.LayerId != sameSourceLayer.LayerId))
        {
            throw new HonuaAdminOperationException(
                $"Layer name '{_options.LayerName}' already exists in service '{_options.ServiceName}' " +
                $"for source table '{sameNameLayer.Schema}.{sameNameLayer.Table}'.",
                "PublishLayer");
        }

        if (sameSourceLayer is not null)
        {
            await output.WriteLineAsync(
                $"Reused published layer '{sameSourceLayer.LayerName}' (layerId={sameSourceLayer.LayerId}) " +
                $"for '{sameSourceLayer.Schema}.{sameSourceLayer.Table}'.");
            return new LayerOutcome(sameSourceLayer, Published: false);
        }

        var publishRequest = new PublishLayerRequest
        {
            Schema = table.Schema,
            Table = table.Table,
            LayerName = _options.LayerName,
            GeometryColumn = table.GeometryColumn,
            GeometryType = table.GeometryType,
            Srid = table.Srid,
            PrimaryKey = GetSinglePrimaryKey(table),
            ServiceName = _options.ServiceName,
            Enabled = true
        };

        var published = await _adminClient.PublishLayerAsync(connectionId, publishRequest, cancellationToken);
        await output.WriteLineAsync(
            $"Published layer '{published.LayerName}' (layerId={published.LayerId}) to service '{published.ServiceName}'.");
        return new LayerOutcome(published, Published: true);
    }

    private async Task<ConfigurationOutcome> ConfigureAsync(
        TextWriter output,
        string connectionId,
        PublishedLayerSummary layer,
        CancellationToken cancellationToken)
    {
        await WriteStepHeaderAsync(output, "Configure");

        var configuredLayer = await _adminClient.SetLayerEnabledAsync(connectionId, layer.LayerId, true, layer.ServiceName, cancellationToken);
        await output.WriteLineAsync(
            $"Enabled layer '{configuredLayer.LayerName}' (layerId={configuredLayer.LayerId}) on service '{configuredLayer.ServiceName}'.");

        var serviceSettings = await _adminClient.GetServiceSettingsAsync(configuredLayer.ServiceName, cancellationToken);
        var protocolsUpdated = false;

        if (!serviceSettings.EnabledProtocols.Contains("Grpc", StringComparer.OrdinalIgnoreCase))
        {
            var mergedProtocols = serviceSettings.EnabledProtocols
                .Concat(["Grpc"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            serviceSettings = await _adminClient.UpdateProtocolsAsync(configuredLayer.ServiceName, mergedProtocols, cancellationToken);
            protocolsUpdated = true;
            await output.WriteLineAsync(
                $"Enabled Grpc for service '{configuredLayer.ServiceName}'.");
        }
        else
        {
            await output.WriteLineAsync(
                $"Grpc already enabled for service '{configuredLayer.ServiceName}'.");
        }

        return new ConfigurationOutcome(configuredLayer, serviceSettings, protocolsUpdated);
    }

    private async Task<QueryFeaturesResponse> VerifyAsync(
        TextWriter output,
        PublishedLayerSummary layer,
        IReadOnlyList<string> verificationFields,
        CancellationToken cancellationToken)
    {
        await WriteStepHeaderAsync(output, "Verify");

        await output.WriteLineAsync(
            $"Querying service '{layer.ServiceName}', layerId={layer.LayerId}, fields: {string.Join(", ", verificationFields)}.");

        var response = await _grpcClient.QueryFeaturesAsync(new QueryFeaturesRequest
        {
            ServiceId = layer.ServiceName,
            LayerId = layer.LayerId,
            Where = "1=1",
            OutFields = verificationFields,
            ReturnGeometry = false,
            ResultRecordCount = 3,
            OrderBy = verificationFields[0]
        }, cancellationToken);

        if (response.Features.Count == 0)
        {
            await output.WriteLineAsync(
                "Bounded gRPC query succeeded. The layer is queryable but currently has no rows.");
            return response;
        }

        await output.WriteLineAsync(
            $"Bounded gRPC query returned {response.Features.Count} row(s).");

        foreach (var feature in response.Features)
        {
            await output.WriteLineAsync(
                $"  [{feature.Id}] {FormatAttributes(feature.Attributes, verificationFields)}");
        }

        return response;
    }

    private static async Task WriteStepHeaderAsync(
        TextWriter output,
        string header,
        bool includeLeadingBlankLine = true)
    {
        if (includeLeadingBlankLine)
        {
            await output.WriteLineAsync();
        }

        await output.WriteLineAsync($"=== {header} ===");
    }

    private bool MatchesConfiguredConnection(SecureConnectionSummary connection)
    {
        return string.Equals(connection.Host, _options.DbHost, StringComparison.OrdinalIgnoreCase) &&
               connection.Port == _options.DbPort &&
               string.Equals(connection.DatabaseName, _options.DbName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(connection.Username, _options.DbUser, StringComparison.OrdinalIgnoreCase) &&
               connection.SslRequired == _options.DbSslRequired &&
               string.Equals(connection.SslMode, _options.DbSslMode, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSinglePrimaryKey(TableInfo table)
    {
        var primaryKeys = table.Columns
            .Where(column => column.IsPrimaryKey)
            .Select(column => column.Name)
            .ToArray();

        return primaryKeys.Length switch
        {
            0 => throw new HonuaAdminOperationException(
                $"Table '{table.Schema}.{table.Table}' does not expose a primary key column.",
                "DiscoverTables"),
            > 1 => throw new HonuaAdminOperationException(
                $"Table '{table.Schema}.{table.Table}' exposes a composite primary key. The sample requires a single primary key column.",
                "DiscoverTables"),
            _ => primaryKeys[0]
        };
    }

    private static IReadOnlyList<string> SelectVerificationFields(TableInfo table)
    {
        var primaryKey = GetSinglePrimaryKey(table);
        var additionalFields = table.Columns
            .Where(column =>
                !column.IsPrimaryKey &&
                !string.Equals(column.Name, table.GeometryColumn, StringComparison.OrdinalIgnoreCase))
            .Select(column => column.Name)
            .Take(2);

        return new[] { primaryKey }
            .Concat(additionalFields)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FormatAttributes(
        IReadOnlyDictionary<string, object?> attributes,
        IReadOnlyList<string> orderedFieldNames)
    {
        var parts = new List<string>(orderedFieldNames.Count);

        foreach (var fieldName in orderedFieldNames)
        {
            attributes.TryGetValue(fieldName, out var value);
            parts.Add($"{fieldName}={FormatValue(value)}");
        }

        return string.Join(", ", parts);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private sealed record ConnectionOutcome(SecureConnectionSummary Connection, bool Created);

    private sealed record LayerOutcome(PublishedLayerSummary Layer, bool Published);

    private sealed record ConfigurationOutcome(
        PublishedLayerSummary Layer,
        ServiceSettingsResponse ServiceSettings,
        bool ProtocolsUpdated);
}

public sealed record BootstrapRunSummary(
    ServerCompatibilityResult Compatibility,
    SecureConnectionSummary Connection,
    TableInfo Table,
    PublishedLayerSummary Layer,
    ServiceSettingsResponse ServiceSettings,
    QueryFeaturesResponse Verification,
    IReadOnlyList<string> VerificationFields,
    bool CreatedConnection,
    bool PublishedLayer,
    bool UpdatedProtocols);
