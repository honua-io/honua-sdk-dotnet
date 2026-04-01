using AdminBootstrapConsole;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AdminBootstrapConsole.Tests;

public sealed class BootstrapOptionsTests
{
    [Fact]
    public void Load_UsesLocalPasswordFallback_WhenNoCredentialOverrideIsConfigured()
    {
        var configuration = CreateConfiguration();

        var options = BootstrapOptions.Load(configuration);

        Assert.Equal("honua_password", options.DbPassword);
        Assert.Null(options.DbSecretReference);
        Assert.Null(options.DbSecretType);
    }

    [Fact]
    public void Load_SuppressesPasswordFallback_WhenSecretReferenceIsConfiguredInAppSettings()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["HonuaBootstrap:DbSecretReference"] = "projects/demo/secrets/postgres",
            ["HonuaBootstrap:DbSecretType"] = "gcp-secret-manager"
        });

        var options = BootstrapOptions.Load(configuration);

        Assert.Null(options.DbPassword);
        Assert.Equal("projects/demo/secrets/postgres", options.DbSecretReference);
        Assert.Equal("gcp-secret-manager", options.DbSecretType);
    }

    [Fact]
    public void Load_AllowsEnvironmentOverrideToClearPassword_WhenUsingSecretReference()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["HonuaBootstrap:DbPassword"] = "honua_password",
            ["HONUA_BOOTSTRAP_DB_PASSWORD"] = string.Empty,
            ["HONUA_BOOTSTRAP_DB_SECRET_REFERENCE"] = "projects/demo/secrets/postgres",
            ["HONUA_BOOTSTRAP_DB_SECRET_TYPE"] = "gcp-secret-manager"
        });

        var options = BootstrapOptions.Load(configuration);

        Assert.Null(options.DbPassword);
        Assert.Equal("projects/demo/secrets/postgres", options.DbSecretReference);
        Assert.Equal("gcp-secret-manager", options.DbSecretType);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["HonuaBootstrap:ServerUrl"] = "http://localhost:8080",
            ["HonuaBootstrap:ConnectionName"] = "sdk-demo-postgres",
            ["HonuaBootstrap:DbHost"] = "postgres",
            ["HonuaBootstrap:DbPort"] = "5432",
            ["HonuaBootstrap:DbName"] = "honua_dev",
            ["HonuaBootstrap:DbUser"] = "honua_user",
            ["HonuaBootstrap:DbSslRequired"] = "false",
            ["HonuaBootstrap:DbSslMode"] = "Prefer",
            ["HonuaBootstrap:ServiceName"] = "sdk_demo",
            ["HonuaBootstrap:Schema"] = "public",
            ["HonuaBootstrap:Table"] = "sdk_demo_points",
            ["HonuaBootstrap:LayerName"] = "sdk_demo_points"
        };

        if (overrides is not null)
        {
            foreach (var entry in overrides)
            {
                values[entry.Key] = entry.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
