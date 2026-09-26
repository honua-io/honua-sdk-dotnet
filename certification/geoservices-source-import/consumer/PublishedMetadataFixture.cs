// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Sdk.GeoServices.FeatureServer;

// Supplemental installed-package fixture, not a live-server qualification. These authored
// literals are deliberately independent of server/SDK output. Direct member assertions ensure
// AdditionalProperties alone cannot pass the typed-coverage acceptance criterion.
internal static class PublishedMetadataFixture
{
    private const string Service = """
        {"currentVersion":11.3,"description":"Parcel inventory","layers":[{"id":0,"name":"Parcels"}],"tables":[{"id":3,"name":"Inspections"}]}
        """;

    private const string Layer = """
        {
          "id":0,"minScale":250000,"maxScale":0,"subtypeField":"ZONE","defaultSubtypeCode":7,
          "attributeRules":[{"name":"AreaCalc","scriptExpression":"Area($feature)"}],
          "subtypes":[{"code":7,"name":"Residential","defaultValues":{"ZONE":7}}],
          "extent":{"spatialReference":{"wkt":"LOCAL_CS[\"Survey grid\"]"}},
          "fields":[
            {"name":"omitted","type":"esriFieldTypeString"},
            {"name":"locked","type":"esriFieldTypeInteger","nullable":false,"editable":false,"defaultValue":9007199254740993},
            {"name":"optional","type":"esriFieldTypeString","nullable":true,"editable":true}
          ]
        }
        """;

    public static async Task<string> VerifyAsync()
    {
        using var http = new HttpClient(new FixtureHandler())
        {
            BaseAddress = new Uri("https://fixture.invalid"),
        };
        var client = new HonuaFeatureServerClient(http, new HonuaFeatureServerClientOptions());
        var service = await client.GetServiceInfoAsync("Parcels").ConfigureAwait(false);
        var layer = await client.GetLayerInfoAsync("Parcels", 0).ConfigureAwait(false);
        if (service.CurrentVersion?.GetDecimal() != 11.3m || service.Description != "Parcel inventory"
            || service.Tables is not { Count: 1 } || service.Tables[0].Id != 3
            || layer.MinScale != 250000 || layer.MaxScale != 0 || layer.SubtypeField != "ZONE"
            || layer.DefaultSubtypeCode?.GetInt32() != 7
            || layer.AttributeRules?[0].GetProperty("scriptExpression").GetString() != "Area($feature)"
            || layer.Subtypes?[0].GetProperty("defaultValues").GetProperty("ZONE").GetInt32() != 7
            || layer.Extent?.SpatialReference?.Wkt != "LOCAL_CS[\"Survey grid\"]")
        {
            throw new CellFailure("published typed service/layer metadata differs from the authored fixture");
        }

        var fields = layer.Fields!;
        if (fields.Count != 3 || fields[0].IsNullable is not null || fields[0].IsEditable is not null
            || fields[1].IsNullable != false || fields[1].IsEditable != false
            || fields[1].DefaultValue?.GetInt64() != 9007199254740993L
            || fields[2].IsNullable != true || fields[2].IsEditable != true)
        {
            throw new CellFailure("published field members lose tri-state flags or the int64 default");
        }

        using var raw = await client.GetLayerMetadataAsync("Parcels", 0).ConfigureAwait(false);
        if (!JsonNode.DeepEquals(JsonNode.Parse(Layer), JsonNode.Parse(raw.RootElement.GetRawText())))
        {
            throw new CellFailure("raw metadata changes the authored fixture");
        }

        return "authored transport fixture: typed metadata, subtype default, WKT, int64 default and absent/false/true field flags preserved (not live-source evidence)";
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = request.RequestUri!.AbsolutePath.EndsWith("/0", StringComparison.Ordinal) ? Layer : Service;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
