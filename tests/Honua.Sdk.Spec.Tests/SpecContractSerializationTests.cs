// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Spec.Models;
using Honua.Sdk.Spec.Tests.Fixtures;

namespace Honua.Sdk.Spec.Tests;

public sealed class SpecContractSerializationTests
{
    [Fact]
    public void SpecDocumentRequest_SerializesWithServerContractShape()
    {
        var fixture = SpecFixtureReader.ReadJson("spec-document-request.json");
        var request = JsonSerializer.Deserialize(fixture, SpecJsonContext.Default.SpecDocumentRequest);

        var json = JsonSerializer.Serialize(request, SpecJsonContext.Default.SpecDocumentRequest);

        Assert.NotNull(request);
        Assert.Equal(2, request!.Nodes.Count);
        Assert.Contains("\"grammarVersion\":\"spec/v1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"processFamilyVersion\":\"process/v1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"Compute\"", json, StringComparison.Ordinal);
        Assert.Contains("\"cacheMode\":\"Bypass\"", json, StringComparison.Ordinal);
        Assert.Contains("\"nondeterministic\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecApplyEvent_RoundTripsServerEventShape()
    {
        var json = SpecFixtureReader.ReadJson("spec-apply-event.json");

        var evt = JsonSerializer.Deserialize(json, SpecJsonContext.Default.SpecApplyEvent);

        Assert.NotNull(evt);
        Assert.Equal(2, evt!.Sequence);
        Assert.Equal(SpecApplyEventKind.Succeeded, evt.Kind);
        Assert.Equal("apply-1", evt.ApplyToken);
        Assert.Equal("buffer", evt.NodeId);
        Assert.Equal(42.5, evt.ActualCost?.DurationMs);
    }
}
