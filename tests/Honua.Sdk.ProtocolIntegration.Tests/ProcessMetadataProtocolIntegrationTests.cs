namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class ProcessMetadataProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task LandingPage_LinksToDiscoveryResources()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var landing = await _fixture.ProcessesClient.GetLandingPageAsync(timeout.Token).ConfigureAwait(false);

        Assert.False(string.IsNullOrWhiteSpace(landing.Title));
        Assert.Contains(landing.Links, link => link.Rel == "conformance"
            && new Uri(link.Href).AbsolutePath == "/ogc/processes/conformance");
        Assert.Contains(landing.Links, link => link.Rel == "http://www.opengis.net/def/rel/ogc/1.0/processes"
            && new Uri(link.Href).AbsolutePath == "/ogc/processes/processes");
    }

    [ProtocolIntegrationFact]
    public async Task Conformance_DeclaresProcessesCore()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var conformance = await _fixture.ProcessesClient.GetConformanceAsync(timeout.Token).ConfigureAwait(false);

        Assert.Contains("http://www.opengis.net/spec/ogcapi-processes-1/1.0/conf/core", conformance.ConformsTo);
        Assert.Equal(conformance.ConformsTo.Count, conformance.ConformsTo.Distinct().Count());
    }

    [ProtocolIntegrationFact]
    public async Task ProcessCatalog_DescribesCanonicalPlanInput()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var catalog = await _fixture.ProcessesClient.ListProcessesAsync(timeout.Token).ConfigureAwait(false);
        var summary = Assert.Single(catalog.Processes, process => process.Id == "honua-geoprocessing");
        var description = await _fixture.ProcessesClient.GetProcessAsync(summary.Id, timeout.Token).ConfigureAwait(false);

        Assert.Equal(summary.Id, description.Id);
        Assert.False(string.IsNullOrWhiteSpace(summary.Title));
        Assert.Equal(summary.Title, description.Title);
        Assert.Equal(summary.Version, description.Version);
        Assert.Contains("async-execute", description.JobControlOptions);
        Assert.Contains("value", description.OutputTransmission);
        Assert.True(description.Inputs.TryGetValue("plan", out var plan));
        Assert.NotNull(plan);
        Assert.NotNull(plan.Schema);
        Assert.Equal("object", plan.Schema.Type);
        Assert.Equal("application/json", plan.Schema.ContentMediaType);
    }
}
