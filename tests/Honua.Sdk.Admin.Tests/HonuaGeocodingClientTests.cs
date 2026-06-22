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
    public async Task ForwardGeocode_CandidateWithNullLocation_IsSkipped()
    {
        var responseJson = """
        {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 },
            "candidates": [
                {
                    "address": "No location candidate",
                    "location": null,
                    "score": 50.0,
                    "attributes": {}
                },
                {
                    "address": "123 Main St, Springfield, IL",
                    "location": { "x": -89.6501, "y": 39.7817 },
                    "score": 97.5,
                    "attributes": {}
                }
            ]
        }
        """;

        var client = CreateGeocodingClient(_ =>
            Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var results = await client.ForwardGeocodeAsync("123 Main St");

        // The null-location candidate is dropped (no false (0, 0) "Null Island" result).
        Assert.Single(results);
        Assert.Equal("123 Main St, Springfield, IL", results[0].Address);
        Assert.Equal(39.7817, results[0].Latitude);
        Assert.Equal(-89.6501, results[0].Longitude);
        Assert.DoesNotContain(results, r => r.Latitude == 0 && r.Longitude == 0);
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
            Assert.Contains("location=-89.65%2C39.78", req.RequestUri.Query);
            Assert.Contains("searchExtent=-90%2C39%2C-89%2C40", req.RequestUri.Query);
            Assert.Contains("category=Address%2CPOI", req.RequestUri.Query);
            Assert.Contains("outFields=Addr_type%2CCity", req.RequestUri.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        var options = new ForwardGeocodeOptions
        {
            MaxResults = 10,
            SpatialReferenceWkid = 3857,
            MagicKey = "abc123",
            CountryCodes = new[] { "USA", "CAN" },
            Location = new GeocodePoint(-89.65, 39.78),
            SearchExtent = new GeocodeExtent(-90, 39, -89, 40),
            Categories = new[] { "Address", "POI" },
            OutFields = new[] { "Addr_type", "City" }
        };

        await client.ForwardGeocodeAsync("test", options);
    }

    [Fact]
    public async Task ForwardGeocode_CandidateWithNullLocation_IsSkippedNotNullIsland()
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

        // A candidate without a location must not surface as a (0, 0) "Null Island" match;
        // it is dropped entirely.
        Assert.Empty(results);
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
            Assert.Contains("location=-89.65%2C39.78", req.RequestUri.Query);
            Assert.Contains("searchExtent=-90%2C39%2C-89%2C40", req.RequestUri.Query);
            Assert.Contains("category=Address", req.RequestUri.Query);
            return Task.FromResult(CreateGeoJsonResponse(responseJson));
        });

        await client.SuggestAsync("test", new SuggestOptions
        {
            MaxResults = 3,
            CountryCodes = new[] { "USA" },
            Location = new GeocodePoint(-89.65, 39.78),
            SearchExtent = new GeocodeExtent(-90, 39, -89, 40),
            Categories = new[] { "Address" }
        });
    }

    // ── Auth header passing ─────────────────────────────────────────────

    [Fact]
    public async Task ForwardGeocode_ApiKeyHeader_IsSentByAuthHandler()
    {
        string? capturedApiKey = null;

        var options = Microsoft.Extensions.Options.Options.Create(new HonuaAdminClientOptions
        {
            BaseAddress = new Uri("https://localhost:5001"),
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
            BaseAddress = new Uri("https://localhost:5001"),
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
    public async Task ForwardGeocode_Http429_ThrowsRateLimitException()
    {
        var client = CreateGeocodingClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                ReasonPhrase = "Too Many Requests",
                Content = new StringContent(
                    """{"error":{"code":429,"message":"Rate limit exceeded"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            }));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ForwardGeocodeAsync("test"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal("Rate limit exceeded", ex.Message);
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
    public async Task BatchGeocode_Detailed_ReturnsPartialFailures()
    {
        var responseJson = """
        {
            "locations": [
                {
                    "address": "123 Main St, Springfield, IL",
                    "location": { "x": -89.6501, "y": 39.7817 },
                    "score": 98.1,
                    "attributes": { "ResultID": 1, "Status": "M", "City": "Springfield" }
                },
                {
                    "address": "",
                    "location": null,
                    "score": 0,
                    "attributes": { "ResultID": 2, "Status": "U" }
                }
            ]
        }
        """;

        var client = CreateGeocodingClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/rest/services/World/GeocodeServer/geocodeAddresses", req.RequestUri!.PathAndQuery);
            var form = ParseForm(await req.Content!.ReadAsStringAsync());
            Assert.Contains("\"SingleLine\":\"123 Main St\"", form["addresses"]);
            Assert.Contains("\"SingleLine\":\"missing place\"", form["addresses"]);
            Assert.Equal("json", form["f"]);
            Assert.Equal("3857", form["outSR"]);
            Assert.Equal("USA,CAN", form["sourceCountry"]);
            Assert.Equal("-90,39,-89,40", form["searchExtent"]);
            Assert.Equal("Address,POI", form["category"]);
            Assert.Equal("City,Region", form["outFields"]);
            return CreateGeoJsonResponse(responseJson);
        });

        var results = await ((IHonuaBatchGeocodingClient)client).BatchGeocodeDetailedAsync(
            new[] { "123 Main St", "missing place" },
            new BatchGeocodeOptions
            {
                SpatialReferenceWkid = 3857,
                CountryCodes = new[] { "USA", "CAN" },
                SearchExtent = new GeocodeExtent(-90, 39, -89, 40),
                Categories = new[] { "Address", "POI" },
                OutFields = new[] { "City", "Region" }
            });

        Assert.Collection(
            results,
            matched =>
            {
                Assert.Equal(1, matched.InputId);
                Assert.Equal("123 Main St", matched.InputAddress);
                Assert.Equal("M", matched.Status);
                Assert.NotNull(matched.Result);
                Assert.Equal("123 Main St, Springfield, IL", matched.Result!.Address);
                Assert.Equal("Springfield", matched.Attributes["City"]);
                Assert.Null(matched.ErrorMessage);
            },
            unmatched =>
            {
                Assert.Equal(2, unmatched.InputId);
                Assert.Equal("missing place", unmatched.InputAddress);
                Assert.Equal("U", unmatched.Status);
                Assert.Null(unmatched.Result);
                Assert.Contains("status 'U'", unmatched.ErrorMessage);
            });
    }

    [Fact]
    public async Task BatchGeocode_ReturnsMatchedResults()
    {
        var responseJson = """
        {
            "locations": [
                {
                    "address": "123 Main St, Springfield, IL",
                    "location": { "x": -89.6501, "y": 39.7817 },
                    "score": 98.1,
                    "attributes": { "ResultID": 1, "Status": "M" }
                },
                {
                    "address": "",
                    "location": null,
                    "score": 0,
                    "attributes": { "ResultID": 2, "Status": "U" }
                }
            ]
        }
        """;

        var client = CreateGeocodingClient(_ => Task.FromResult(CreateGeoJsonResponse(responseJson)));

        var results = await client.BatchGeocodeAsync(new[] { "123 Main St", "missing place" });

        var result = Assert.Single(results);
        Assert.Equal("123 Main St, Springfield, IL", result.Address);
    }

    [Fact]
    public async Task BatchGeocode_EmptyInputs_ReturnsEmptyList()
    {
        var client = CreateGeocodingClient(_ => Task.FromResult(CreateGeoJsonResponse("{}")));

        var results = await client.BatchGeocodeAsync(Array.Empty<string>());

        Assert.Empty(results);
    }

    private static IReadOnlyDictionary<string, string> ParseForm(string body)
        => body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Decode(pair[0]),
                pair => pair.Length == 2 ? Decode(pair[1]) : string.Empty,
                StringComparer.Ordinal);

    private static string Decode(string value)
        => Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));
}
