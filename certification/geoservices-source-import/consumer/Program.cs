// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

// Installed-package certification for honua-sdk-dotnet#341: lossless ArcGIS
// service/layer import through the published Honua.Sdk.GeoServices package.
//
// Every cell drives the public SDK surface against a live source and compares
// the result with either (a) the hand-derived fixture oracle, for data values,
// or (b) the source's own wire JSON fetched independently of the SDK, for
// metadata. A cell passes only on true preservation: a field that the SDK
// merely recognises (for example, a typed model that parses but drops keys)
// fails with the exact lost JSON paths.

using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Sdk.GeoServices;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Microsoft.Extensions.DependencyInjection;

var arguments = CommandLine.Parse(args);
var certification = new SourceImportCertification(arguments);
return await certification.RunAsync().ConfigureAwait(false);

internal sealed class CommandLine
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public static CommandLine Parse(string[] args)
    {
        var parsed = new CommandLine();
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{name}'.");
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                parsed._values[name[2..]] = args[++i];
            }
            else
            {
                parsed._flags.Add(name[2..]);
            }
        }

        return parsed;
    }

    public string Required(string name) =>
        _values.TryGetValue(name, out var value) && value.Length > 0
            ? value
            : throw new ArgumentException($"--{name} is required.");

    public string? Optional(string name) => _values.GetValueOrDefault(name);

    public bool Flag(string name) => _flags.Contains(name);
}

internal sealed class CellFailure(string detail) : Exception(detail);

internal sealed record CellResult(string Id, string Criterion, string Verdict, string Detail);

internal sealed class TransportProbe
{
    private int _requests;

    public int Requests => _requests;

    public List<string> Observations { get; } = [];

    public void Record(HttpRequestMessage request)
    {
        Interlocked.Increment(ref _requests);
        var query = request.RequestUri?.Query ?? string.Empty;
        var observation =
            $"{request.Method} {request.RequestUri?.AbsolutePath} " +
            $"token-param={query.Contains("token=", StringComparison.Ordinal)} " +
            $"authorization={request.Headers.Authorization?.Scheme ?? "none"} " +
            $"x-api-key={request.Headers.Contains("X-API-Key")}";
        lock (Observations)
        {
            Observations.Add(observation);
        }
    }
}

internal sealed class RecordingHandler(TransportProbe probe, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        probe.Record(request);
        return base.SendAsync(request, cancellationToken);
    }
}

