using Honua.Sdk.Admin.Models;

namespace AdminBootstrapConsole;

public sealed class BootstrapConfigurationException : Exception
{
    public BootstrapConfigurationException(string message)
        : base(message)
    {
    }
}

public sealed class BootstrapCompatibilityException : Exception
{
    public BootstrapCompatibilityException(ServerCompatibilityResult compatibility)
        : base(BuildMessage(compatibility))
    {
        Compatibility = compatibility;
    }

    public ServerCompatibilityResult Compatibility { get; }

    private static string BuildMessage(ServerCompatibilityResult compatibility)
    {
        var serverVersion = string.IsNullOrWhiteSpace(compatibility.ServerVersion)
            ? "unknown"
            : compatibility.ServerVersion;
        var releaseChannel = string.IsNullOrWhiteSpace(compatibility.ReleaseChannel)
            ? "unknown"
            : compatibility.ReleaseChannel;
        var reason = string.IsNullOrWhiteSpace(compatibility.UnsupportedReason)
            ? "Compatibility metadata did not satisfy the SDK baseline."
            : compatibility.UnsupportedReason;

        return
            $"Server {serverVersion} ({releaseChannel}) is not supported. " +
            $"Minimum supported version: {compatibility.MinimumSupportedServerVersion} " +
            $"({compatibility.MinimumSupportedReleaseChannel}+). {reason}";
    }
}
