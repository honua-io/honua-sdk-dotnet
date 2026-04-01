using Honua.Sdk.Admin.Models;
using Microsoft.Extensions.Configuration;

namespace AdminBootstrapConsole;

public sealed class BootstrapOptions
{
    public Uri ServerUri { get; init; } = new("http://localhost:8080");

    public string? ApiKey { get; init; }

    public string? BearerToken { get; init; }

    public string ConnectionName { get; init; } = "sdk-demo-postgres";

    public string DbHost { get; init; } = "postgres";

    public int DbPort { get; init; } = 5432;

    public string DbName { get; init; } = "honua_dev";

    public string DbUser { get; init; } = "honua_user";

    public string? DbPassword { get; init; } = "honua_password";

    public string? DbSecretReference { get; init; }

    public string? DbSecretType { get; init; }

    public bool DbSslRequired { get; init; }

    public string DbSslMode { get; init; } = "Prefer";

    public string ServiceName { get; init; } = "sdk_demo";

    public string Schema { get; init; } = "public";

    public string Table { get; init; } = "sdk_demo_points";

    public string LayerName { get; init; } = "sdk_demo_points";

    public static BootstrapOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("HonuaBootstrap");
        var dbSecretReference = ReadValue(
            configuration,
            section,
            "DbSecretReference",
            "HONUA_BOOTSTRAP_DB_SECRET_REFERENCE",
            treatEmptyAsOverride: true);

        var options = new BootstrapOptions
        {
            ServerUri = ParseServerUri(ReadRequiredValue(
                configuration,
                section,
                "ServerUrl",
                "HONUA_BOOTSTRAP_SERVER_URL",
                "http://localhost:8080")),
            ApiKey = ReadValue(configuration, section, "ApiKey", "HONUA_BOOTSTRAP_API_KEY"),
            BearerToken = ReadValue(configuration, section, "BearerToken", "HONUA_BOOTSTRAP_BEARER_TOKEN"),
            ConnectionName = ReadRequiredValue(configuration, section, "ConnectionName", "HONUA_BOOTSTRAP_CONNECTION_NAME", "sdk-demo-postgres"),
            DbHost = ReadRequiredValue(configuration, section, "DbHost", "HONUA_BOOTSTRAP_DB_HOST", "postgres"),
            DbPort = ReadInt(configuration, section, "DbPort", "HONUA_BOOTSTRAP_DB_PORT", 5432),
            DbName = ReadRequiredValue(configuration, section, "DbName", "HONUA_BOOTSTRAP_DB_NAME", "honua_dev"),
            DbUser = ReadRequiredValue(configuration, section, "DbUser", "HONUA_BOOTSTRAP_DB_USER", "honua_user"),
            DbPassword = ReadValue(
                configuration,
                section,
                "DbPassword",
                "HONUA_BOOTSTRAP_DB_PASSWORD",
                string.IsNullOrWhiteSpace(dbSecretReference) ? "honua_password" : null,
                treatEmptyAsOverride: true),
            DbSecretReference = dbSecretReference,
            DbSecretType = ReadValue(
                configuration,
                section,
                "DbSecretType",
                "HONUA_BOOTSTRAP_DB_SECRET_TYPE",
                treatEmptyAsOverride: true),
            DbSslRequired = ReadBool(configuration, section, "DbSslRequired", "HONUA_BOOTSTRAP_DB_SSL_REQUIRED", false),
            DbSslMode = ReadRequiredValue(configuration, section, "DbSslMode", "HONUA_BOOTSTRAP_DB_SSL_MODE", "Prefer"),
            ServiceName = ReadRequiredValue(configuration, section, "ServiceName", "HONUA_BOOTSTRAP_SERVICE_NAME", "sdk_demo"),
            Schema = ReadRequiredValue(configuration, section, "Schema", "HONUA_BOOTSTRAP_SCHEMA", "public"),
            Table = ReadRequiredValue(configuration, section, "Table", "HONUA_BOOTSTRAP_TABLE", "sdk_demo_points"),
            LayerName = ReadRequiredValue(configuration, section, "LayerName", "HONUA_BOOTSTRAP_LAYER_NAME", "sdk_demo_points")
        };

