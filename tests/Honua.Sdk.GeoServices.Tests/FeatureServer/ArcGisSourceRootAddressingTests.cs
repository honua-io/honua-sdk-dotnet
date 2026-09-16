// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

/// <summary>
/// honua-sdk-dotnet#373: a source client addresses an arbitrary ArcGIS service root (path prefix, folders,
/// FeatureServer or MapServer) for every discovery, query, queryAttachments and attachment request. Each
/// case drives the client through a capturing handler and asserts the exact URLs sent, in the format of the
/// issue's evidence block.
/// </summary>
public sealed class ArcGisSourceRootAddressingTests
{
    public static TheoryData<string, string?, string, ArcGisServiceType, string> SourceShapes => new()
    {
        // label, HttpClient.BaseAddress, serviceId, service type, expected service root
        { "ArcGIS Online hosted (/<org>/arcgis prefix)", "https://services.arcgis.com/Org123/arcgis/", "Hydrants", ArcGisServiceType.FeatureServer, "https://services.arcgis.com/Org123/arcgis/rest/services/Hydrants/FeatureServer" },
        { "ArcGIS Enterprise web adaptor (/arcgis prefix)", "https://gis.example.com/arcgis/", "Inspections", ArcGisServiceType.FeatureServer, "https://gis.example.com/arcgis/rest/services/Inspections/FeatureServer" },
        { "Web adaptor base address without trailing slash", "https://gis.example.com/arcgis", "Inspections", ArcGisServiceType.FeatureServer, "https://gis.example.com/arcgis/rest/services/Inspections/FeatureServer" },
        { "Foldered service (Utilities/Water)", "https://gis.example.com/", "Utilities/Water", ArcGisServiceType.FeatureServer, "https://gis.example.com/rest/services/Utilities/Water/FeatureServer" },
        { "MapServer source (MapServer-MixedRenderers)", "https://gis.example.com/arcgis/", "Fixtures/MapServer-MixedRenderers", ArcGisServiceType.MapServer, "https://gis.example.com/arcgis/rest/services/Fixtures/MapServer-MixedRenderers/MapServer" },
    };

    [Theory]
    [MemberData(nameof(SourceShapes))]
    public async Task EveryRequest_ResolvesUnderTheServiceRoot(
        string label, string? baseAddress, string serviceId, ArcGisServiceType serviceType, string serviceRoot)
    {
        var sent = new List<string>();
        var client = CreateClient(sent, baseAddress, new HonuaFeatureServerClientOptions { ServiceType = serviceType });

        await DriveEveryCallAsync(client, serviceId);

        Assert.True(sent.Count == 17, $"[{label}] expected one request per call (17); sent {sent.Count}.");
        Assert.All(sent, url => Assert.True(
            url.Replace("POST ", string.Empty, StringComparison.Ordinal).StartsWith(serviceRoot + "?", StringComparison.Ordinal) ||
            url.Replace("POST ", string.Empty, StringComparison.Ordinal).StartsWith(serviceRoot + "/0", StringComparison.Ordinal),
            $"[{label}] sent {url} outside {serviceRoot}"));
        AssertExpectedUrls(serviceRoot, sent);
    }

    [Fact]
    public async Task ServiceRootUrl_AddressesTheSourceWithoutABaseAddress()
    {
        var root = ArcGisServiceRoot.Parse(new Uri("https://services.arcgis.com/Org123/arcgis/rest/services/Utilities/Water/MapServer?f=pjson"));
        var sent = new List<string>();
        var client = CreateClient(sent, baseAddress: null, root.ToClientOptions());

        await DriveEveryCallAsync(client, root.ServiceId);

        AssertExpectedUrls("https://services.arcgis.com/Org123/arcgis/rest/services/Utilities/Water/MapServer", sent);
    }

    [Fact]
    public async Task RootAddress_TakesPrecedenceOverTheHttpClientBaseAddress()
    {
        var sent = new List<string>();
        var client = CreateClient(
            sent,
            "https://honua.example.com/",
            new HonuaFeatureServerClientOptions { RootAddress = new Uri("https://gis.example.com/arcgis") });

        await client.GetLayerInfoAsync("Inspections", 0);

        Assert.Equal(["https://gis.example.com/arcgis/rest/services/Inspections/FeatureServer/0?f=json"], sent);
    }

