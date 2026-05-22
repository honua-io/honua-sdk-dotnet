using AdminBootstrapConsole;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

BootstrapOptions options;

try
{
    options = BootstrapOptions.Load(builder.Configuration);
}
catch (BootstrapConfigurationException ex)
{
    PrintError("Configuration", ex.Message);
    return (int)BootstrapExitCode.ConfigurationFailure;
}

builder.Services.AddSingleton(options);
builder.Services.AddHonuaAdmin(config =>
{
    config.BaseAddress = options.ServerUri;
    config.ApiKey = options.ApiKey;
    config.BearerToken = options.BearerToken;
});
builder.Services.AddHonuaGrpc(config =>
{
    config.BaseAddress = options.ServerUri;
    config.ApiKey = options.ApiKey;
    config.BearerToken = options.BearerToken;
});
builder.Services.AddSingleton<BootstrapWorkflow>();

using var host = builder.Build();
using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var workflow = host.Services.GetRequiredService<BootstrapWorkflow>();

try
{
    await workflow.RunAsync(Console.Out, cancellation.Token);
    return (int)BootstrapExitCode.Success;
}
catch (BootstrapCompatibilityException ex)
{
    PrintError("Compatibility", ex.Message);
    return (int)BootstrapExitCode.CompatibilityFailure;
}
catch (HonuaAdminApiException ex)
{
    PrintError("Admin API", $"{(int)ex.StatusCode} {ex.StatusCode}: {ex.Message}");
    return (int)BootstrapExitCode.AdminFailure;
}
catch (HonuaAdminOperationException ex)
{
    var operation = string.IsNullOrWhiteSpace(ex.Operation) ? "Admin" : ex.Operation;
    PrintError(operation, ex.Message);
    return (int)BootstrapExitCode.AdminFailure;
}
catch (HonuaGrpcException ex)
{
    PrintError("gRPC", $"{ex.StatusCode}: {ex.Message}");
    return (int)BootstrapExitCode.VerificationFailure;
}
catch (OperationCanceledException)
{
    PrintError("Cancelled", "Bootstrap run cancelled.");
    return (int)BootstrapExitCode.Cancelled;
}
catch (Exception ex)
{
    PrintError("Unexpected", ex.Message);
    return (int)BootstrapExitCode.UnexpectedFailure;
}

static void PrintError(string scope, string message)
{
    Console.Error.WriteLine($"[{scope}] {message}");
}

enum BootstrapExitCode
{
    Success = 0,
    UnexpectedFailure = 1,
    ConfigurationFailure = 2,
    CompatibilityFailure = 3,
    AdminFailure = 4,
    VerificationFailure = 5,
    Cancelled = 130
}
