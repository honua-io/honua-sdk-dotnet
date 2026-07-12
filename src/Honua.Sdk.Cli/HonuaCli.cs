using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Honua.Sdk.Cli;

internal sealed class CliArgumentException : Exception
{
    public CliArgumentException()
    {
    }

    public CliArgumentException(string message)
        : base(message)
    {
    }

    public CliArgumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal static class HonuaCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        using SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None
        };
        using HttpClient client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return await RunAsync(args, Console.Out, Console.Error, client, CancellationToken.None).ConfigureAwait(false);
    }

    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                await output.WriteLineAsync(Usage()).ConfigureAwait(false);
                return args.Length == 0 ? 2 : 0;
            }
            if (!args[0].Equals("doctor", StringComparison.Ordinal))
                throw new CliArgumentException("Unknown command. Only 'honua doctor' is supported.");

            Dictionary<string, string> options = ParseOptions(args[1..]);
            if (options.ContainsKey("help"))
            {
                await output.WriteLineAsync(Usage()).ConfigureAwait(false);
                return 0;
            }

            string destination = Require(options, "output", "honua doctor requires --output <bundle.json>.");
            int timeoutMilliseconds = ReadInteger(options, "timeout-ms", 10_000, 1, 30_000);
            int previewBytes = ReadInteger(
                options,
                "preview-bytes",
                DiagnosticSanitizer.DefaultPreviewBytes,
                1,
                DiagnosticSanitizer.MaxPreviewBytes);
            TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
            string? replayPath = Get(options, "replay");

            DiagnosticBundle bundle;
            string outcome;
            bool capabilityProbe;
            if (replayPath is not null)
            {
                RejectPresent(options, "exchange", "classification", "redaction-acknowledged", "share-with-support", "granted-by", "bundle-id");
                string baseValue = Get(options, "base-url")
                    ?? Environment.GetEnvironmentVariable("HONUA_BASE_URL")
                    ?? throw new CliArgumentException("Replay requires --base-url or HONUA_BASE_URL.");
                Uri baseUri = DiagnosticReplay.ValidateBaseUri(baseValue);
                DiagnosticBundle source = DiagnosticReplay.ReadAndValidateBundle(replayPath);
                bundle = await DiagnosticReplay.ReplayAsync(
                    source,
                    baseUri,
                    client,
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                outcome = "replayed";
                capabilityProbe = false;
            }
            else
            {
                string classification = Require(
                    options,
                    "classification",
                    "honua doctor requires --classification <value>.");
                DiagnosticConsent consent = new(
                    ReadExplicitBoolean(options, "redaction-acknowledged"),
                    ReadExplicitBoolean(options, "share-with-support"),
                    Get(options, "granted-by"));
                List<DiagnosticExchangeInput> exchanges = [];
                string? baseValue = Get(options, "base-url") ?? Environment.GetEnvironmentVariable("HONUA_BASE_URL");
                capabilityProbe = baseValue is not null;
                if (baseValue is not null)
                {
                    Uri baseUri = DiagnosticReplay.ValidateBaseUri(baseValue);
                    exchanges.Add(await DiagnosticDoctor.ProbeCapabilitiesAsync(
                        baseUri,
                        client,
                        timeout,
                        cancellationToken).ConfigureAwait(false));
                }

                string? exchangePath = Get(options, "exchange");
                if (exchangePath is not null)
                    exchanges.Add(DiagnosticDoctor.ReadCapturedExchange(exchangePath));
                if (exchanges.Count == 0)
                    throw new CliArgumentException("honua doctor requires --base-url/HONUA_BASE_URL or --exchange <captured.json>.");

                bundle = DiagnosticSanitizer.CreateBundle(
                    classification,
                    consent,
                    exchanges,
                    Get(options, "bundle-id"),
                    previewBytes);
                outcome = "emitted";
            }

            WriteValidatedBundle(destination, bundle);
            await WriteSummaryAsync(output, options.ContainsKey("json"), outcome, bundle.Envelopes.Count, capabilityProbe)
                .ConfigureAwait(false);
            return 0;
        }
        catch (CliArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
        catch (Exception exception) when (exception is DiagnosticSafetyException
            or HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or OperationCanceledException
            or JsonException)
        {
            await error.WriteLineAsync("honua doctor failed safely; no diagnostic artifact was written.").ConfigureAwait(false);
            return 1;
        }
#pragma warning disable CA1031 // CLI safety boundary: never leak an unexpected exception or raw input to process streams.
        catch (Exception)
        {
            await error.WriteLineAsync("honua doctor failed safely; no diagnostic artifact was written.").ConfigureAwait(false);
            return 1;
        }
#pragma warning restore CA1031
    }

    internal static void WriteValidatedBundle(string path, DiagnosticBundle bundle)
    {
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(bundle, DiagnosticJson.Options);
        using (JsonDocument document = JsonDocument.Parse(serialized))
            DiagnosticSchema.Instance.AssertValid(document.RootElement);

        string destination = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(destination);
        if (directory is null)
            throw new IOException("Diagnostic output directory could not be resolved.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(serialized);
                stream.WriteByte((byte)'\n');
                stream.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, destination, overwrite: true);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
                throw new CliArgumentException("honua doctor does not accept positional arguments.");
            string token = argument[2..];
            int equals = token.IndexOf('=', StringComparison.Ordinal);
            string name = equals >= 0 ? token[..equals] : token;
            string value;
            if (name is "json" or "help")
            {
                value = "true";
                if (equals >= 0)
                    throw new CliArgumentException($"--{name} does not accept a value.");
            }
            else if (equals >= 0)
            {
                value = token[(equals + 1)..];
            }
            else
            {
                if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                    throw new CliArgumentException($"--{name} requires a value.");
                value = args[index];
            }

            if (!KnownOptions.Contains(name))
                throw new CliArgumentException("honua doctor received an unknown option.");
            if (!options.TryAdd(name, value))
                throw new CliArgumentException($"--{name} may be specified only once.");
        }
        return options;
    }

    private static readonly HashSet<string> KnownOptions = new(StringComparer.Ordinal)
    {
        "base-url", "bundle-id", "classification", "exchange", "granted-by", "help", "json", "output",
        "preview-bytes", "redaction-acknowledged", "replay", "share-with-support", "timeout-ms"
    };

    private static string Require(Dictionary<string, string> options, string name, string message)
        => Get(options, name) ?? throw new CliArgumentException(message);

    private static string? Get(Dictionary<string, string> options, string name)
        => options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static bool ReadExplicitBoolean(Dictionary<string, string> options, string name)
    {
        string value = Require(options, name, $"--{name} must be explicitly true or false.");
        return value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new CliArgumentException($"--{name} must be explicitly true or false.")
        };
    }

    private static int ReadInteger(
        Dictionary<string, string> options,
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        string? value = Get(options, name);
        if (value is null)
            return defaultValue;
        if (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            || parsed < minimum
            || parsed > maximum)
        {
            throw new CliArgumentException($"--{name} is outside the supported range.");
        }
        return parsed;
    }

    private static void RejectPresent(Dictionary<string, string> options, params string[] names)
    {
        if (names.Any(options.ContainsKey))
            throw new CliArgumentException("Replay options cannot be combined with capture options.");
    }

    private static async Task WriteSummaryAsync(
        TextWriter output,
        bool json,
        string outcome,
        int envelopeCount,
        bool capabilityProbe)
    {
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        if (!json)
        {
            await output.WriteLineAsync($"doctorOutcome={outcome}").ConfigureAwait(false);
            await output.WriteLineAsync("output=written").ConfigureAwait(false);
            await output.WriteLineAsync($"schema={DiagnosticSchema.CanonicalUrl}").ConfigureAwait(false);
            await output.WriteLineAsync($"schemaSha256={DiagnosticSchema.Sha256}").ConfigureAwait(false);
            return;
        }

        var summary = new
        {
            format = "honua.doctor-result.v1",
            outcome,
            outputWritten = true,
            envelopeCount,
            capabilityProbe = capabilityProbe ? "attempted" : "not-configured",
            runtime = new { framework = RuntimeInformation.FrameworkDescription, target = "net10.0", os = RuntimeInformation.OSDescription },
            sdk = new { package = "Honua.Sdk.Cli", version },
            schema = DiagnosticSchema.CanonicalUrl,
            schemaSha256 = DiagnosticSchema.Sha256,
            uploaded = false
        };
        await output.WriteLineAsync(JsonSerializer.Serialize(summary, DiagnosticJson.Options)).ConfigureAwait(false);
    }

    private static string Usage() => """
        Usage:
          honua doctor --exchange <capture.json> --classification <value>
            --redaction-acknowledged=<true|false> --share-with-support=<true|false>
            --output <bundle.json> [--base-url <url>] [--json]

          honua doctor --replay <bundle.json> --base-url <url>
            --output <replay.json> [--json]

        The command validates against the pinned support v1 schema and never uploads automatically.
        """;
}
