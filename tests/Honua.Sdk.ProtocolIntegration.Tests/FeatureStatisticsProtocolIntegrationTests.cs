using System.Text.Json;
using Honua.Sdk.GeoServices.FeatureServer.Models;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class FeatureStatisticsProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task Statistics_CountMatchesFilteredFeatureCount()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var layer = await _fixture.FeatureServerClient.GetLayerInfoAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId, timeout.Token).ConfigureAwait(false);
        Assert.False(string.IsNullOrWhiteSpace(layer.ObjectIdField));
        var definitions = JsonSerializer.Serialize(new[]
        {
            new { statisticType = "count", onStatisticField = layer.ObjectIdField, outStatisticFieldName = "feature_count" }
        });
        foreach (var where in new[] { "1=1", "1=0" })
        {
            var count = await _fixture.FeatureServerClient.QueryCountAsync(
                _fixture.Options.ServiceName, _fixture.Options.LayerId,
                new FeatureServerQueryParams { Where = where }, timeout.Token).ConfigureAwait(false);
            Assert.True(where == "1=1" ? count > 0 : count == 0);
            var statistics = await _fixture.FeatureServerClient.QueryStatisticsAsync(
                _fixture.Options.ServiceName, _fixture.Options.LayerId,
                new FeatureServerStatisticsParams { Where = where, OutStatistics = definitions },
                timeout.Token).ConfigureAwait(false);
            var feature = Assert.Single(statistics.Features ?? []);
            Assert.NotNull(feature.Attributes);
            Assert.True(feature.Attributes.TryGetValue("feature_count", out var value));
            Assert.Equal((long)count, value.GetInt64());
        }
    }

    [ProtocolIntegrationFact]
    public async Task SqlValidation_DistinguishesValidAndMalformedExpressions()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var valid = await _fixture.FeatureServerClient.ValidateSqlAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId, "1=1", timeout.Token).ConfigureAwait(false);
        Assert.True(valid.IsValidSql);
        var malformed = await _fixture.FeatureServerClient.ValidateSqlAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId, "1 =", timeout.Token).ConfigureAwait(false);
        Assert.False(malformed.IsValidSql);
    }
}