        Validate(options);
        return options;
    }

    public CreateSecureConnectionRequest ToCreateConnectionRequest()
    {
        return new CreateSecureConnectionRequest
        {
            Name = ConnectionName,
            Host = DbHost,
            Port = DbPort,
            DatabaseName = DbName,
            Username = DbUser,
            Password = string.IsNullOrWhiteSpace(DbSecretReference) ? DbPassword : null,
            SecretReference = DbSecretReference,
            SecretType = DbSecretType,
            SslRequired = DbSslRequired,
            SslMode = DbSslMode
        };
    }

    private static Uri ParseServerUri(string rawValue)
    {
        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var uri))
        {
            throw new BootstrapConfigurationException(
                $"HONUA_BOOTSTRAP_SERVER_URL must be an absolute HTTP or HTTPS URI. Value: '{rawValue}'.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new BootstrapConfigurationException(
                "HONUA_BOOTSTRAP_SERVER_URL must use the http or https scheme.");
        }

        return uri;
    }

    private static void Validate(BootstrapOptions options)
    {
        ValidateRequired(options.ConnectionName, "HONUA_BOOTSTRAP_CONNECTION_NAME");
        ValidateRequired(options.DbHost, "HONUA_BOOTSTRAP_DB_HOST");
        ValidateRequired(options.DbName, "HONUA_BOOTSTRAP_DB_NAME");
        ValidateRequired(options.DbUser, "HONUA_BOOTSTRAP_DB_USER");
        ValidateRequired(options.DbSslMode, "HONUA_BOOTSTRAP_DB_SSL_MODE");
        ValidateRequired(options.ServiceName, "HONUA_BOOTSTRAP_SERVICE_NAME");
        ValidateRequired(options.Schema, "HONUA_BOOTSTRAP_SCHEMA");
        ValidateRequired(options.Table, "HONUA_BOOTSTRAP_TABLE");
        ValidateRequired(options.LayerName, "HONUA_BOOTSTRAP_LAYER_NAME");

        if (options.DbPort is < 1 or > 65535)
        {
            throw new BootstrapConfigurationException(
                $"HONUA_BOOTSTRAP_DB_PORT must be between 1 and 65535. Value: {options.DbPort}.");
        }

        var hasPassword = !string.IsNullOrWhiteSpace(options.DbPassword);
        var hasSecretReference = !string.IsNullOrWhiteSpace(options.DbSecretReference);

        if (!hasPassword && !hasSecretReference)
        {
            throw new BootstrapConfigurationException(
                "Set either HONUA_BOOTSTRAP_DB_PASSWORD or HONUA_BOOTSTRAP_DB_SECRET_REFERENCE.");
        }

        if (hasPassword && hasSecretReference)
        {
            throw new BootstrapConfigurationException(
                "Configure only one credential source: HONUA_BOOTSTRAP_DB_PASSWORD or HONUA_BOOTSTRAP_DB_SECRET_REFERENCE.");
        }

        if (hasSecretReference && string.IsNullOrWhiteSpace(options.DbSecretType))
        {
            throw new BootstrapConfigurationException(
                "HONUA_BOOTSTRAP_DB_SECRET_TYPE is required when HONUA_BOOTSTRAP_DB_SECRET_REFERENCE is set.");
        }

        if (!hasSecretReference && !string.IsNullOrWhiteSpace(options.DbSecretType))
        {
            throw new BootstrapConfigurationException(
                "HONUA_BOOTSTRAP_DB_SECRET_TYPE requires HONUA_BOOTSTRAP_DB_SECRET_REFERENCE.");
        }
    }

    private static void ValidateRequired(string value, string variableName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BootstrapConfigurationException($"{variableName} must not be empty.");
        }
    }

    private static string ReadRequiredValue(
        IConfiguration configuration,
        IConfigurationSection section,
        string sectionKey,
        string environmentKey,
        string fallback)
    {
        return ReadValue(configuration, section, sectionKey, environmentKey, fallback)
            ?? throw new BootstrapConfigurationException($"{environmentKey} must not be empty.");
    }

    private static string? ReadValue(
        IConfiguration configuration,
        IConfigurationSection section,
        string sectionKey,
        string environmentKey,
        string? fallback = null,
        bool treatEmptyAsOverride = false)
    {
        var environmentValue = configuration[environmentKey];
        if (environmentValue is not null)
        {
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue;
            }

            if (treatEmptyAsOverride)
            {
                return null;
            }
        }

        var sectionValue = section[sectionKey];
        if (sectionValue is not null)
        {
            if (!string.IsNullOrWhiteSpace(sectionValue))
            {
                return sectionValue;
            }

            if (treatEmptyAsOverride)
            {
                return null;
            }
        }

        return fallback;
    }

    private static int ReadInt(
        IConfiguration configuration,
        IConfigurationSection section,
        string sectionKey,
        string environmentKey,
        int fallback)
    {
        var rawValue = ReadValue(configuration, section, sectionKey, environmentKey);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (!int.TryParse(rawValue, out var parsed))
        {
            throw new BootstrapConfigurationException(
                $"{environmentKey} must be an integer. Value: '{rawValue}'.");
        }

        return parsed;
    }

    private static bool ReadBool(
        IConfiguration configuration,
        IConfigurationSection section,
        string sectionKey,
        string environmentKey,
        bool fallback)
    {
        var rawValue = ReadValue(configuration, section, sectionKey, environmentKey);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (bool.TryParse(rawValue, out var parsed))
        {
            return parsed;
        }

        if (string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(rawValue, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new BootstrapConfigurationException(
            $"{environmentKey} must be true/false, yes/no, or 1/0. Value: '{rawValue}'.");
    }
}
