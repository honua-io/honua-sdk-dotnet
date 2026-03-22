// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class HonuaGeocodingClientTests
{
    private static HonuaGeocodingClient CreateGeocodingClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        return new HonuaGeocodingClient(httpClient);
    }

    private static HttpResponseMessage CreateGeoJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    // ── ForwardGeocodeAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ForwardGeocode_Success_ReturnsResults()
    {
        var responseJson = """
        {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 },
            "candidates": [
                {
                    "address": "123 Main St, Springfield, IL",
                    "location": { "x": -89.6501, "y": 39.7817 },
                    "score": 97.5,
                    "attributes": { "Addr_type": "PointAddress", "City": "Springfield" }
                },
                {
                    "address": "123 Main St, Springfield, OH",
                    "location": { "x": -83.8088, "y": 39.9242 },
                    "score": 85.0,
                    "attributes": {}
                }
            ]
        }
        """;

        var client = CreateGeocodingClient(req =>
        {
            Assert.Contains("/rest/services/World/GeocodeServer/findAddressCandidates", req.RequestUri!.PathAndQuery);
            Assert.Contains("singleLine=123%20Main%20St", req.RequestUri.Query);
            Assert.Contains("f=json", req.RequestUri.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var results = await client.ForwardGeocodeAsync("123 Main St");

        Assert.Equal(2, results.Count);
        Assert.Equal("123 Main St, Springfield, IL", results[0].Address);
        Assert.Equal(39.7817, results[0].Latitude);
        Assert.Equal(-89.6501, results[0].Longitude);
        Assert.Equal(97.5, results[0].Score);
        Assert.Equal("PointAddress", results[0].Attributes["Addr_type"]);
        Assert.Equal("Springfield", results[0].Attributes["City"]);
    }

    [Fact]
    public async Task ForwardGeocode_EmptyResults_ReturnsEmptyList()
    {
        var responseJson = """
        {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 },
            "candidates": []
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var results = await client.ForwardGeocodeAsync("xyznonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task ForwardGeocode_NullCandidates_ReturnsEmptyList()
    {
        var responseJson = """
        {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var results = await client.ForwardGeocodeAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task ForwardGeocode_GeoServicesError_ThrowsHonuaAdminApiException()
    {
        var responseJson = """
        {
            "error": {
                "code": 400,
                "message": "Unable to find address",
                "details": []
            }
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ForwardGeocodeAsync("bad input"));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Unable to find address", ex.Message);
    }

    [Fact]
    public async Task ForwardGeocode_NullAddress_ThrowsArgumentNullException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse("{}")));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.ForwardGeocodeAsync(null!));
    }

    [Fact]
    public async Task ForwardGeocode_WithOptions_PassesQueryParameters()
    {
        var responseJson = """
        {
            "spatialReference": { "wkid": 3857 },
            "candidates": []
        }
        """;

        var client = CreateGeocodingClient(req =>
        {
            Assert.Contains("maxLocations=10", req.RequestUri!.Query);
            Assert.Contains("outSR=3857", req.RequestUri.Query);
            Assert.Contains("magicKey=abc123", req.RequestUri.Query);
            Assert.Contains("countryCode=USA%2CCAN", req.RequestUri.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var options = new ForwardGeocodeOptions
        {
            MaxResults = 10,
            SpatialReferenceWkid = 3857,
            MagicKey = "abc123",
            CountryCodes = new[] { "USA", "CAN" }
        };

        await client.ForwardGeocodeAsync("test", options);
    }

    [Fact]
    public async Task ForwardGeocode_CandidateWithNullLocation_DefaultsToZero()
    {
        var responseJson = """
        {
            "spatialReference": { "wkid": 4326 },
            "candidates": [
                {
                    "address": "Unknown",
                    "location": null,
                    "score": 50.0,
                    "attributes": {}
                }
            ]
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var results = await client.ForwardGeocodeAsync("test");

        Assert.Single(results);
        Assert.Equal(0, results[0].Latitude);
        Assert.Equal(0, results[0].Longitude);
    }

    // ── ReverseGeocodeAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ReverseGeocode_Success_ReturnsResult()
    {
        var responseJson = """
        {
            "address": {
                "Match_addr": "123 Main St, Springfield, IL 62701",
                "LongLabel": "123 Main St, Springfield, IL 62701, USA",
                "City": "Springfield",
                "Region": "Illinois"
            },
            "location": { "x": -89.6501, "y": 39.7817 }
        }
        """;

        var client = CreateGeocodingClient(req =>
        {
            Assert.Contains("/rest/services/World/GeocodeServer/reverseGeocode", req.RequestUri!.PathAndQuery);
            Assert.Contains("location=-89.6501%2C39.7817", req.RequestUri.Query);
            Assert.Contains("f=json", req.RequestUri.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var result = await client.ReverseGeocodeAsync(39.7817, -89.6501);

        Assert.NotNull(result);
        Assert.Equal("123 Main St, Springfield, IL 62701", result.Address);
        Assert.Equal(39.7817, result.Latitude);
        Assert.Equal(-89.6501, result.Longitude);
        Assert.Equal("Springfield", result.Attributes["City"]);
        Assert.Equal("Illinois", result.Attributes["Region"]);
    }

    [Fact]
    public async Task ReverseGeocode_NullAddress_ReturnsNull()
    {
        var responseJson = """
        {
            "address": null,
            "location": null
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var result = await client.ReverseGeocodeAsync(0.0, 0.0);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReverseGeocode_NullLocation_ReturnsNull()
    {
        var responseJson = """
        {
            "address": { "Match_addr": "Somewhere" },
            "location": null
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var result = await client.ReverseGeocodeAsync(0.0, 0.0);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReverseGeocode_HttpError_ThrowsHonuaAdminApiException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"code":400,"message":"Invalid location"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            }));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ReverseGeocodeAsync(999.0, 999.0));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Invalid location", ex.Message);
    }

    [Fact]
    public async Task ReverseGeocode_WithSpatialReference_PassesOutSR()
    {
        var responseJson = """
        {
            "address": null,
            "location": null
        }
        """;

        var client = CreateGeocodingClient(req =>
        {
            Assert.Contains("outSR=3857", req.RequestUri!.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        await client.ReverseGeocodeAsync(39.78, -89.65, new ReverseGeocodeOptions
        {
            SpatialReferenceWkid = 3857
        });
    }

    // ── SuggestAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Suggest_Success_ReturnsSuggestions()
    {
        var responseJson = """
        {
            "suggestions": [
                {
                    "text": "123 Main St, Springfield, IL",
                    "magicKey": "key1",
                    "isCollection": false
                },
                {
                    "text": "123 Main St, Springfield, OH",
                    "magicKey": "key2",
                    "isCollection": false
                }
            ]
        }
        """;

        var client = CreateGeocodingClient(req =>
        {
            Assert.Contains("/rest/services/World/GeocodeServer/suggest", req.RequestUri!.PathAndQuery);
            Assert.Contains("text=123%20Main", req.RequestUri.Query);
            Assert.Contains("f=json", req.RequestUri.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var results = await client.SuggestAsync("123 Main");

        Assert.Equal(2, results.Count);
        Assert.Equal("123 Main St, Springfield, IL", results[0].Text);
        Assert.Equal("key1", results[0].MagicKey);
        Assert.False(results[0].IsCollection);
    }

    [Fact]
    public async Task Suggest_EmptyResults_ReturnsEmptyList()
    {
        var responseJson = """
        {
            "suggestions": []
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var results = await client.SuggestAsync("xyznonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Suggest_NullSuggestions_ReturnsEmptyList()
    {
        var responseJson = "{}";

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var results = await client.SuggestAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Suggest_NullText_ThrowsArgumentNullException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse("{}")));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.SuggestAsync(null!));
    }

    [Fact]
    public async Task Suggest_WithOptions_PassesQueryParameters()
    {
        var responseJson = """{ "suggestions": [] }""";

        var client = CreateGeocodingClient(req =>
        {
            Assert.Contains("maxSuggestions=3", req.RequestUri!.Query);
            Assert.Contains("countryCode=USA", req.RequestUri.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        await client.SuggestAsync("test", new SuggestOptions
        {
            MaxResults = 3,
            CountryCodes = new[] { "USA" }
        });
    }

    // ── Auth header passing ─────────────────────────────────────────────

    [Fact]
    public async Task ForwardGeocode_ApiKeyHeader_IsSentByAuthHandler()
    {
        string? capturedApiKey = null;

        var options = Microsoft.Extensions.Options.Options.Create(new HonuaAdminClientOptions
        {
            ApiKey = "geo-test-key"
        });

        var responseJson = """{ "candidates": [] }""";

        var innerHandler = new MockHttpHandler(req =>
        {
            if (req.Headers.TryGetValues("X-API-Key", out var values))
            {
                capturedApiKey = values.First();
            }

            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaGeocodingClient(httpClient);
        await client.ForwardGeocodeAsync("test address");

        Assert.Equal("geo-test-key", capturedApiKey);
    }

    [Fact]
    public async Task ForwardGeocode_BearerToken_IsSentByAuthHandler()
    {
        string? capturedAuth = null;

        var options = Microsoft.Extensions.Options.Options.Create(new HonuaAdminClientOptions
        {
            BearerToken = "geo-jwt-token"
        });

        var responseJson = """{ "candidates": [] }""";

        var innerHandler = new MockHttpHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.ToString();
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaGeocodingClient(httpClient);
        await client.ForwardGeocodeAsync("test address");

        Assert.Equal("Bearer geo-jwt-token", capturedAuth);
    }

    // ── HTTP error responses ────────────────────────────────────────────

    [Fact]
    public async Task ForwardGeocode_Http404_ThrowsHonuaAdminApiException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                ReasonPhrase = "Not Found",
                Content = new StringContent(
                    """{"error":{"code":404,"message":"Locator not found"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            }));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ForwardGeocodeAsync("test"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Locator not found", ex.Message);
    }

    [Fact]
    public async Task ForwardGeocode_Http500_ThrowsHonuaAdminApiException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "Internal Server Error",
                Content = new StringContent(
                    """{"error":{"code":500,"message":"Internal server error"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            }));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ForwardGeocodeAsync("test"));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal("Internal server error", ex.Message);
    }

    [Fact]
    public async Task ReverseGeocode_Http500_ThrowsHonuaAdminApiException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "Internal Server Error",
                Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain")
            }));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ReverseGeocodeAsync(39.78, -89.65));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task Suggest_Http404_ThrowsHonuaAdminApiException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                ReasonPhrase = "Not Found",
                Content = new StringContent("Not Found", System.Text.Encoding.UTF8, "text/plain")
            }));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.SuggestAsync("test"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    // ── Custom locator name ─────────────────────────────────────────────

    [Fact]
    public async Task ForwardGeocode_CustomLocator_UsesCorrectPath()
    {
        var responseJson = """{ "candidates": [] }""";

        var mockHandler = new MockHttpHandler(req =>
        {
            Assert.Contains("/rest/services/MyLocator/GeocodeServer/findAddressCandidates", req.RequestUri!.PathAndQuery);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaGeocodingClient(httpClient, "MyLocator");
        await client.ForwardGeocodeAsync("test");
    }

    // ── BatchGeocodeAsync ───────────────────────────────────────────────

    [Fact]
    public async Task BatchGeocode_ThrowsNotSupportedException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse("{}")));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => client.BatchGeocodeAsync(new[] { "addr1", "addr2" }));
    }
}
