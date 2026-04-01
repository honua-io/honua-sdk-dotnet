using AdminBootstrapConsole;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Models;
using Moq;
using Xunit;

namespace AdminBootstrapConsole.Tests;

public sealed class BootstrapWorkflowTests
{
    private static readonly Guid TestConnectionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task RunAsync_StopsAfterCompatibilityCheck_WhenServerUnsupported()
    {
        var adminClient = new Mock<IHonuaAdminClient>(MockBehavior.Strict);
        var grpcClient = new Mock<IHonuaGrpcClient>(MockBehavior.Strict);

        adminClient
            .Setup(client => client.CheckCompatibilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCompatibility(isSupported: false, serverVersion: "0.0.1"));

        var workflow = CreateWorkflow(adminClient, grpcClient);

        var ex = await Assert.ThrowsAsync<BootstrapCompatibilityException>(
            () => workflow.RunAsync(new StringWriter()));

        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
        adminClient.Verify(client => client.CheckCompatibilityAsync(It.IsAny<CancellationToken>()), Times.Once);
        adminClient.VerifyNoOtherCalls();
        grpcClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAsync_ReusesConnection_EnablesGrpc_AndRunsBoundedVerificationQuery()
    {
        var adminClient = new Mock<IHonuaAdminClient>(MockBehavior.Strict);
        var grpcClient = new Mock<IHonuaGrpcClient>(MockBehavior.Strict);
        var options = CreateOptions();
        var writer = new StringWriter();
        var sequence = new MockSequence();
        var connection = CreateConnection(options);
        var table = CreateTable();
        var layer = CreateLayer(options, enabled: false);

        adminClient.InSequence(sequence)
            .Setup(client => client.CheckCompatibilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCompatibility(isSupported: true));
        adminClient.InSequence(sequence)
            .Setup(client => client.TestDraftConnectionAsync(
                It.Is<CreateSecureConnectionRequest>(request =>
                    request.Name == options.ConnectionName &&
                    request.Host == options.DbHost &&
                    request.Port == options.DbPort &&
                    request.DatabaseName == options.DbName &&
                    request.Username == options.DbUser &&
                    request.Password == options.DbPassword &&
                    request.SslRequired == options.DbSslRequired &&
                    request.SslMode == options.DbSslMode),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionTestResult
            {
                ConnectionId = Guid.Empty,
                ConnectionName = options.ConnectionName,
                IsHealthy = true,
                TestedAt = DateTimeOffset.UtcNow,
                Message = "Draft OK"
            });
        adminClient.InSequence(sequence)
            .Setup(client => client.ListConnectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([connection]);
        adminClient.InSequence(sequence)
            .Setup(client => client.TestConnectionAsync(TestConnectionId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionTestResult
            {
                ConnectionId = TestConnectionId,
                ConnectionName = options.ConnectionName,
                IsHealthy = true,
                TestedAt = DateTimeOffset.UtcNow,
                Message = "Healthy"
            });
        adminClient.InSequence(sequence)
            .Setup(client => client.DiscoverTablesAsync(TestConnectionId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TableDiscoveryResponse { Tables = [table] });
        adminClient.InSequence(sequence)
            .Setup(client => client.ListLayersAsync(TestConnectionId.ToString("D"), options.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([layer]);
        adminClient.InSequence(sequence)
            .Setup(client => client.SetLayerEnabledAsync(TestConnectionId.ToString("D"), layer.LayerId, true, options.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLayer(options, enabled: true));
        adminClient.InSequence(sequence)
            .Setup(client => client.GetServiceSettingsAsync(options.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceSettingsResponse
            {
                ServiceName = options.ServiceName,
                EnabledProtocols = ["FeatureServer"],
                AvailableProtocols = ["FeatureServer", "Grpc"]
            });
        adminClient.InSequence(sequence)
            .Setup(client => client.UpdateProtocolsAsync(
                options.ServiceName,
                It.Is<IReadOnlyList<string>>(protocols =>
                    protocols.SequenceEqual(new[] { "FeatureServer", "Grpc" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceSettingsResponse
            {
                ServiceName = options.ServiceName,
                EnabledProtocols = ["FeatureServer", "Grpc"],
                AvailableProtocols = ["FeatureServer", "Grpc"]
            });

        grpcClient
            .Setup(client => client.QueryFeaturesAsync(
                It.Is<QueryFeaturesRequest>(request =>
                    request.ServiceId == options.ServiceName &&
                    request.LayerId == layer.LayerId &&
                    request.Where == "1=1" &&
                    request.ReturnGeometry == false &&
                    request.ResultRecordCount == 3 &&
                    request.OrderBy == "id" &&
                    request.OutFields != null &&
                    request.OutFields.SequenceEqual(new[] { "id", "name", "status" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryFeaturesResponse
            {
                Features =
                [
                    new Feature
                    {
                        Id = 1,
                        Attributes = new Dictionary<string, object?>
                        {
                            ["id"] = 1,
                            ["name"] = "Honolulu Harbor",
                            ["status"] = "active"
                        }
                    }
                ]
            });

        var workflow = new BootstrapWorkflow(adminClient.Object, grpcClient.Object, options);
        var summary = await workflow.RunAsync(writer);

        Assert.False(summary.CreatedConnection);
        Assert.False(summary.PublishedLayer);
        Assert.True(summary.UpdatedProtocols);
        Assert.Equal(["id", "name", "status"], summary.VerificationFields);
        Assert.Single(summary.Verification.Features);
        Assert.Contains("=== Verify ===", writer.ToString(), StringComparison.Ordinal);

        adminClient.VerifyAll();
        grpcClient.VerifyAll();
    }

    [Fact]
    public async Task RunAsync_CreatesAndPublishes_WhenResourcesDoNotExist()
    {
        var adminClient = new Mock<IHonuaAdminClient>(MockBehavior.Strict);
        var grpcClient = new Mock<IHonuaGrpcClient>(MockBehavior.Strict);
        var options = CreateOptions();
        var writer = new StringWriter();
        var sequence = new MockSequence();
        var table = CreateTable();
        var createdConnection = CreateConnection(options);
        var publishedLayer = CreateLayer(options, enabled: true);

        adminClient.InSequence(sequence)
            .Setup(client => client.CheckCompatibilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCompatibility(isSupported: true));
        adminClient.InSequence(sequence)
            .Setup(client => client.TestDraftConnectionAsync(It.IsAny<CreateSecureConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionTestResult
            {
                ConnectionId = Guid.Empty,
                ConnectionName = options.ConnectionName,
                IsHealthy = true,
                TestedAt = DateTimeOffset.UtcNow,
                Message = "Draft OK"
            });
        adminClient.InSequence(sequence)
            .Setup(client => client.ListConnectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SecureConnectionSummary>());
        adminClient.InSequence(sequence)
            .Setup(client => client.CreateConnectionAsync(It.IsAny<CreateSecureConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdConnection);
        adminClient.InSequence(sequence)
            .Setup(client => client.DiscoverTablesAsync(TestConnectionId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TableDiscoveryResponse { Tables = [table] });
        adminClient.InSequence(sequence)
            .Setup(client => client.ListLayersAsync(TestConnectionId.ToString("D"), options.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PublishedLayerSummary>());
        adminClient.InSequence(sequence)
            .Setup(client => client.PublishLayerAsync(
                TestConnectionId.ToString("D"),
                It.Is<PublishLayerRequest>(request =>
                    request.Schema == options.Schema &&
                    request.Table == options.Table &&
                    request.LayerName == options.LayerName &&
                    request.GeometryColumn == "geom" &&
                    request.GeometryType == "POINT" &&
                    request.Srid == 4326 &&
                    request.PrimaryKey == "id" &&
                    request.ServiceName == options.ServiceName &&
                    request.Enabled),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishedLayer);
        adminClient.InSequence(sequence)
            .Setup(client => client.SetLayerEnabledAsync(TestConnectionId.ToString("D"), publishedLayer.LayerId, true, options.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishedLayer);
        adminClient.InSequence(sequence)
            .Setup(client => client.GetServiceSettingsAsync(options.ServiceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceSettingsResponse
            {
                ServiceName = options.ServiceName,
                EnabledProtocols = ["FeatureServer", "Grpc"],
                AvailableProtocols = ["FeatureServer", "Grpc"]
            });

        grpcClient
            .Setup(client => client.QueryFeaturesAsync(It.IsAny<QueryFeaturesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryFeaturesResponse { Features = [] });

        var workflow = new BootstrapWorkflow(adminClient.Object, grpcClient.Object, options);
        var summary = await workflow.RunAsync(writer);

        Assert.True(summary.CreatedConnection);
        Assert.True(summary.PublishedLayer);
        Assert.False(summary.UpdatedProtocols);
        Assert.Empty(summary.Verification.Features);
        Assert.Contains("currently has no rows", writer.ToString(), StringComparison.OrdinalIgnoreCase);

        adminClient.VerifyAll();
        grpcClient.VerifyAll();
    }

    [Fact]
    public async Task RunAsync_FailsWhenNamedConnectionDoesNotMatchConfiguredTarget()
    {
        var adminClient = new Mock<IHonuaAdminClient>(MockBehavior.Strict);
        var grpcClient = new Mock<IHonuaGrpcClient>(MockBehavior.Strict);
        var options = CreateOptions();

        adminClient
            .Setup(client => client.CheckCompatibilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCompatibility(isSupported: true));
        adminClient
            .Setup(client => client.TestDraftConnectionAsync(It.IsAny<CreateSecureConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionTestResult
            {
                ConnectionId = Guid.Empty,
                ConnectionName = options.ConnectionName,
                IsHealthy = true,
                TestedAt = DateTimeOffset.UtcNow,
                Message = "Draft OK"
            });
        adminClient
            .Setup(client => client.ListConnectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SecureConnectionSummary
                {
                    ConnectionId = TestConnectionId,
                    Name = options.ConnectionName,
                    Host = "other-host",
                    Port = options.DbPort,
                    DatabaseName = options.DbName,
                    Username = options.DbUser,
                    SslRequired = options.DbSslRequired,
                    SslMode = options.DbSslMode
                }
            ]);

        var workflow = new BootstrapWorkflow(adminClient.Object, grpcClient.Object, options);

        var ex = await Assert.ThrowsAsync<HonuaAdminOperationException>(
            () => workflow.RunAsync(new StringWriter()));

        Assert.Equal("CreateConnection", ex.Operation);
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        grpcClient.VerifyNoOtherCalls();
    }

    private static BootstrapWorkflow CreateWorkflow(
        Mock<IHonuaAdminClient> adminClient,
        Mock<IHonuaGrpcClient> grpcClient)
    {
        return new BootstrapWorkflow(adminClient.Object, grpcClient.Object, CreateOptions());
    }

    private static BootstrapOptions CreateOptions()
    {
        return new BootstrapOptions
        {
            ServerUri = new Uri("http://localhost:8080"),
            ConnectionName = "sdk-demo-postgres",
            DbHost = "postgres",
            DbPort = 5432,
            DbName = "honua_dev",
            DbUser = "honua_user",
            DbPassword = "honua_password",
            DbSslRequired = false,
            DbSslMode = "Prefer",
            ServiceName = "sdk_demo",
            Schema = "public",
            Table = "sdk_demo_points",
            LayerName = "sdk_demo_points"
        };
    }

    private static ServerCompatibilityResult CreateCompatibility(bool isSupported, string serverVersion = "1.0.0")
    {
        return new ServerCompatibilityResult
        {
            IsSupported = isSupported,
            UnsupportedReason = isSupported ? null : "Server version is below the supported baseline.",
            Capabilities = new AdminCapabilitiesResponse
            {
                Compatibility = new AdminCompatibilityInfo
                {
                    ServerVersion = serverVersion,
                    ReleaseChannel = "stable",
                    ControlPlaneApi = new ControlPlaneApiCompatibility
                    {
                        Major = 1,
                        BasePath = "/api/v1"
                    }
                }
            }
        };
    }

    private static SecureConnectionSummary CreateConnection(BootstrapOptions options)
    {
        return new SecureConnectionSummary
        {
            ConnectionId = TestConnectionId,
            Name = options.ConnectionName,
            Host = options.DbHost,
            Port = options.DbPort,
            DatabaseName = options.DbName,
            Username = options.DbUser,
            SslRequired = options.DbSslRequired,
            SslMode = options.DbSslMode,
            StorageType = "managed",
            IsActive = true,
            HealthStatus = "Healthy",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        };
    }

    private static TableInfo CreateTable()
    {
        return new TableInfo
        {
            Schema = "public",
            Table = "sdk_demo_points",
            GeometryColumn = "geom",
            GeometryType = "POINT",
            Srid = 4326,
            EstimatedRows = 3,
            Columns =
            [
                new ColumnInfo { Name = "id", DataType = "integer", IsPrimaryKey = true },
                new ColumnInfo { Name = "name", DataType = "text" },
                new ColumnInfo { Name = "status", DataType = "text" },
                new ColumnInfo { Name = "geom", DataType = "geometry" }
            ]
        };
    }

    private static PublishedLayerSummary CreateLayer(BootstrapOptions options, bool enabled)
    {
        return new PublishedLayerSummary
        {
            LayerId = 42,
            LayerName = options.LayerName,
            Schema = options.Schema,
            Table = options.Table,
            GeometryType = "Point",
            Srid = 4326,
            PrimaryKey = "id",
            FieldCount = 4,
            Enabled = enabled,
            ServiceName = options.ServiceName
        };
    }
}
