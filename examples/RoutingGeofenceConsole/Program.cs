using System.Net.Http.Headers;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.GeoServices;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.Routing;
using RoutingGeofenceConsole;

var options = RoutingGeofenceOptions.FromEnvironment();
if (options.UseServer && HasCredentials(options.ApiKey, options.BearerToken) && RequiresHttpsForAuthentication(options.ServerUri))
{
    Console.Error.WriteLine("Authenticated routing requests require HTTPS, except loopback HTTP for local development.");
    return 2;
}

using var owner = options.UseServer ? CreateServerRoutingClient(options) : null;
var client = owner?.Client ?? new SimulatedRoutingClient();

try
{
    var summary = await RoutingGeofenceDemo.RunAsync(
        Console.Out,
        client,
        options.UseServer ? "server" : "simulated",
        options.ServiceId,
        options.RouteLayerName);
    return summary.GeofenceTransitions.Count >= 4 ? 0 : 1;
}
catch (HonuaFeatureServerException ex)
{
    Console.Error.WriteLine($"Routing request failed: {(int)ex.StatusCode} {ex.Message}");
    return 3;
}

static ServerRoutingClientOwner CreateServerRoutingClient(RoutingGeofenceOptions options)
{
    var transport = new HttpClientHandler();
    var auth = new DemoAuthHandler(options.ApiKey, options.BearerToken, transport);
    var http = new HttpClient(auth)
    {
        BaseAddress = options.ServerUri
    };
    var clientOptions = new HonuaGeoServicesClientOptions
    {
        BaseAddress = options.ServerUri,
        ApiKey = options.ApiKey,
        BearerToken = options.BearerToken,
        RoutingServiceId = options.ServiceId,
        RoutingRouteLayerName = options.RouteLayerName
    };

    return new ServerRoutingClientOwner(new HonuaRoutingClient(http, clientOptions), http);
}

static bool HasCredentials(string? apiKey, string? bearerToken) =>
    !string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(bearerToken);

static bool RequiresHttpsForAuthentication(Uri uri)
{
    if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        (!uri.IsLoopback && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}

internal sealed class DemoAuthHandler : DelegatingHandler
{
    private readonly string? _apiKey;
    private readonly string? _bearerToken;

    public DemoAuthHandler(string? apiKey, string? bearerToken, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _apiKey = apiKey;
        _bearerToken = bearerToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        }

        if (!string.IsNullOrWhiteSpace(_bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

internal sealed record ServerRoutingClientOwner(IHonuaRoutingClient Client, HttpClient Http) : IDisposable
{
    public void Dispose() => Http.Dispose();
}
