using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
[Trait("Mutation", "Destructive")]
public sealed class DestructiveProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact(true)]
    public async Task FeatureServerApplyEdits_AddUpdateDelete_RoundTrips()
    {
        using var timeout = _fixture.CreateTimeoutScope(TimeSpan.FromSeconds(90));
        var addAttributes = ParseAttributes(
            _fixture.Options.FeatureServerEditAddAttributesJson,
            "HONUA_PROTOCOL_FEATURESERVER_EDIT_ADD_ATTRIBUTES_JSON");
        var updateAttributes = ParseAttributes(
            _fixture.Options.FeatureServerEditUpdateAttributesJson,
            "HONUA_PROTOCOL_FEATURESERVER_EDIT_UPDATE_ATTRIBUTES_JSON");
        var geometry = ParseOptionalElement(_fixture.Options.FeatureServerEditGeometryJson);
        long? objectIdForCleanup = null;

        try
        {
            var addResponse = await _fixture.FeatureServerEditClient.AddFeaturesAsync(
                _fixture.Options.ServiceName,
                _fixture.Options.LayerId,
                [
                    new FeatureServerFeature
                    {
                        Attributes = addAttributes,
                        Geometry = geometry
                    }
                ],
                rollbackOnFailure: true,
                timeout.Token).ConfigureAwait(false);

            var addResult = Assert.Single(addResponse.AddResults);
            Assert.True(addResult.Success, FormatError(addResult.Error));
            Assert.True(addResult.ObjectId.HasValue, "FeatureServer add did not return an object ID.");
            objectIdForCleanup = addResult.ObjectId.Value;

            var sharedEditClient = _fixture.Services
                .GetServices<IHonuaFeatureEditClient>()
                .Single(client => client.ProviderName == "geoservices-featureserver");

            var updateResponse = await sharedEditClient.ApplyEditsAsync(
                new FeatureEditRequest
                {
                    Source = new FeatureSource
                    {
                        ServiceId = _fixture.Options.ServiceName,
                        LayerId = _fixture.Options.LayerId
                    },
                    Updates =
                    [
                        new FeatureEditFeature
                        {
                            ObjectId = objectIdForCleanup.Value,
                            Attributes = updateAttributes
                        }
                    ],
                    RollbackOnFailure = true
                },
                timeout.Token).ConfigureAwait(false);

            var updateResult = Assert.Single(updateResponse.UpdateResults);
            Assert.True(updateResult.Succeeded, FormatError(updateResult.Error));

            var deleteResponse = await _fixture.FeatureServerEditClient.DeleteFeaturesAsync(
                _fixture.Options.ServiceName,
                _fixture.Options.LayerId,
                [objectIdForCleanup.Value],
                rollbackOnFailure: true,
                timeout.Token).ConfigureAwait(false);

            var deleteResult = Assert.Single(deleteResponse.DeleteResults);
            Assert.True(deleteResult.Success, FormatError(deleteResult.Error));
            objectIdForCleanup = null;
        }
        finally
        {
            if (objectIdForCleanup.HasValue)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await TryDeleteAsync(objectIdForCleanup.Value, cleanup.Token).ConfigureAwait(false);
            }
        }
    }

    private async Task TryDeleteAsync(long objectId, CancellationToken cancellationToken)
    {
        try
        {
            await _fixture.FeatureServerEditClient.DeleteFeaturesAsync(
                _fixture.Options.ServiceName,
                _fixture.Options.LayerId,
                [objectId],
                rollbackOnFailure: true,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Preserve the original edit failure.
        }
    }

    private static Dictionary<string, JsonElement> ParseAttributes(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new Xunit.Sdk.XunitException($"{name} must be a JSON object.");
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new Xunit.Sdk.XunitException($"{name} must be a JSON object.");
        }

        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    private static JsonElement? ParseOptionalElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string FormatError(FeatureServerEditError? error)
        => error is null
            ? "FeatureServer edit result did not succeed."
            : $"FeatureServer edit failed: code={error.Code?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}; message={error.Description ?? error.Message ?? "unknown"}";

    private static string FormatError(FeatureEditError? error)
        => error is null
            ? "Shared FeatureServer edit result did not succeed."
            : $"Shared FeatureServer edit failed: code={error.Code}; message={error.Message}";
}
