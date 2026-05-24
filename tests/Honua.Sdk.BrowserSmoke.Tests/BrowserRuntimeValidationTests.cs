using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Xunit;

namespace Honua.Sdk.BrowserSmoke.Tests;

public sealed class BrowserRuntimeValidationTests
{
    [Fact]
    public async Task RuntimeValidation_UsesBrowserHttpClientAndCorsPreflight()
    {
        if (!IsEnabled(Environment.GetEnvironmentVariable("HONUA_BROWSER_RUNTIME_SMOKE")))
        {
            return;
        }

        var appUri = CreateLoopbackUri();
        var apiUri = CreateLoopbackUri();
        var observed = new ConcurrentQueue<ObservedRequest>();

        await using var appHost = await StartBrowserSmokeHostAsync(appUri);
        await using var apiHost = await StartFakeHonuaApiAsync(apiUri, appUri.GetLeftPart(UriPartial.Authority), observed);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });

        var page = await browser.NewPageAsync();
        var diagnostics = new ConcurrentQueue<string>();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Enqueue(message.Text);
            }
        };
        page.PageError += (_, error) => diagnostics.Enqueue(error);

        var target = new UriBuilder(appUri)
        {
            Query = BuildQuery(apiUri)
        }.Uri;

        await page.GotoAsync(target.ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });

        var statusElement = page.Locator("[data-browser-smoke-status]");
        await WaitForRuntimeValidationAsync(page, diagnostics);

        var status = await statusElement.GetAttributeAsync("data-browser-smoke-status");
        var text = await statusElement.InnerTextAsync();

        Assert.True(
            string.Equals("passed", status, StringComparison.Ordinal),
            $"Browser smoke status was '{status}'. Page text: {text}. Browser diagnostics: {string.Join(" | ", diagnostics)}");

        Assert.Contains(observed, request =>
            string.Equals(request.Method, HttpMethods.Options, StringComparison.Ordinal) &&
            string.Equals(request.Origin, appUri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal) &&
            request.AccessControlRequestHeaders?.Contains("x-honua-browser-smoke", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Contains(observed, request =>
            string.Equals(request.Path, "/ogc/features/collections/parks/items", StringComparison.Ordinal) &&
            string.Equals(request.ProbeHeader, "true", StringComparison.Ordinal));
        Assert.Contains(observed, request =>
            string.Equals(request.Path, "/rest/services/World/GeocodeServer/findAddressCandidates", StringComparison.Ordinal));
        Assert.Contains(observed, request =>
            string.Equals(request.Path, "/rest/services/sdk-demo/FeatureServer/0", StringComparison.Ordinal));
        Assert.Contains(observed, request =>
            string.Equals(request.Path, "/wfs", StringComparison.Ordinal));
        Assert.Contains(observed, request =>
            string.Equals(request.Path, "/ogc/processes/processes", StringComparison.Ordinal));

        GC.KeepAlive(appHost);
        GC.KeepAlive(apiHost);
    }

    private static async Task WaitForRuntimeValidationAsync(IPage page, ConcurrentQueue<string> diagnostics)
    {
        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                  const element = document.querySelector('[data-browser-smoke-status]');
                  const status = element?.getAttribute('data-browser-smoke-status');
                  return status && status !== 'running';
                }
                """,
                null,
                new PageWaitForFunctionOptions
                {
                    Timeout = 90_000,
                }).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            var content = await page.ContentAsync().ConfigureAwait(false);
            throw new TimeoutException(
                $"Browser runtime validation did not finish. Page content: {content}. Browser diagnostics: {string.Join(" | ", diagnostics)}",
                ex);
        }
    }

    private static string BuildQuery(Uri apiUri)
    {
        var parameters = new Dictionary<string, string>
        {
            ["live"] = "1",
            ["baseUrl"] = apiUri.ToString(),
            ["corsProbe"] = "1",
            ["collectionId"] = "parks",
            ["serviceName"] = "sdk-demo",
            ["layerId"] = "0",
            ["wfsTypeName"] = "parcels",
            ["address"] = "Honolulu, HI",
        };

        return string.Join(
            "&",
            parameters.Select(pair =>
                string.Concat(Uri.EscapeDataString(pair.Key), "=", Uri.EscapeDataString(pair.Value))));
    }

    private static async Task<WebApplication> StartBrowserSmokeHostAsync(Uri appUri)
    {
        var repoRoot = GetRepoRoot();
        var webRoot = Path.Combine(repoRoot, "tests", "Honua.Sdk.BrowserSmoke", "wwwroot");
        var frameworkRoot = Path.Combine(
            repoRoot,
            "tests",
            "Honua.Sdk.BrowserSmoke",
            "bin",
            "Release",
            "net10.0",
            "wwwroot",
            "_framework");
        if (!Directory.Exists(frameworkRoot))
        {
            throw new DirectoryNotFoundException(
                "Could not find the Honua.Sdk.BrowserSmoke Release _framework output. Build the browser smoke app before running this test.");
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            WebRootPath = webRoot,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(appUri.ToString());

        var app = builder.Build();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = CreateContentTypeProvider(),
            FileProvider = new PhysicalFileProvider(frameworkRoot),
            RequestPath = "/_framework",
        });
        app.MapFallback(async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(Path.Combine(webRoot, "index.html"), context.RequestAborted)
                .ConfigureAwait(false);
        });

        await app.StartAsync().ConfigureAwait(false);
        return app;
    }

    private static async Task<WebApplication> StartFakeHonuaApiAsync(
        Uri apiUri,
        string allowedOrigin,
        ConcurrentQueue<ObservedRequest> observed)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = []
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(apiUri.ToString());

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            AddCorsHeaders(context, allowedOrigin);
            observed.Enqueue(ObservedRequest.From(context));

            if (string.Equals(context.Request.Method, HttpMethods.Options, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await next(context).ConfigureAwait(false);
        });

        app.MapGet("/ogc/features/collections/{collectionId}/items", (string collectionId) => Results.Json(new
        {
            type = "FeatureCollection",
            numberMatched = 1,
            numberReturned = 1,
            features = new[]
            {
                new
                {
                    type = "Feature",
                    id = "park-1",
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new[] { -157.8557, 21.3045 },
                    },
                    properties = new
                    {
                        name = collectionId,
                        status = "open",
                    },
                },
            },
        }));

        app.MapGet("/rest/services/World/GeocodeServer/findAddressCandidates", () => Results.Json(new
        {
            spatialReference = new
            {
                wkid = 4326,
            },
            candidates = new[]
            {
                new
                {
                    address = "Honolulu, HI",
                    location = new
                    {
                        x = -157.8583,
                        y = 21.3069,
                    },
                    score = 100.0,
                    attributes = new
                    {
                        Match_addr = "Honolulu, HI",
                    },
                },
            },
        }));

        app.MapGet("/rest/services/sdk-demo/FeatureServer/0", () => Results.Json(new
        {
            id = 0,
            name = "Parks",
            description = "Browser smoke fixture layer",
            geometryType = "esriGeometryPoint",
            objectIdField = "OBJECTID",
            maxRecordCount = 1000,
            capabilities = "Query",
            hasAttachments = false,
            supportsStatistics = true,
            supportsAdvancedQueries = true,
            spatialReference = new
            {
                wkid = 4326,
            },
            fields = new object[]
            {
                new
                {
                    name = "OBJECTID",
                    type = "esriFieldTypeOID",
                    alias = "OBJECTID",
                },
                new
                {
                    name = "name",
                    type = "esriFieldTypeString",
                    alias = "Name",
                    length = 255,
                },
            },
        }));

        app.MapGet("/wfs", () => Results.Text(WfsCapabilities, "application/xml"));
        app.MapGet("/ogc/processes/processes", () => Results.Json(new
        {
            processes = new[]
            {
                new
                {
                    id = "honua.analysis",
                    title = "Analysis Plan",
                    version = "1.0.0",
                    jobControlOptions = new[] { "async-execute" },
                    outputTransmission = new[] { "value" },
                    links = Array.Empty<object>(),
                },
            },
            links = Array.Empty<object>(),
        }));

        await app.StartAsync().ConfigureAwait(false);
        return app;
    }

    private static void AddCorsHeaders(HttpContext context, string allowedOrigin)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.Equals(origin, allowedOrigin, StringComparison.Ordinal))
        {
            return;
        }

        context.Response.Headers["Access-Control-Allow-Origin"] = allowedOrigin;
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] =
            "authorization, content-type, x-api-key, x-honua-browser-smoke";
        context.Response.Headers["Access-Control-Max-Age"] = "600";
        context.Response.Headers["Vary"] = "Origin";
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Honua.Sdk.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the honua-sdk-dotnet repository root.");
    }

    private static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".wasm"] = "application/wasm";
        provider.Mappings[".dat"] = "application/octet-stream";
        provider.Mappings[".pdb"] = "application/octet-stream";
        return provider;
    }

    private static Uri CreateLoopbackUri()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return new Uri(FormattableString.Invariant($"http://127.0.0.1:{port}/"));
    }

    private static bool IsEnabled(string? value)
        => value is not null &&
           (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));

    private sealed record ObservedRequest(
        string Method,
        string Path,
        string? Origin,
        string? AccessControlRequestHeaders,
        string? ProbeHeader)
    {
        public static ObservedRequest From(HttpContext context)
            => new(
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Request.Headers.Origin.ToString(),
                context.Request.Headers.AccessControlRequestHeaders.ToString(),
                context.Request.Headers["X-Honua-Browser-Smoke"].ToString());
    }

    private const string WfsCapabilities = """
        <?xml version="1.0" encoding="UTF-8"?>
        <wfs:WFS_Capabilities version="2.0.0"
          xmlns:wfs="http://www.opengis.net/wfs/2.0"
          xmlns:ows="http://www.opengis.net/ows/1.1"
          xmlns:xlink="http://www.w3.org/1999/xlink">
          <ows:ServiceIdentification>
            <ows:Title>Honua WFS</ows:Title>
            <ows:Abstract>Browser smoke WFS service</ows:Abstract>
            <ows:ServiceType>WFS</ows:ServiceType>
            <ows:ServiceTypeVersion>2.0.0</ows:ServiceTypeVersion>
          </ows:ServiceIdentification>
          <ows:OperationsMetadata>
            <ows:Operation name="GetFeature">
              <ows:Parameter name="outputFormat">
                <ows:AllowedValues>
                  <ows:Value>application/geo+json</ows:Value>
                </ows:AllowedValues>
              </ows:Parameter>
            </ows:Operation>
          </ows:OperationsMetadata>
          <wfs:FeatureTypeList>
            <wfs:FeatureType>
              <wfs:Name>parcels</wfs:Name>
              <wfs:Title>Parcels</wfs:Title>
              <wfs:DefaultCRS>urn:ogc:def:crs:EPSG::4326</wfs:DefaultCRS>
              <ows:WGS84BoundingBox>
                <ows:LowerCorner>-180.0 -90.0</ows:LowerCorner>
                <ows:UpperCorner>180.0 90.0</ows:UpperCorner>
              </ows:WGS84BoundingBox>
            </wfs:FeatureType>
          </wfs:FeatureTypeList>
        </wfs:WFS_Capabilities>
        """;
}
