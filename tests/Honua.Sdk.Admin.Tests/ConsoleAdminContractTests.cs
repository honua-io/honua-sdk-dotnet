// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class ConsoleAdminContractTests
{
    private static readonly Guid RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SubscriberId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task RbacEndpoints_ReturnTypedContractsForRouteGuards()
    {
        var observedPaths = new List<string>();
        var client = TestHelpers.CreateClient(request =>
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            observedPaths.Add(path);
            return Task.FromResult(path switch
            {
                "/api/v1/admin/roles/" => TestHelpers.CreateJsonResponse(new[]
                {
                    CreateRole()
                }),
                "/api/v1/admin/users/?source=oidc&role=publisher&active=true&limit=10&offset=5" =>
                    TestHelpers.CreateJsonResponse(new UserListResponse
                    {
                        Users = [CreateUser()],
                        TotalCount = 1
                    }),
                "/api/v1/admin/users/user-operator/effective-permissions" =>
                    TestHelpers.CreateJsonResponse(CreateEffectivePermissions()),
                _ => TestHelpers.CreateErrorResponse(System.Net.HttpStatusCode.NotFound, "Unexpected route.")
            });
        });

        var roles = await client.ListRolesAsync();
        var users = await client.ListUsersAsync(new UserListQuery
        {
            Source = "oidc",
            Role = "publisher",
            Active = true,
            Limit = 10,
            Offset = 5
        });
        var effective = await client.GetEffectivePermissionsAsync("user-operator");

        Assert.Equal(RoleId, roles.Single().RoleId);
        Assert.Equal("user-operator", users.Users.Single().UserId);
        Assert.Contains(effective.Permissions, permission => permission.Operation == "publish");
        Assert.Equal(
            [
                "/api/v1/admin/roles/",
                "/api/v1/admin/users/?source=oidc&role=publisher&active=true&limit=10&offset=5",
                "/api/v1/admin/users/user-operator/effective-permissions"
            ],
            observedPaths);
    }

    [Fact]
    public async Task AlertsEventsAndStreamingEndpoints_ReturnConsoleControlPlaneContracts()
    {
        var observed = new List<(HttpMethod Method, string Path)>();
        string? ruleBody = null;
        var client = TestHelpers.CreateClient(async request =>
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            observed.Add((request.Method, path));
            if (request.Content is not null)
            {
                ruleBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            return path switch
            {
                "/api/v1/admin/alerts/zones?serviceId=parcels" => TestHelpers.CreateJsonResponse(new[]
                {
                    CreateAlertZone()
                }),
                "/api/v1/admin/alerts/rules" when request.Method == HttpMethod.Post =>
                    TestHelpers.CreateJsonResponse(CreateAlertRule()),
                "/api/v1/admin/feature-events/replay?cursor=1001&limit=10" =>
                    CreateServerFeatureEventReplayResponse(),
                "/api/v1/admin/operations/streaming/subscribers" =>
                    TestHelpers.CreateJsonResponse(CreateSubscriberList()),
                "/api/v1/admin/operations/streaming/subscribers/33333333-3333-3333-3333-333333333333" =>
                    TestHelpers.CreateJsonResponse(new { disconnected = true }),
                _ => TestHelpers.CreateErrorResponse(System.Net.HttpStatusCode.NotFound, "Unexpected route.")
            };
        });

        var zones = await client.ListAlertZonesAsync("parcels");
        var rule = await client.CreateAlertRuleAsync(new AlertRuleRequest
        {
            ServiceId = "parcels",
            LayerId = 1,
            ZoneId = 10,
            RuleName = "Inspection Dwell",
            TriggerType = "dwell",
            Channels = ["websocket"]
        });
        var replay = await client.ReplayFeatureEventsAsync(new FeatureEventReplayQuery
        {
            Cursor = 1001,
            Limit = 10
        });
        var subscribers = await client.ListStreamingSubscribersAsync();
        await client.DisconnectStreamingSubscriberAsync(SubscriberId);

        Assert.Equal(10, zones.Single().ZoneId);
        Assert.Equal(20, rule.RuleId);
        Assert.Contains("\"ruleName\":\"Inspection Dwell\"", ruleBody, StringComparison.Ordinal);
        Assert.Equal(1002, replay.NextCursor);
        Assert.Equal(SubscriberId, subscribers.Subscribers.Single().SubscriberId);
        Assert.Equal(
            [
                (HttpMethod.Get, "/api/v1/admin/alerts/zones?serviceId=parcels"),
                (HttpMethod.Post, "/api/v1/admin/alerts/rules"),
                (HttpMethod.Get, "/api/v1/admin/feature-events/replay?cursor=1001&limit=10"),
                (HttpMethod.Get, "/api/v1/admin/operations/streaming/subscribers"),
                (HttpMethod.Delete, "/api/v1/admin/operations/streaming/subscribers/33333333-3333-3333-3333-333333333333")
            ],
            observed);
    }

    [Fact]
    public void ConsoleFixtures_DeserializeThroughAdminJsonContext()
    {
        using var metadata = LoadFixture("metadata-rbac.v1.json");
        var metadataRoot = metadata.RootElement;
        var resources = JsonSerializer.Deserialize(
            metadataRoot.GetProperty("metadataResources").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponseMetadataResourceArray);
        var roles = JsonSerializer.Deserialize(
            metadataRoot.GetProperty("roles").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponseRoleResponseArray);
        var users = JsonSerializer.Deserialize(
            metadataRoot.GetProperty("users").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponseUserListResponse);
        var permissions = JsonSerializer.Deserialize(
            metadataRoot.GetProperty("effectivePermissions").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponseEffectivePermissionsResponse);

        Assert.Equal("Layer", resources?.Data?.Single().Kind);
        Assert.Equal("publisher", roles?.Data?.Single().Name);
        Assert.Equal("user-operator", users?.Data?.Users.Single().UserId);
        Assert.Equal("publish", permissions?.Data?.Permissions.Single(permission => permission.Operation == "publish").Operation);

        using var publishing = LoadFixture("admin-publishing-workflow.v1.json");
        var publishingRoot = publishing.RootElement;
        var tables = JsonSerializer.Deserialize(
            publishingRoot.GetProperty("tableDiscovery").GetRawText(),
            HonuaAdminJsonContext.Default.TableDiscoveryResponse);
        var publishRequest = JsonSerializer.Deserialize(
            publishingRoot.GetProperty("publishLayerRequest").GetRawText(),
            HonuaAdminJsonContext.Default.PublishLayerRequest);
        var publishedLayer = JsonSerializer.Deserialize(
            publishingRoot.GetProperty("publishedLayer").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponsePublishedLayerSummary);
        var manifest = JsonSerializer.Deserialize(
            publishingRoot.GetProperty("manifest").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponseMetadataManifest);
        var applyRequest = JsonSerializer.Deserialize(
            publishingRoot.GetProperty("manifestApplyRequest").GetRawText(),
            HonuaAdminJsonContext.Default.ManifestApplyRequest);

        Assert.Equal("parcels", tables?.Tables.Single().Table);
        Assert.Equal("Parcels", publishRequest?.LayerName);
        Assert.Equal("parcels", publishedLayer?.Data?.ServiceName);
        Assert.Equal("sha256:console-publish-fixture", manifest?.Data?.ManifestHash);
        Assert.True(applyRequest?.DryRun);

        using var observability = LoadFixture("observability-dashboard.v1.json");
        var observabilityRoot = observability.RootElement;
        var recentErrors = JsonSerializer.Deserialize(
            observabilityRoot.GetProperty("recentErrors").GetRawText(),
            HonuaAdminJsonContext.Default.RecentErrorsResponse);
        var telemetry = JsonSerializer.Deserialize(
            observabilityRoot.GetProperty("telemetry").GetRawText(),
            HonuaAdminJsonContext.Default.TelemetryStatus);
        var migrations = JsonSerializer.Deserialize(
            observabilityRoot.GetProperty("migrations").GetRawText(),
            HonuaAdminJsonContext.Default.MigrationStatus);
        var featureEvents = JsonSerializer.Deserialize(
            observabilityRoot.GetProperty("featureEventReplay").GetRawText(),
            HonuaAdminJsonContext.Default.FeatureEventReplayResponse);

        Assert.Equal("server-1", recentErrors?.InstanceId);
        Assert.True(telemetry?.TracingEnabled);
        Assert.True(migrations?.IsReady);
        Assert.Equal(1002, featureEvents?.NextCursor);

        using var alerts = LoadFixture("alerts-rules.v1.json");
        var alertsRoot = alerts.RootElement;
        var zoneRequest = JsonSerializer.Deserialize(
            alertsRoot.GetProperty("zoneRequest").GetRawText(),
            HonuaAdminJsonContext.Default.AlertZoneRequest);
        var zones = JsonSerializer.Deserialize(
            alertsRoot.GetProperty("zones").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponseAlertZoneResponseArray);
        var ruleRequest = JsonSerializer.Deserialize(
            alertsRoot.GetProperty("ruleRequest").GetRawText(),
            HonuaAdminJsonContext.Default.AlertRuleRequest);
        var rules = JsonSerializer.Deserialize(
            alertsRoot.GetProperty("rules").GetRawText(),
            HonuaAdminJsonContext.Default.ApiResponseAlertRuleResponseArray);

        Assert.Equal("Downtown", zoneRequest?.ZoneName);
        Assert.Equal(10, zones?.Data?.Single().ZoneId);
        Assert.Equal("dwell", ruleRequest?.TriggerType);
        Assert.Equal(20, rules?.Data?.Single().RuleId);
        Assert.False(alertsRoot.GetProperty("invalidRuleError").GetProperty("success").GetBoolean());
    }

    private static RoleResponse CreateRole()
        => new()
        {
            RoleId = RoleId,
            Name = "publisher",
            Description = "Can publish layers.",
            IsBuiltIn = false,
            Permissions =
            [
                new PermissionGrantResponse
                {
                    Service = "parcels",
                    Layer = "0",
                    Operation = "publish"
                }
            ],
            CreatedAt = DateTimeOffset.Parse("2026-05-23T21:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-05-23T21:15:00Z", System.Globalization.CultureInfo.InvariantCulture)
        };

    private static UserResponse CreateUser()
        => new()
        {
            UserId = "user-operator",
            DisplayName = "Console Operator",
            Email = "operator@example.test",
            ProvisioningSource = "oidc",
            ProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            IsActive = true,
            Roles = ["operator", "publisher"],
            CreatedAt = DateTimeOffset.Parse("2026-05-23T21:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-05-23T21:15:00Z", System.Globalization.CultureInfo.InvariantCulture)
        };

    private static EffectivePermissionsResponse CreateEffectivePermissions()
        => new()
        {
            UserId = "user-operator",
            Roles = ["operator", "publisher"],
            Permissions =
            [
                new PermissionGrantResponse
                {
                    Service = "parcels",
                    Layer = "0",
                    Operation = "publish"
                }
            ],
            ResolvedAt = DateTimeOffset.Parse("2026-05-23T22:10:00Z", System.Globalization.CultureInfo.InvariantCulture)
        };

    private static AlertZoneResponse CreateAlertZone()
        => new()
        {
            ZoneId = 10,
            ServiceId = "parcels",
            ZoneName = "Downtown",
            Wkt = "POLYGON((-158 21,-157 21,-157 22,-158 22,-158 21))",
            Srid = 4326,
            Metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["owner"] = "operations"
            },
            IsActive = true
        };

    private static AlertRuleResponse CreateAlertRule()
        => new()
        {
            RuleId = 20,
            ServiceId = "parcels",
            LayerId = 1,
            ZoneId = 10,
            RuleName = "Inspection Dwell",
            TriggerType = "dwell",
            CooldownSeconds = 300,
            Severity = "warning",
            EditionRequired = "pro",
            Channels = ["websocket"],
            IsActive = true
        };

    private static HttpResponseMessage CreateServerFeatureEventReplayResponse()
        => new()
        {
            Content = new StringContent(
                """
                {
                  "Events": [
                    {
                      "EventId": "event-1",
                      "Cursor": 1001,
                      "Timestamp": "2026-05-23T22:29:00Z",
                      "ServiceId": "parcels",
                      "LayerId": 0,
                      "ObjectId": 42,
                      "Operation": "update",
                      "Protocol": "geoservices-feature-service",
                      "RequestId": "req-1",
                      "GeometryChanged": false
                    }
                  ],
                  "NextCursor": 1002,
                  "HasMore": false
                }
                """,
                System.Text.Encoding.UTF8,
                "application/json")
        };

    private static SubscriberListResponse CreateSubscriberList()
        => new()
        {
            SubscriberCount = 1,
            Subscribers =
            [
                new SubscriberInfoResponse
                {
                    SubscriberId = SubscriberId,
                    ConnectedAt = DateTimeOffset.Parse("2026-05-23T22:29:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    ClientLabel = "console",
                    DurationSeconds = 10
                }
            ],
            GeneratedAt = DateTimeOffset.Parse("2026-05-23T22:30:00Z", System.Globalization.CultureInfo.InvariantCulture)
        };

    private static JsonDocument LoadFixture(string name)
        => JsonDocument.Parse(File.ReadAllText(Path.Join(FindRepoRoot(), "contracts", "fixtures", "console", name)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Honua.Sdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the honua-sdk-dotnet repository root.");
    }
}