    [Fact]
    public async Task EmptyServicesPath_PlacesTheServiceIdDirectlyUnderTheRoot()
    {
        var sent = new List<string>();
        var client = CreateClient(
            sent,
            "https://proxy.example.com/sources/",
            new HonuaFeatureServerClientOptions { ServicesPath = string.Empty });

        await client.GetServiceInfoAsync("Hydrants");

        Assert.Equal(["https://proxy.example.com/sources/Hydrants/FeatureServer?f=json"], sent);
    }

    [Fact]
    public async Task DefaultClient_KeepsTheHonuaServerAddressing()
    {
        var sent = new List<string>();
        var client = CreateClient(sent, "http://localhost:5000", options: null);

        await client.GetLayerInfoAsync("parks", 0);

        Assert.Equal(["http://localhost:5000/rest/services/parks/FeatureServer/0?f=json"], sent);
    }

    [Fact]
    public async Task FolderSegments_AreEscapedIndividually()
    {
        var sent = new List<string>();
        var client = CreateClient(sent, "https://gis.example.com/arcgis/", options: null);

        await client.GetServiceInfoAsync("Public Works/Water Mains#2");

        Assert.Equal(["https://gis.example.com/arcgis/rest/services/Public%20Works/Water%20Mains%232/FeatureServer?f=json"], sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/Hydrants")]
    [InlineData("Utilities//Water")]
    [InlineData("Utilities/")]
    [InlineData("..")]
    [InlineData("Utilities/../../admin")]
    [InlineData("./Hydrants")]
    public async Task InvalidServiceIds_AreRejectedBeforeAnyRequest(string serviceId)
    {
        var sent = new List<string>();
        var client = CreateClient(sent, "https://gis.example.com/arcgis/", options: null);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetServiceInfoAsync(serviceId));
        Assert.Empty(sent);
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        using var http = new HttpClient();

        Assert.Throws<ArgumentNullException>(() => new HonuaFeatureServerClient(http, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HonuaFeatureServerClient(http, new HonuaFeatureServerClientOptions { MaxResponseBytes = 0 }));
        Assert.Throws<ArgumentException>(() => new HonuaFeatureServerClient(http, new HonuaFeatureServerClientOptions { RootAddress = new Uri("arcgis/", UriKind.Relative) }));
        Assert.Throws<ArgumentException>(() => new HonuaFeatureServerClient(http, new HonuaFeatureServerClientOptions { ServiceType = (ArcGisServiceType)42 }));
    }

    // ── ArcGisServiceRoot ───────────────────────────────────────────

    [Theory]
    [InlineData("https://services.arcgis.com/Org123/arcgis/rest/services/Hydrants/FeatureServer", "https://services.arcgis.com/Org123/arcgis/", "rest/services", "Hydrants", ArcGisServiceType.FeatureServer)]
    [InlineData("https://gis.example.com/arcgis/rest/services/Inspections/FeatureServer/", "https://gis.example.com/arcgis/", "rest/services", "Inspections", ArcGisServiceType.FeatureServer)]
    [InlineData("https://gis.example.com/rest/services/Utilities/Water/MapServer", "https://gis.example.com/", "rest/services", "Utilities/Water", ArcGisServiceType.MapServer)]
    [InlineData("https://gis.example.com:6443/arcgis/REST/Services/Public%20Works/Mains/mapserver?f=json#x", "https://gis.example.com:6443/arcgis/", "REST/Services", "Public Works/Mains", ArcGisServiceType.MapServer)]
    [InlineData("https://proxy.example.com/sources/Hydrants/FeatureServer", "https://proxy.example.com/sources/", "", "Hydrants", ArcGisServiceType.FeatureServer)]
    public void ServiceRoot_ParsesPrefixFoldersAndType(
        string serviceRoot, string rootAddress, string servicesPath, string serviceId, ArcGisServiceType serviceType)
    {
        var root = ArcGisServiceRoot.Parse(new Uri(serviceRoot));

        Assert.Equal(new Uri(rootAddress), root.RootAddress);
        Assert.Equal(servicesPath, root.ServicesPath);
        Assert.Equal(serviceId, root.ServiceId);
        Assert.Equal(serviceType, root.ServiceType);

        var options = root.ToClientOptions(1024);
        Assert.Equal(root.RootAddress, options.RootAddress);
        Assert.Equal(servicesPath, options.ServicesPath);
        Assert.Equal(serviceType, options.ServiceType);
        Assert.Equal(1024, options.MaxResponseBytes);
    }

    [Theory]
    [InlineData("https://gis.example.com/arcgis/rest/services/Hydrants/FeatureServer/0")]
    [InlineData("https://gis.example.com/arcgis/rest/services/Hydrants/ImageServer")]
    [InlineData("https://gis.example.com/FeatureServer")]
    [InlineData("https://gis.example.com/arcgis/rest/services/FeatureServer")]
    [InlineData("https://gis.example.com/arcgis/rest/services/a/%2E%2E/FeatureServer")]
    public void ServiceRoot_RejectsUrlsThatAreNotServiceRoots(string candidate)
    {
        Assert.False(ArcGisServiceRoot.TryParse(new Uri(candidate), out var root));
        Assert.Null(root);
        Assert.Throws<ArgumentException>(() => ArcGisServiceRoot.Parse(new Uri(candidate)));
    }

    [Fact]
    public void ServiceRoot_RejectsRelativeAndNullUrls()
    {
        Assert.False(ArcGisServiceRoot.TryParse(new Uri("rest/services/Hydrants/FeatureServer", UriKind.Relative), out _));
        Assert.False(ArcGisServiceRoot.TryParse(null, out _));
        Assert.Throws<ArgumentNullException>(() => ArcGisServiceRoot.Parse(null!));
    }

    // ── helpers ─────────────────────────────────────────────────────

    private static void AssertExpectedUrls(string serviceRoot, List<string> sent)
    {
        Assert.Contains($"{serviceRoot}?f=json", sent);
        Assert.Contains($"{serviceRoot}/0?f=json", sent);
        Assert.Contains($"{serviceRoot}/0/query?where=1%3D1&f=json&outFields=%2A&returnGeometry=true&resultOffset=0&resultRecordCount=10", sent);
        Assert.Contains($"POST {serviceRoot}/0/query", sent);
        Assert.Contains(sent, url => url.StartsWith($"{serviceRoot}/0/query?", StringComparison.Ordinal) && url.Contains("returnCountOnly=true", StringComparison.Ordinal));
        Assert.Contains(sent, url => url.StartsWith($"{serviceRoot}/0/query?", StringComparison.Ordinal) && url.Contains("returnIdsOnly=true", StringComparison.Ordinal));
        Assert.Contains(sent, url => url.StartsWith($"{serviceRoot}/0/query?", StringComparison.Ordinal) && url.Contains("returnExtentOnly=true", StringComparison.Ordinal));
        Assert.Contains(sent, url => url.StartsWith($"{serviceRoot}/0/query?", StringComparison.Ordinal) && url.Contains("outStatistics=", StringComparison.Ordinal));
        Assert.Contains($"POST {serviceRoot}/0/validateSQL", sent);
        Assert.Contains($"{serviceRoot}/0/queryAttachments?objectIds=1%2C2%2C3&returnUrl=false&f=json", sent);
        Assert.Contains($"{serviceRoot}/0/42/attachments?f=json", sent);
        Assert.Contains($"{serviceRoot}/0/42/attachments/7", sent);
        Assert.Contains($"POST {serviceRoot}/0/42/addAttachment", sent);
        Assert.Contains($"POST {serviceRoot}/0/42/updateAttachment", sent);
        Assert.Contains($"POST {serviceRoot}/0/42/deleteAttachments", sent);
        Assert.Contains($"POST {serviceRoot}/0/applyEdits", sent);
    }

    private static async Task DriveEveryCallAsync(HonuaFeatureServerClient client, string serviceId)
    {
        var page = new FeatureServerQueryParams { Where = "1=1", OutFields = "*", ReturnGeometry = true, ResultOffset = 0, ResultRecordCount = 10 };
        var source = new FeatureSource { ServiceId = serviceId, LayerId = 0 };

        await client.GetServiceInfoAsync(serviceId);
        await client.GetLayerInfoAsync(serviceId, 0);
        await client.QueryAsync(serviceId, 0, page);
        await client.QueryAsync(serviceId, 0, new FeatureServerQueryParams { Where = "NAME IN ('" + new string('x', 2100) + "')" });
        await client.QueryCountAsync(serviceId, 0, new FeatureServerQueryParams());
        await client.QueryIdsAsync(serviceId, 0, new FeatureServerQueryParams());
        await client.QueryExtentAsync(serviceId, 0, new FeatureServerQueryParams());
        await client.QueryStatisticsAsync(
            serviceId,
            0,
            new FeatureServerStatisticsParams
            {
                OutStatistics = """[{"statisticType":"count","onStatisticField":"OBJECTID","outStatisticFieldName":"n"}]""",
            });
        await client.ValidateSqlAsync(serviceId, 0, "1=1");
        using (var raw = await client.QueryRawAsync(serviceId, 0, page))
        {
            Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
        }

        await client.QueryAttachmentsAsync(serviceId, 0, [1, 2, 3]);
        await client.ListAttachmentsAsync(new FeatureAttachmentListRequest { Source = source, ObjectId = 42 });
        var download = await client.DownloadAttachmentAsync(new FeatureAttachmentDownloadRequest { Source = source, ObjectId = 42, AttachmentId = 7 });
        await download.Content.DisposeAsync();

        using var upload = new MemoryStream(Encoding.UTF8.GetBytes("photo"));
        await client.AddAttachmentAsync(new FeatureAttachmentAddRequest { Source = source, ObjectId = 42, Content = upload, Name = "a.jpg", ContentType = "image/jpeg" });
        upload.Position = 0;
        await client.UpdateAttachmentAsync(new FeatureAttachmentUpdateRequest { Source = source, ObjectId = 42, AttachmentId = 7, Content = upload, Name = "a.jpg", ContentType = "image/jpeg" });
        await client.DeleteAttachmentAsync(new FeatureAttachmentDeleteRequest { Source = source, ObjectId = 42, AttachmentId = 7 });
        await client.DeleteFeaturesAsync(serviceId, 0, [42]);
    }

    private static HonuaFeatureServerClient CreateClient(
        List<string> sent, string? baseAddress, HonuaFeatureServerClientOptions? options)
    {
        var handler = new MockHttpHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            sent.Add(request.Method == HttpMethod.Post ? $"POST {url}" : url);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(ResponseFor(request.RequestUri!.AbsolutePath)));
        });
        var http = new HttpClient(handler);
        if (baseAddress is not null)
        {
            http.BaseAddress = new Uri(baseAddress);
        }

