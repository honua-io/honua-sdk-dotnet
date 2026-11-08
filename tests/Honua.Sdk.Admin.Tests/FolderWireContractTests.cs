// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin.Tests;

public sealed class FolderWireContractTests
{
    [Fact]
    public void ServiceSummary_RoundTripPreservesOrganizationAndLogicalIdentity()
    {
        const string wire = """
            {"serviceName":"logical-parcels","description":"Parcels","layerCount":1,
             "folderId":"folder-1","folderName":"Survey","publicName":"Parcels",
             "title":"Field parcels","folderMutationIssues":["Retired path is reserved."]}
            """;
        var service = JsonSerializer.Deserialize(wire, HonuaAdminJsonContext.Default.ServiceSummary)!;
        Assert.Equal("logical-parcels", service.ServiceName);
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(service, HonuaAdminJsonContext.Default.ServiceSummary));
        foreach (var property in new[] { "folderId", "folderName", "publicName", "title", "folderMutationIssues" })
        {
            using var expected = JsonDocument.Parse(wire);
            Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty(property), result.RootElement.GetProperty(property)), property);
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"folder-1\"")]
    public void PublishLayer_RoundTripPreservesExplicitFolderSelection(string folderJson)
    {
        var wire = $$"""{"schema":"public","table":"parcels","layerName":"Parcels","folderId":{{folderJson}}}""";
        var request = JsonSerializer.Deserialize(wire, HonuaAdminJsonContext.Default.PublishLayerRequest)!;
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(request, HonuaAdminJsonContext.Default.PublishLayerRequest));
        using var expected = JsonDocument.Parse(folderJson);
        Assert.True(JsonElement.DeepEquals(expected.RootElement, result.RootElement.GetProperty("folderId")));
    }

    [Fact]
    public void PublishLayer_RoundTripPreservesImmutablePublicName()
    {
        const string wire = """{"schema":"public","table":"parcels","layerName":"Parcels","publicName":"PublicParcels"}""";
        var request = JsonSerializer.Deserialize(wire, HonuaAdminJsonContext.Default.PublishLayerRequest)!;
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(request, HonuaAdminJsonContext.Default.PublishLayerRequest));
        Assert.Equal("PublicParcels", result.RootElement.GetProperty("publicName").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("workspace")]
    public void PublishLayer_RoundTripPreservesExplicitNamespace(string namespaceName)
    {
        var wire = $$"""{"schema":"public","table":"parcels","layerName":"Parcels","namespace":"{{namespaceName}}"}""";
        var request = JsonSerializer.Deserialize(wire, HonuaAdminJsonContext.Default.PublishLayerRequest)!;
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(request, HonuaAdminJsonContext.Default.PublishLayerRequest));
        Assert.Equal(namespaceName, result.RootElement.GetProperty("namespace").GetString());
    }

    [Fact]
    public void PublishLayer_LegacyRequestOmitsOrganization()
    {
        var request = new PublishLayerRequest { Schema = "public", Table = "parcels", LayerName = "Parcels" };
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(request, HonuaAdminJsonContext.Default.PublishLayerRequest));
        Assert.False(result.RootElement.TryGetProperty("folderId", out _));
        Assert.False(result.RootElement.TryGetProperty("publicName", out _));
        Assert.False(result.RootElement.TryGetProperty("namespace", out _));
        Assert.Equal("parcels", result.RootElement.GetProperty("table").GetString());
    }
}