internal sealed class SourceImportCertification
{
    private static readonly JsonSerializerOptions ModelJson = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false,
    };

    private readonly CommandLine _args;
    private readonly JsonObject _oracle;
    private readonly string _serviceName;
    private readonly int _layerId;
    private readonly long[] _objectIds;
    private readonly X509Certificate2? _tlsRoot;
    private readonly List<CellResult> _results = [];

    public SourceImportCertification(CommandLine args)
    {
        _args = args;
        _oracle = JsonNode.Parse(File.ReadAllText(args.Required("expected")))!.AsObject();
        if (args.Flag("corrupt-oracle"))
        {
            // Negative control: 2^53 is what a double-precision decode of the source's
            // 2^53 + 1 would produce. A certification that cannot tell the two apart
            // must not be trusted, so this run is required to fail data.int64-precision.
            _oracle["rows"]!["1"]!["attributes"]!["big_counter"] = JsonValue.Create(9007199254740992L);
        }

        _serviceName = _oracle["serviceName"]!.GetValue<string>();
        _layerId = _oracle["layerId"]!.GetValue<int>();
        _objectIds = _oracle["objectIds"]!.AsArray().Select(node => node!.GetValue<long>()).ToArray();
        var tlsRoot = args.Optional("tls-root");
        _tlsRoot = tlsRoot is null ? null : X509Certificate2.CreateFromPem(File.ReadAllText(tlsRoot));
    }

    private Uri BaseUrl => new(_args.Required("base-url"));

    private string ApiKey => _args.Required("api-key");

    public async Task<int> RunAsync()
    {
        await RunCellAsync("package.published-bytes", "AC6 normal versioned PackageReference from the published package", PublishedPackageAsync).ConfigureAwait(false);
        await RunCellAsync("package.typed-importer-metadata", "AC1/AC2 published typed members preserve an independently authored source fixture", PublishedMetadataFixture.VerifyAsync).ConfigureAwait(false);
        await RunCellAsync("metadata.raw-source-documents", "AC1/AC2 importer raw metadata APIs preserve source member presence and values", RawMetadataAsync).ConfigureAwait(false);

        await RunCellAsync("discovery.service-layers", "AC1 service/layer discovery", ServiceLayersAsync).ConfigureAwait(false);
        await RunCellAsync("discovery.service-tables", "AC1 table discovery", () => ServiceMetadataPathAsync("$.tables")).ConfigureAwait(false);
        await RunCellAsync("metadata.service.lossless-roundtrip", "AC2 service metadata preserved", () => ServiceMetadataPathAsync("$")).ConfigureAwait(false);
        await RunCellAsync("metadata.layer.lossless-roundtrip", "AC2 layer metadata preserved", () => LayerMetadataPathAsync("$", requireSourceKey: false)).ConfigureAwait(false);
        await RunCellAsync("metadata.layer.drawing-info", "AC2 drawingInfo/labels preserved", () => LayerMetadataPathAsync("$.drawingInfo", requireSourceKey: true)).ConfigureAwait(false);
        await RunCellAsync("metadata.layer.relationships", "AC2 relationship keys preserved", () => LayerMetadataPathAsync("$.relationships", requireSourceKey: true)).ConfigureAwait(false);
        await RunCellAsync("metadata.layer.time-info", "AC2 temporal metadata preserved", LayerTimeInfoAsync).ConfigureAwait(false);
        await RunCellAsync("metadata.layer.pagination-capabilities", "AC3 supportsPagination/advancedQueryCapabilities preserved", PaginationCapabilitiesAsync).ConfigureAwait(false);
        await RunCellAsync("metadata.field.lossless-roundtrip", "AC2 field metadata preserved", () => LayerMetadataPathAsync("$.fields", requireSourceKey: true)).ConfigureAwait(false);
        await RunCellAsync("schema.object-id-field", "AC1 schema: object ID field", ObjectIdFieldAsync).ConfigureAwait(false);
        await RunCellAsync("schema.field-types", "AC1/AC2 schema: field types", FieldTypesAsync).ConfigureAwait(false);
        await RunCellAsync("schema.domain", "AC2 fields/defaults/domains", DomainAsync).ConfigureAwait(false);

        await RunCellAsync("data.count-and-ids", "AC1 count/IDs", CountAndIdsAsync).ConfigureAwait(false);
        await RunCellAsync("data.int64-precision", "AC2 int64 precision", () => AttributesAsync("big_counter")).ConfigureAwait(false);
        await RunCellAsync("data.guid", "AC2 GUIDs preserved", () => AttributesAsync("site_guid")).ConfigureAwait(false);
        await RunCellAsync("data.null-versus-empty", "AC2 null versus empty values", () => AttributesAsync("name", "note")).ConfigureAwait(false);
        await RunCellAsync("data.temporal", "AC2 temporal types", () => AttributesAsync("inspected_at", "install_date")).ConfigureAwait(false);
        await RunCellAsync("data.numeric-and-coded", "AC2 numeric and domain-coded values", () => AttributesAsync("amount", "status_code")).ConfigureAwait(false);
        await RunCellAsync("geometry.null-versus-present", "AC2 null versus empty geometries", NullGeometryAsync).ConfigureAwait(false);
        await RunCellAsync("geometry.z-m", "AC2 Z/M preserved through the typed query API", ZmAsync).ConfigureAwait(false);

        await RunCellAsync("paging.exceeded-transfer-limit", "AC3 transfer limits", TransferLimitAsync).ConfigureAwait(false);
        await RunCellAsync("paging.offset-pages", "AC3 supportsPagination source: termination, no duplicates/omissions", () => OffsetPagesAsync(BaseUrl)).ConfigureAwait(false);
        await RunCellAsync("paging.object-id-batches", "AC3 stable ID batching", () => ObjectIdBatchesAsync(BaseUrl)).ConfigureAwait(false);
        await RunCellAsync("paging.offset-ignoring-source.offset-pages", "AC3 repeated/ignored offsets: termination, no duplicates/omissions", () => OffsetPagesAsync(new Uri(_args.Required("offset-ignoring-url")))).ConfigureAwait(false);
        await RunCellAsync("paging.offset-ignoring-source.object-id-batches", "AC3 repeated/ignored offsets: stable ID batching", () => ObjectIdBatchesAsync(new Uri(_args.Required("offset-ignoring-url")))).ConfigureAwait(false);

        await RunCellAsync("auth.none-rejected", "AC4 protected source without credentials fails explicitly", NoCredentialsAsync).ConfigureAwait(false);
        await RunCellAsync("auth.api-key", "AC4 Honua API key", ApiKeyAsync).ConfigureAwait(false);
        await RunCellAsync("auth.token", "AC4 ArcGIS token credential handler", () => CredentialAsync(ArcGisCredentialMode.Token)).ConfigureAwait(false);
        await RunCellAsync("auth.bearer", "AC4 bearer credential handler", () => CredentialAsync(ArcGisCredentialMode.Bearer)).ConfigureAwait(false);
        await RunCellAsync("auth.basic", "AC4 basic credential handler", () => CredentialAsync(ArcGisCredentialMode.Basic)).ConfigureAwait(false);
        await RunCellAsync("auth.invalid-token", "AC4 HTTP-200 Esri error envelope for an invalid token", InvalidTokenAsync).ConfigureAwait(false);
        await RunCellAsync("auth.basic-rejected", "AC4 wrong basic password fails explicitly", BasicRejectedAsync).ConfigureAwait(false);
        await RunCellAsync("errors.http200-envelope.missing-layer", "AC4 HTTP-200 Esri error envelope", MissingLayerAsync).ConfigureAwait(false);
        await RunCellAsync("errors.http200-envelope.invalid-where", "AC4 HTTP-200 Esri error envelope", InvalidWhereAsync).ConfigureAwait(false);
        await RunCellAsync("transport.injected-primary-handler", "AC4 injectable HTTP transport", InjectedTransportAsync).ConfigureAwait(false);
        await RunCellAsync("transport.cancellation", "AC4 cancellation preserved mid-enumeration", CancellationAsync).ConfigureAwait(false);
        await RunCellAsync("transport.bounded-streaming", "AC4 raw query response is streamed, not buffered", BoundedStreamingAsync).ConfigureAwait(false);
        await RunCellAsync("transport.retry-after-429", "AC4 429 Retry-After", RetryAfterAsync).ConfigureAwait(false);

        foreach (var released in _oracle["released"]?.AsArray() ?? [])
        {
            _results.Add(new CellResult(
                released!["cell"]!.GetValue<string>(),
                "released",
                "released",
                released["reason"]!.GetValue<string>()));
        }

        var failed = _results.Count(result => result.Verdict == "fail");
        WriteReceipt(failed);
        foreach (var result in _results)
        {
            Console.WriteLine($"{result.Verdict.ToUpperInvariant(),-8} {result.Id}: {result.Detail}");
        }

        Console.WriteLine(
            $"summary: pass={_results.Count(r => r.Verdict == "pass")} fail={failed} released={_results.Count(r => r.Verdict == "released")}");
        return failed == 0 ? 0 : 1;
    }

    private async Task RunCellAsync(string id, string criterion, Func<Task<string>> cell)
    {
        try
        {
            var detail = await cell().ConfigureAwait(false);
            _results.Add(new CellResult(id, criterion, "pass", detail));
        }
        catch (CellFailure failure)
        {
            _results.Add(new CellResult(id, criterion, "fail", failure.Message));
        }
        catch (Exception exception)
        {
            _results.Add(new CellResult(id, criterion, "fail", $"unexpected {exception.GetType().Name}: {exception.Message}"));
        }
    }

    // ── Clients ─────────────────────────────────────────────────────────

    private HttpMessageHandler CreateTransport()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        if (_tlsRoot is { } root)
        {
            handler.ServerCertificateCustomValidationCallback = (_, certificate, presentedChain, errors) =>
            {
                if (certificate is null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
                {
                    return false;
                }

                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(root);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                if (presentedChain is not null)
                {
                    foreach (var element in presentedChain.ChainElements)
                    {
                        chain.ChainPolicy.ExtraStore.Add(element.Certificate);
                    }
                }

                return chain.Build(certificate);
            };
        }

        return handler;
    }

    private (ServiceProvider Provider, IHonuaFeatureServerClient Client, TransportProbe Probe) CreateClient(
        Uri baseAddress,
        ArcGisSourceCredential? credential = null,
        bool useApiKey = true,
        Action<HonuaGeoServicesClientOptions>? configure = null)
    {
        var probe = new TransportProbe();
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = baseAddress;
            options.Timeout = TimeSpan.FromSeconds(60);
            if (useApiKey)
            {
                options.ApiKey = ApiKey;
            }

            options.PrimaryHttpMessageHandlerFactory = () =>
            {
                HttpMessageHandler recording = new RecordingHandler(probe, CreateTransport());
                return credential is null ? recording : new ArcGisSourceCredentialHandler(credential, recording);
            };
            configure?.Invoke(options);
        });
        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<IHonuaFeatureServerClient>(), probe);
    }

    private async Task<JsonNode> GetSourceWireAsync(string relativePath)
    {
        // Harness-side reference read of the source's own JSON; deliberately not
        // the SDK, so metadata cells compare the SDK against the source itself.
        using var http = new HttpClient(CreateTransport()) { BaseAddress = BaseUrl };
        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        request.Headers.Add("X-API-Key", ApiKey);
        using var response = await http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false))!;
    }

    private static CancellationTokenSource Timeout() => new(TimeSpan.FromMinutes(2));

    // ── Package ─────────────────────────────────────────────────────────

    private Task<string> PublishedPackageAsync()
    {
        var assembly = typeof(HonuaFeatureServerClient).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "(none)";
        var packageVersion = assembly.GetName().Version;
        var version = _args.Required("package-version");
        if (Path.IsPathRooted(version) || version.Contains(Path.DirectorySeparatorChar) || version.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new CellFailure($"--package-version '{version}' is not a safe path segment");
        }

        var packageDirectory = Path.Combine(Path.GetFullPath(_args.Required("packages-root")), "honua.sdk.geoservices", version.ToLowerInvariant());
        var hashFile = Path.Combine(packageDirectory, $"honua.sdk.geoservices.{version.ToLowerInvariant()}.nupkg.sha512");
        if (!File.Exists(hashFile))
        {
            throw new CellFailure($"restored package hash '{hashFile}' is missing");
        }

        var restoredHash = File.ReadAllText(hashFile).Trim();
        var expectedHash = _args.Required("expected-nupkg-sha512");
        if (!string.Equals(restoredHash, expectedHash, StringComparison.Ordinal))
        {
            throw new CellFailure($"restored nupkg sha512 {restoredHash} does not match the expected package hash {expectedHash}");
        }

        // The consumer runs from its build output, so the loaded assembly is a copy. Bind it
        // to the restored package by content, which also rules out a ProjectReference build.
        var packagedAssemblies = Directory.EnumerateFiles(Path.Combine(packageDirectory, "lib"), "Honua.Sdk.GeoServices.dll", SearchOption.AllDirectories)
            .Select(path => (Path: path, Hash: FileSha256(path)))
            .ToArray();
        var loadedHash = FileSha256(assembly.Location);
        var match = packagedAssemblies.FirstOrDefault(candidate => candidate.Hash == loadedHash);
        if (match.Path is null)
        {
            throw new CellFailure($"loaded Honua.Sdk.GeoServices.dll sha256 {loadedHash} matches no assembly inside the restored package [{string.Join(", ", packagedAssemblies.Select(a => $"{a.Path}={a.Hash}"))}]");
        }

        return Task.FromResult(
            $"Honua.Sdk.GeoServices {version} (assembly {packageVersion}, informational {informational}) from {_args.Required("package-source")}; " +
            $"nupkg sha512 matches the expected package hash; loaded assembly sha256 {loadedHash} equals {Path.GetRelativePath(packageDirectory, match.Path)} in the restored package");
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    // ── Discovery and metadata ──────────────────────────────────────────

    private async Task<string> RawMetadataAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            var sourceClient = (HonuaFeatureServerClient)client;
            using var timeout = Timeout();
            using var service = await sourceClient.GetServiceMetadataAsync(_serviceName, timeout.Token).ConfigureAwait(false);
            using var layer = await sourceClient.GetLayerMetadataAsync(_serviceName, _layerId, timeout.Token).ConfigureAwait(false);
            var serviceWire = await GetSourceWireAsync($"/rest/services/{_serviceName}/FeatureServer?f=json").ConfigureAwait(false);
            var layerWire = await GetSourceWireAsync($"/rest/services/{_serviceName}/FeatureServer/{_layerId}?f=json").ConfigureAwait(false);
            if (!JsonNode.DeepEquals(serviceWire, JsonNode.Parse(service.RootElement.GetRawText()))
                || !JsonNode.DeepEquals(layerWire, JsonNode.Parse(layer.RootElement.GetRawText())))
            {
                throw new CellFailure("raw metadata differs from independently fetched source JSON, including member presence");
            }

            return "service and layer raw metadata equal the source, including explicit nulls and absent members";
        }
    }

    private async Task<string> ServiceLayersAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var service = await client.GetServiceInfoAsync(_serviceName, timeout.Token).ConfigureAwait(false);
            var expectedName = _oracle["layerName"]!.GetValue<string>();
            if (service.Layers?.Any(layer => layer.Id == _layerId && layer.Name == expectedName) != true)
            {
                throw new CellFailure($"layer {_layerId} '{expectedName}' is not listed; saw [{string.Join(", ", service.Layers?.Select(l => $"{l.Id}:{l.Name}") ?? [])}]");
            }

            return $"service lists layer {_layerId} '{expectedName}'";
        }
    }

    private async Task<string> ServiceMetadataPathAsync(string path)
    {
        var wire = await GetSourceWireAsync($"/rest/services/{_serviceName}/FeatureServer?f=json").ConfigureAwait(false);
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var typed = await client.GetServiceInfoAsync(_serviceName, timeout.Token).ConfigureAwait(false);
            return CompareMetadata(wire, JsonSerializer.SerializeToNode(typed, ModelJson), path, requireSourceKey: path != "$");
        }
    }

    private async Task<string> LayerMetadataPathAsync(string path, bool requireSourceKey)
    {
        var wire = await GetSourceWireAsync($"/rest/services/{_serviceName}/FeatureServer/{_layerId}?f=json").ConfigureAwait(false);
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var typed = await client.GetLayerInfoAsync(_serviceName, _layerId, timeout.Token).ConfigureAwait(false);
            return CompareMetadata(wire, JsonSerializer.SerializeToNode(typed, ModelJson), path, requireSourceKey);
        }
    }

    private static string CompareMetadata(JsonNode wire, JsonNode? decoded, string path, bool requireSourceKey)
    {
        var loss = new List<string>();
        MetadataDiff.Collect(wire, decoded, "$", loss);
        var scoped = path == "$"
            ? loss
            : loss.Where(entry => entry.StartsWith(path + ".", StringComparison.Ordinal)
                || entry.StartsWith(path + "[", StringComparison.Ordinal)
                || entry.StartsWith(path + ":", StringComparison.Ordinal)).ToList();
        if (requireSourceKey && path != "$" && MetadataDiff.Select(wire, path) is null)
        {
            throw new CellFailure($"source wire has no {path}; the cell cannot distinguish preservation from absence");
        }

        if (scoped.Count > 0)
        {
            throw new CellFailure($"{scoped.Count} source JSON path(s) lost by the typed model: {string.Join("; ", scoped.Take(40))}");
        }

        return $"every source JSON path under {path} survives the typed model";
    }

    private async Task<string> LayerTimeInfoAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var layer = await client.GetLayerInfoAsync(_serviceName, _layerId, timeout.Token).ConfigureAwait(false);
            var expected = _oracle["timeInfo"]!;
            if (layer.TimeInfo?.StartTimeField != expected["startTimeField"]!.GetValue<string>()
                || layer.TimeInfo?.EndTimeField != expected["endTimeField"]!.GetValue<string>())
            {
                throw new CellFailure($"timeInfo start/end = {layer.TimeInfo?.StartTimeField}/{layer.TimeInfo?.EndTimeField}");
            }
        }

        return await LayerMetadataPathAsync("$.timeInfo", requireSourceKey: true).ConfigureAwait(false);
    }

    private async Task<string> PaginationCapabilitiesAsync()
    {
        var first = await LayerMetadataPathAsync("$.supportsPagination", requireSourceKey: true).ConfigureAwait(false);
        var second = await LayerMetadataPathAsync("$.advancedQueryCapabilities", requireSourceKey: true).ConfigureAwait(false);
        return $"{first}; {second}";
    }

    private async Task<string> ObjectIdFieldAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var layer = await client.GetLayerInfoAsync(_serviceName, _layerId, timeout.Token).ConfigureAwait(false);
            var expected = _oracle["objectIdField"]!.GetValue<string>();
            return layer.ObjectIdField == expected
                ? $"objectIdField = {expected}"
                : throw new CellFailure($"objectIdField = {layer.ObjectIdField}, expected {expected}");
        }
    }

    private async Task<string> FieldTypesAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var layer = await client.GetLayerInfoAsync(_serviceName, _layerId, timeout.Token).ConfigureAwait(false);
            var mismatches = new List<string>();
            foreach (var (name, type) in _oracle["fieldTypes"]!.AsObject())
            {
                var field = layer.Fields?.FirstOrDefault(f => f.Name == name);
                if (field?.Type != type!.GetValue<string>())
                {
                    mismatches.Add($"{name}: {field?.Type ?? "(missing)"} != {type}");
                }
            }

            return mismatches.Count == 0
                ? $"{_oracle["fieldTypes"]!.AsObject().Count} field types match the source contract"
                : throw new CellFailure(string.Join("; ", mismatches));
        }
    }

    private async Task<string> DomainAsync()
    {
        var expected = _oracle["domain"]!;
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var layer = await client.GetLayerInfoAsync(_serviceName, _layerId, timeout.Token).ConfigureAwait(false);
            var field = layer.Fields?.FirstOrDefault(f => f.Name == expected["field"]!.GetValue<string>())
                ?? throw new CellFailure("domain field is missing");
            if (field.Alias != expected["alias"]!.GetValue<string>())
            {
                throw new CellFailure($"alias = {field.Alias}");
            }

            if (field.Domain is not { ValueKind: JsonValueKind.Object } domain)
            {
                throw new CellFailure("domain was not preserved");
            }

            var actual = JsonNode.Parse(domain.GetRawText())!;
            var expectedDomain = new JsonObject
            {
                ["type"] = expected["type"]!.DeepClone(),
                ["name"] = expected["name"]!.DeepClone(),
                ["codedValues"] = expected["codedValues"]!.DeepClone(),
            };
            var loss = new List<string>();
            MetadataDiff.Collect(expectedDomain, actual, "$.domain", loss);
            return loss.Count == 0
                ? $"coded-value domain {expected["name"]} preserved with every code/name pair"
                : throw new CellFailure(string.Join("; ", loss));
        }
    }

    // ── Data ────────────────────────────────────────────────────────────

    private async Task<string> CountAndIdsAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            var query = new FeatureServerQueryParams { Where = "1=1" };
            using var countTimeout = Timeout();
            var count = await client.QueryCountAsync(_serviceName, _layerId, query, countTimeout.Token).ConfigureAwait(false);
            using var idsTimeout = Timeout();
            var ids = await client.QueryIdsAsync(_serviceName, _layerId, query, idsTimeout.Token).ConfigureAwait(false);
            if (count != _objectIds.Length)
            {
                throw new CellFailure($"count = {count}, expected {_objectIds.Length}");
            }

            var sorted = ids.Order().ToArray();
            return sorted.SequenceEqual(_objectIds)
                ? $"count {count}; {ids.Count} object IDs equal the fixture set"
                : throw new CellFailure($"object IDs [{string.Join(",", sorted)}] differ from the fixture set");
        }
    }

    private async Task<Dictionary<long, FeatureServerFeature>> QueryOracleRowsAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            var rowIds = _oracle["rows"]!.AsObject().Select(row => long.Parse(row.Key, CultureInfo.InvariantCulture)).ToArray();
            using var timeout = Timeout();
            var response = await client.QueryAsync(
                _serviceName,
                _layerId,
                RequestZm(new FeatureServerQueryParams
                {
                    ObjectIds = rowIds,
                    OutFields = "*",
                    ReturnGeometry = true,
                    OrderByFields = "objectid",
                }),
                timeout.Token).ConfigureAwait(false);
            var rows = new Dictionary<long, FeatureServerFeature>();
            foreach (var feature in response.Features ?? [])
            {
                if (feature.Attributes?.TryGetValue("objectid", out var id) == true && id.TryGetInt64(out var objectId))
                {
                    rows[objectId] = feature;
                }
            }

            var missing = rowIds.Where(id => !rows.ContainsKey(id)).ToArray();
            return missing.Length == 0
                ? rows
                : throw new CellFailure($"rows [{string.Join(",", missing)}] were not returned");
        }
    }

    // Sources omit Z/M unless returnZ/returnM is requested. Packages that expose the typed
    // ReturnZ/ReturnM query parameters get them set; older packages cannot request Z/M at all,
    // which geometry.z-m reports. Reflection keeps one consumer compilable against both.
    private static readonly PropertyInfo? ReturnZProperty = typeof(FeatureServerQueryParams).GetProperty("ReturnZ");
    private static readonly PropertyInfo? ReturnMProperty = typeof(FeatureServerQueryParams).GetProperty("ReturnM");

    private static FeatureServerQueryParams RequestZm(FeatureServerQueryParams query)
    {
        ReturnZProperty?.SetValue(query, true);
        ReturnMProperty?.SetValue(query, true);
        return query;
    }

    private async Task<string> AttributesAsync(params string[] names)
    {
        var rows = await QueryOracleRowsAsync().ConfigureAwait(false);
        var mismatches = new List<string>();
        var compared = 0;
        foreach (var (key, row) in _oracle["rows"]!.AsObject())
        {
            var feature = rows[long.Parse(key, CultureInfo.InvariantCulture)];
            var attributes = row!["attributes"]!.AsObject();
            foreach (var name in names)
            {
                if (!attributes.TryGetPropertyValue(name, out var expected))
                {
                    continue;
                }

                compared++;
                if (feature.Attributes is null || !feature.Attributes.TryGetValue(name, out var actual))
                {
                    mismatches.Add($"row {key} {name}: key dropped");
                    continue;
                }

                if (!ValueEquals(expected, actual))
                {
                    mismatches.Add($"row {key} {name}: expected {expected?.ToJsonString() ?? "null"}, got {actual.GetRawText()}");
                }
            }
        }

        return mismatches.Count == 0
            ? $"{compared} attribute value(s) preserved exactly"
            : throw new CellFailure(string.Join("; ", mismatches));
    }

    private static bool ValueEquals(JsonNode? expected, JsonElement actual)
    {
        if (expected is null)
        {
            return actual.ValueKind == JsonValueKind.Null;
        }

        switch (expected.GetValueKind())
        {
            case JsonValueKind.String:
                return actual.ValueKind == JsonValueKind.String
                    && string.Equals(expected.GetValue<string>(), actual.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number when actual.ValueKind == JsonValueKind.Number:
                var text = expected.ToJsonString();
                if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var expectedInteger))
                {
                    return actual.TryGetInt64(out var actualInteger) && actualInteger == expectedInteger;
                }

                return double.Parse(text, CultureInfo.InvariantCulture).Equals(actual.GetDouble());
            case JsonValueKind.True:
            case JsonValueKind.False:
                return actual.ValueKind == expected.GetValueKind();
            default:
                return false;
        }
    }

    private async Task<string> NullGeometryAsync()
    {
        var rows = await QueryOracleRowsAsync().ConfigureAwait(false);
        var mismatches = new List<string>();
        foreach (var (key, row) in _oracle["rows"]!.AsObject())
        {
            var geometry = rows[long.Parse(key, CultureInfo.InvariantCulture)].Geometry;
            var expectNull = row!["geometry"] is null;
            var isNull = geometry is null || geometry.Value.ValueKind == JsonValueKind.Null;
            if (expectNull != isNull || (!isNull && geometry!.Value.ValueKind != JsonValueKind.Object))
            {
                mismatches.Add($"row {key}: expected {(expectNull ? "null" : "geometry object")}, got {geometry?.GetRawText() ?? "(absent)"}");
            }
        }

        return mismatches.Count == 0
            ? "null geometry stays null and present geometries stay objects"
            : throw new CellFailure(string.Join("; ", mismatches));
    }

    private async Task<string> ZmAsync()
    {
        var rows = await QueryOracleRowsAsync().ConfigureAwait(false);
        var mismatches = new List<string>();
        var compared = 0;
        foreach (var (key, row) in _oracle["rows"]!.AsObject())
        {
            if (row!["geometry"] is not JsonObject expected)
            {
                continue;
            }

            var geometry = rows[long.Parse(key, CultureInfo.InvariantCulture)].Geometry;
            foreach (var (ordinate, value) in expected)
            {
                compared++;
                if (geometry is not { ValueKind: JsonValueKind.Object } actual
                    || !actual.TryGetProperty(ordinate, out var actualOrdinate)
                    || actualOrdinate.ValueKind != JsonValueKind.Number
                    || !actualOrdinate.GetDouble().Equals(value!.GetValue<double>()))
                {
                    mismatches.Add($"row {key} {ordinate}: expected {value}, got {(geometry is { ValueKind: JsonValueKind.Object } g && g.TryGetProperty(ordinate, out var o) ? o.GetRawText() : "(dropped)")}");
                }
            }
        }

        var requestable = ReturnZProperty is not null && ReturnMProperty is not null
            ? "typed ReturnZ/ReturnM requested"
            : "this package has no typed ReturnZ/ReturnM query parameter, so Z/M cannot be requested";
        return mismatches.Count == 0
            ? $"{compared} X/Y/Z/M ordinate(s) preserved exactly; {requestable}"
            : throw new CellFailure($"{mismatches.Count} ordinate(s) lost through the typed query API ({requestable}): {string.Join("; ", mismatches)}");
    }

    // ── Paging ──────────────────────────────────────────────────────────

    private static FeatureServerQueryParams IdPageQuery(int pageSize) => new()
    {
        Where = "1=1",
        OutFields = "objectid",
        ReturnGeometry = false,
        OrderByFields = "objectid",
        ResultRecordCount = pageSize,
    };

    private static IEnumerable<long> ObjectIdsOf(FeatureServerQueryResponse page) =>
        (page.Features ?? []).Select(feature => feature.Attributes!["objectid"].GetInt64());

    private async Task<string> TransferLimitAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var page = await client.QueryAsync(_serviceName, _layerId, IdPageQuery(10), timeout.Token).ConfigureAwait(false);
            return page.ExceededTransferLimit && page.Features?.Count == 10
                ? "a 10-record window over 25 rows reports exceededTransferLimit=true with 10 features"
                : throw new CellFailure($"exceededTransferLimit={page.ExceededTransferLimit}, features={page.Features?.Count}");
        }
    }

    private async Task<string> OffsetPagesAsync(Uri source)
    {
        var (provider, client, probe) = CreateClient(source);
        using (provider)
        {
            var seen = new List<long>();
            var pages = 0;
            Exception? terminal = null;
            using var timeout = Timeout();
            try
            {
                await foreach (var page in client.QueryPagesAsync(_serviceName, _layerId, IdPageQuery(10), timeout.Token).ConfigureAwait(false))
                {
                    pages++;
                    seen.AddRange(ObjectIdsOf(page));
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                terminal = exception;
            }

            var duplicates = seen.Count - seen.Distinct().Count();
            var omitted = _objectIds.Except(seen).Count();
            var summary = $"{pages} page(s), {probe.Requests} request(s), {seen.Count} record(s) yielded, {duplicates} duplicate(s), {omitted} omitted";
            if (duplicates > 0)
            {
                throw new CellFailure($"{summary}; terminal={terminal?.GetType().Name ?? "none"}");
            }

            if (terminal is not null)
            {
                return $"{summary}; failed explicitly with {terminal.GetType().Name} before yielding a duplicate";
            }

            return omitted == 0 && seen.SequenceEqual(_objectIds)
                ? $"{summary}; terminated with the complete ordered set"
                : throw new CellFailure($"{summary}; completed silently without the full set (lossy success)");
        }
    }

    private async Task<string> ObjectIdBatchesAsync(Uri source)
    {
        var (provider, client, probe) = CreateClient(source);
        using (provider)
        {
            var seen = new List<long>();
            var batches = 0;
            using var timeout = Timeout();
            await foreach (var batch in client.QueryAllFeaturesByObjectIdBatchesAsync(
                _serviceName,
                _layerId,
                new FeatureServerQueryParams { Where = "1=1", OutFields = "objectid", ReturnGeometry = false },
                batchSize: 7,
                timeout.Token).ConfigureAwait(false))
            {
                batches++;
                if (batch.ExceededTransferLimit)
                {
                    throw new CellFailure($"batch {batches} reported exceededTransferLimit");
                }

                seen.AddRange(ObjectIdsOf(batch));
            }

            var duplicates = seen.Count - seen.Distinct().Count();
            var sorted = seen.Order().ToArray();
            var summary = $"{batches} batch(es) of <=7, {probe.Requests} request(s), {seen.Count} record(s), {duplicates} duplicate(s)";
            return duplicates == 0 && sorted.SequenceEqual(_objectIds)
                ? $"{summary}; complete fixture set"
                : throw new CellFailure($"{summary}; set differs from the fixture");
        }
    }

    // ── Authentication, errors, transport ───────────────────────────────

    private async Task<string> NoCredentialsAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl, useApiKey: false);
        using (provider)
        {
            using var timeout = Timeout();
            try
            {
                var count = await client.QueryCountAsync(_serviceName, _layerId, new FeatureServerQueryParams { Where = "1=1" }, timeout.Token).ConfigureAwait(false);
                throw new CellFailure($"an unauthenticated query succeeded with count {count}");
            }
            catch (HonuaFeatureServerException exception)
            {
                return exception.GeoServicesErrorCode is 499 or 401 or 403 || exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? $"HonuaFeatureServerException http={(int)exception.StatusCode} esriCode={exception.GeoServicesErrorCode}"
                    : throw new CellFailure($"unexpected rejection http={(int)exception.StatusCode} esriCode={exception.GeoServicesErrorCode}");
            }
        }
    }

    private async Task<string> ApiKeyAsync()
    {
        var (provider, client, probe) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            var count = await client.QueryCountAsync(_serviceName, _layerId, new FeatureServerQueryParams { Where = "1=1" }, timeout.Token).ConfigureAwait(false);
            return count == _objectIds.Length
                ? $"count {count}; {probe.Observations.Last()}"
                : throw new CellFailure($"count {count}");
        }
    }

    private async Task<string> CredentialAsync(ArcGisCredentialMode mode)
    {
        var credential = mode switch
        {
            ArcGisCredentialMode.Token => new ArcGisSourceCredential { Mode = mode, Token = _args.Required("portal-token") },
            ArcGisCredentialMode.Bearer => new ArcGisSourceCredential { Mode = mode, BearerToken = _args.Required("portal-token") },
            ArcGisCredentialMode.Basic => new ArcGisSourceCredential { Mode = mode, Username = _args.Required("basic-user"), Password = _args.Required("basic-password") },
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        var source = new Uri(_args.Required(mode == ArcGisCredentialMode.Basic ? "basic-url" : "tls-url"));
        var (provider, client, probe) = CreateClient(source, credential, useApiKey: false);
        using (provider)
        {
            using var countTimeout = Timeout();
            var count = await client.QueryCountAsync(_serviceName, _layerId, new FeatureServerQueryParams { Where = "1=1" }, countTimeout.Token).ConfigureAwait(false);
            using var layerTimeout = Timeout();
            var layer = await client.GetLayerInfoAsync(_serviceName, _layerId, layerTimeout.Token).ConfigureAwait(false);
            if (count != _objectIds.Length || layer.Id != _layerId)
            {
                throw new CellFailure($"count {count}, layer {layer.Id}");
            }

            var expectedMarker = mode switch
            {
                ArcGisCredentialMode.Token => "token-param=True authorization=none x-api-key=False",
                ArcGisCredentialMode.Bearer => "token-param=False authorization=Bearer x-api-key=False",
                _ => "token-param=False authorization=Basic x-api-key=False",
            };
            return probe.Observations.All(observation => observation.EndsWith(expectedMarker, StringComparison.Ordinal))
                ? $"count {count} over {source.Scheme}; {probe.Requests} request(s) all carried [{expectedMarker}]"
                : throw new CellFailure($"credential not applied on every request: {string.Join(" | ", probe.Observations)}");
        }
    }

    private async Task<string> InvalidTokenAsync()
    {
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Token, Token = "lane-341-not-a-token" };
        var (provider, client, _) = CreateClient(new Uri(_args.Required("tls-url")), credential, useApiKey: false);
        using (provider)
        {
            using var timeout = Timeout();
            try
            {
                await client.QueryCountAsync(_serviceName, _layerId, new FeatureServerQueryParams { Where = "1=1" }, timeout.Token).ConfigureAwait(false);
                throw new CellFailure("an invalid token was accepted");
            }
            catch (HonuaFeatureServerException exception) when (exception.GeoServicesErrorCode == 498)
            {
                return $"HonuaFeatureServerException http={(int)exception.StatusCode} esriCode=498 details={exception.Details?.Count ?? 0}";
            }
        }
    }

    private async Task<string> BasicRejectedAsync()
    {
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Basic, Username = _args.Required("basic-user"), Password = "wrong-password" };
        var (provider, client, _) = CreateClient(new Uri(_args.Required("basic-url")), credential, useApiKey: false);
        using (provider)
        {
            using var timeout = Timeout();
            try
            {
                await client.QueryCountAsync(_serviceName, _layerId, new FeatureServerQueryParams { Where = "1=1" }, timeout.Token).ConfigureAwait(false);
                throw new CellFailure("a wrong basic password was accepted");
            }
            catch (HonuaFeatureServerException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                return "HonuaFeatureServerException http=401";
            }
        }
    }

    private async Task<string> MissingLayerAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            try
            {
                await client.GetLayerInfoAsync(_serviceName, 99, timeout.Token).ConfigureAwait(false);
                throw new CellFailure("a missing layer was returned as success");
            }
            catch (HonuaFeatureServerException exception) when (exception.GeoServicesErrorCode == 404 && exception.Details is { Count: > 0 })
            {
                return $"HonuaFeatureServerException http={(int)exception.StatusCode} esriCode=404 details={exception.Details.Count}";
            }
        }
    }

    private async Task<string> InvalidWhereAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            try
            {
                await client.QueryAsync(_serviceName, _layerId, new FeatureServerQueryParams { Where = "no_such_column=1" }, timeout.Token).ConfigureAwait(false);
                throw new CellFailure("an invalid where clause was returned as success");
            }
            catch (HonuaFeatureServerException exception) when (exception.GeoServicesErrorCode == 400)
            {
                return $"HonuaFeatureServerException http={(int)exception.StatusCode} esriCode=400 details={exception.Details?.Count ?? 0}";
            }
        }
    }

    private async Task<string> InjectedTransportAsync()
    {
        var (provider, client, probe) = CreateClient(BaseUrl);
        using (provider)
        {
            using var serviceTimeout = Timeout();
            await client.GetServiceInfoAsync(_serviceName, serviceTimeout.Token).ConfigureAwait(false);
            using var layerTimeout = Timeout();
            await client.GetLayerInfoAsync(_serviceName, _layerId, layerTimeout.Token).ConfigureAwait(false);
            return probe.Requests == 2
                ? $"both requests traversed the injected primary handler: {string.Join(" | ", probe.Observations)}"
                : throw new CellFailure($"injected handler saw {probe.Requests} request(s)");
        }
    }

    private async Task<string> CancellationAsync()
    {
        var (provider, client, probe) = CreateClient(BaseUrl);
        using (provider)
        {
            using var cancellation = new CancellationTokenSource();
            var pages = 0;
            try
            {
                await foreach (var _ in client.QueryPagesAsync(_serviceName, _layerId, IdPageQuery(10), cancellation.Token).ConfigureAwait(false))
                {
                    pages++;
                    await cancellation.CancelAsync().ConfigureAwait(false);
                }

                throw new CellFailure($"enumeration completed after cancellation ({pages} pages, {probe.Requests} requests)");
            }
            catch (OperationCanceledException)
            {
                return pages == 1 && probe.Requests == 1
                    ? "OperationCanceledException after the first page; no further request was sent"
                    : throw new CellFailure($"cancelled after {pages} page(s) and {probe.Requests} request(s)");
            }
        }
    }

    private async Task<string> BoundedStreamingAsync()
    {
        var (provider, client, _) = CreateClient(BaseUrl);
        using (provider)
        {
            using var timeout = Timeout();
            using var response = await client.QueryRawAsync(
                _serviceName,
                _layerId,
                new FeatureServerQueryParams { Where = "1=1", OutFields = "*" },
                timeout.Token).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return !stream.CanSeek
                ? $"raw query content is a forward-only network stream ({stream.GetType().Name})"
                : throw new CellFailure($"raw query content was buffered into a seekable {stream.GetType().Name}");
        }
    }

    private async Task<string> RetryAfterAsync()
    {
        var (provider, client, probe) = CreateClient(
            new Uri(_args.Required("throttled-url")),
            configure: options => options.MaxRetryAttempts = 2);
        using (provider)
        {
            using var timeout = Timeout();
            try
            {
                await client.QueryCountAsync(_serviceName, _layerId, new FeatureServerQueryParams { Where = "1=1" }, timeout.Token).ConfigureAwait(false);
                throw new CellFailure("a throttled source returned success");
            }
            catch (HonuaFeatureServerException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return exception.RetryAfter == TimeSpan.FromSeconds(1)
                    ? $"HonuaFeatureServerException http=429 RetryAfter={exception.RetryAfter} after {probe.Requests} attempt(s)"
                    : throw new CellFailure($"RetryAfter={exception.RetryAfter?.ToString() ?? "null"} after {probe.Requests} attempt(s)");
            }
        }
    }

    // ── Receipt ─────────────────────────────────────────────────────────

    private void WriteReceipt(int failed)
    {
        var receipt = new JsonObject
        {
            ["schema"] = "honua-sdk-dotnet.source-import-certification.v1",
            ["issue"] = "https://github.com/honua-io/honua-sdk-dotnet/issues/341",
            ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["fixtureVersion"] = _oracle["fixtureVersion"]!.DeepClone(),
            ["negativeControl"] = _args.Flag("corrupt-oracle"),
            ["package"] = new JsonObject
            {
                ["id"] = "Honua.Sdk.GeoServices",
                ["version"] = _args.Required("package-version"),
                ["source"] = _args.Required("package-source"),
                ["nupkgSha512"] = _args.Required("expected-nupkg-sha512"),
                ["informationalVersion"] = typeof(HonuaFeatureServerClient).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            },
            ["source"] = new JsonObject
            {
                ["image"] = _args.Optional("server-image"),
                ["sourceSha"] = _args.Optional("server-source-sha"),
                ["service"] = _serviceName,
                ["layerId"] = _layerId,
            },
            ["summary"] = new JsonObject
            {
                ["pass"] = _results.Count(r => r.Verdict == "pass"),
                ["fail"] = failed,
                ["released"] = _results.Count(r => r.Verdict == "released"),
                ["verdict"] = failed == 0 ? "certified" : "not-certified",
            },
            ["cells"] = new JsonArray(_results.Select(result => (JsonNode)new JsonObject
            {
                ["id"] = result.Id,
                ["criterion"] = result.Criterion,
                ["verdict"] = result.Verdict,
                ["detail"] = result.Detail,
            }).ToArray()),
        };
        var path = _args.Required("receipt");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, receipt.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}