        return options is null ? new HonuaFeatureServerClient(http) : new HonuaFeatureServerClient(http, options);
    }

    private static string ResponseFor(string path) => path switch
    {
        _ when path.EndsWith("/applyEdits", StringComparison.Ordinal) =>
            """{"addResults":[],"updateResults":[],"deleteResults":[{"objectId":42,"success":true}]}""",
        _ when path.EndsWith("/addAttachment", StringComparison.Ordinal) =>
            """{"addAttachmentResult":{"objectId":7,"success":true}}""",
        _ when path.EndsWith("/updateAttachment", StringComparison.Ordinal) =>
            """{"updateAttachmentResult":{"objectId":7,"success":true}}""",
        _ when path.EndsWith("/deleteAttachments", StringComparison.Ordinal) =>
            """{"deleteAttachmentResults":[{"objectId":7,"success":true}]}""",
        _ when path.EndsWith("/validateSQL", StringComparison.Ordinal) => """{"isValidSQL":true}""",
        _ when path.EndsWith("/queryAttachments", StringComparison.Ordinal) => """{"attachmentGroups":[]}""",
        _ when path.EndsWith("/attachments", StringComparison.Ordinal) => """{"attachmentInfos":[]}""",
        _ when path.EndsWith("/query", StringComparison.Ordinal) =>
            """{"features":[],"count":0,"objectIds":[],"extent":{"xmin":0,"ymin":0,"xmax":1,"ymax":1}}""",
        _ => """{"layers":[],"id":0,"name":"layer"}""",
    };
}