internal static class MetadataDiff
{
    public static void Collect(JsonNode? source, JsonNode? decoded, string path, List<string> loss)
    {
        switch (source)
        {
            case null:
                if (decoded is not null && decoded.GetValueKind() != JsonValueKind.Null)
                {
                    loss.Add($"{path}: null became {decoded.ToJsonString()}");
                }

                return;
            case JsonObject sourceObject:
                if (decoded is not JsonObject decodedObject)
                {
                    loss.Add($"{path}: object became {decoded?.ToJsonString() ?? "(dropped)"}");
                    return;
                }

                foreach (var (key, value) in sourceObject)
                {
                    if (!decodedObject.TryGetPropertyValue(key, out var decodedValue))
                    {
                        loss.Add($"{path}.{key}: dropped");
                        continue;
                    }

                    Collect(value, decodedValue, $"{path}.{key}", loss);
                }

                return;
            case JsonArray sourceArray:
                if (decoded is not JsonArray decodedArray || decodedArray.Count != sourceArray.Count)
                {
                    loss.Add($"{path}: array of {sourceArray.Count} became {decoded?.ToJsonString() ?? "(dropped)"}");
                    return;
                }

                for (var i = 0; i < sourceArray.Count; i++)
                {
                    Collect(sourceArray[i], decodedArray[i], $"{path}[{i}]", loss);
                }

                return;
            default:
                if (!ScalarEquals(source, decoded))
                {
                    loss.Add($"{path}: {source.ToJsonString()} became {decoded?.ToJsonString() ?? "(dropped)"}");
                }

                return;
        }
    }

    public static JsonNode? Select(JsonNode root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in path.Split('.').Skip(1))
        {
            current = current is JsonObject obj && obj.TryGetPropertyValue(segment, out var next) ? next : null;
        }

        return current;
    }

    private static bool ScalarEquals(JsonNode source, JsonNode? decoded)
    {
        if (decoded is null)
        {
            return false;
        }

        var kind = source.GetValueKind();
        if (kind != decoded.GetValueKind())
        {
            return false;
        }

        return kind switch
        {
            JsonValueKind.String => string.Equals(source.GetValue<string>(), decoded.GetValue<string>(), StringComparison.Ordinal),
            JsonValueKind.Number => decimal.Parse(source.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture)
                == decimal.Parse(decoded.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture),
            _ => true,
        };
    }
}
