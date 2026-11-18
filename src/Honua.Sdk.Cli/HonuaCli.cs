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
                await output.WriteLineAsync(Usage(), cancellationToken).ConfigureAwait(false);
                return args.Length == 0 ? 2 : 0;
            }
            if (args[0].Equals(FormsCommand.Name, StringComparison.Ordinal))
                return await FormsCommand.RunAsync(
                    args[1..],
                    new FormsCommandContext(new CliStreams(output, error), client),
                    cancellationToken).ConfigureAwait(false);
            if (!args[0].Equals("doctor", StringComparison.Ordinal))
                throw new CliArgumentException("Unknown command. Use 'honua doctor' or 'honua forms'.");

            Dictionary<string, string> options = ParseOptions(args[1..]);
            if (options.ContainsKey("help"))
            {
                await output.WriteLineAsync(Usage(), cancellationToken).ConfigureAwait(false);
                return 0;
            }

            string destination = CliOptions.Require(options, "output", "honua doctor requires --output <bundle.json>.");
            int timeoutMilliseconds = CliOptions.ReadInteger(
                CliOptions.Get(options, "timeout-ms"),
                new IntegerBounds { Name = "timeout-ms", DefaultValue = 10_000, Minimum = 1, Maximum = 30_000 });
            int previewBytes = CliOptions.ReadInteger(
                CliOptions.Get(options, "preview-bytes"),
                new IntegerBounds
                {
                    Name = "preview-bytes",
                    DefaultValue = DiagnosticSanitizer.DefaultPreviewBytes,
                    Minimum = 1,
                    Maximum = DiagnosticSanitizer.MaxPreviewBytes
                });
            TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
            string? replayPath = CliOptions.Get(options, "replay");

            DiagnosticBundle bundle;
            string outcome;
            bool capabilityProbe;
            if (replayPath is not null)
            {
                RejectPresent(options, "exchange", "classification", "redaction-acknowledged", "share-with-support", "granted-by", "bundle-id");
                string baseValue = CliOptions.RequireBaseUrl(
                    CliOptions.Get(options, "base-url"),
                    "Replay requires --base-url or HONUA_BASE_URL.");
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
                string classification = CliOptions.Require(
                    options,
                    "classification",
                    "honua doctor requires --classification <value>.");
                DiagnosticConsent consent = new(
                    ReadExplicitBoolean(options, "redaction-acknowledged"),
                    ReadExplicitBoolean(options, "share-with-support"),
                    CliOptions.Get(options, "granted-by"));
                List<DiagnosticExchangeInput> exchanges = [];
                string? baseValue = CliOptions.Get(options, "base-url") ?? Environment.GetEnvironmentVariable("HONUA_BASE_URL");
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

                string? exchangePath = CliOptions.Get(options, "exchange");
                if (exchangePath is not null)
                    exchanges.Add(DiagnosticDoctor.ReadCapturedExchange(exchangePath));
                if (exchanges.Count == 0)
                    throw new CliArgumentException("honua doctor requires --base-url/HONUA_BASE_URL or --exchange <captured.json>.");

                bundle = DiagnosticSanitizer.CreateBundle(
                    classification,
                    consent,
                    exchanges,
                    CliOptions.Get(options, "bundle-id"),
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
            await error.WriteLineAsync(exception.Message, CancellationToken.None).ConfigureAwait(false);
            return 2;
        }
        catch (Exception exception) when (exception is DiagnosticSafetyException
            or HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or OperationCanceledException
            or JsonException)
        {
            await error.WriteLineAsync("honua doctor failed safely; no diagnostic artifact was written.", CancellationToken.None).ConfigureAwait(false);
            return 1;
        }
#pragma warning disable CA1031 // CLI safety boundary: never leak an unexpected exception or raw input to process streams.
        catch (Exception)
        {
            await error.WriteLineAsync("honua doctor failed safely; no diagnostic artifact was written.", CancellationToken.None).ConfigureAwait(false);
            return 1;
        }
#pragma warning restore CA1031
    }

    internal static void WriteValidatedBundle(string path, DiagnosticBundle bundle)
    {
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(bundle, DiagnosticJson.Options);
        using (JsonDocument document = JsonDocument.Parse(serialized))
            DiagnosticSchema.Instance.AssertValid(document.RootElement);

        byte[] payload = new byte[serialized.Length + 1];
        serialized.CopyTo(payload, 0);
        payload[^1] = (byte)'\n';
        CliFiles.WriteAtomically(path, payload, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
                throw new CliArgumentException("honua doctor does not accept positional arguments.");
            index = CliOptions.Read(new OptionRead(new OptionCursor(args, index, argument[2..]), options, Grammar));
        }
        return options;
    }

    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal) { "help", "json" };

    private static readonly HashSet<string> KnownOptions = new(StringComparer.Ordinal)
    {
        "base-url", "bundle-id", "classification", "exchange", "granted-by", "help", "json", "output",
        "preview-bytes", "redaction-acknowledged", "replay", "share-with-support", "timeout-ms"
    };

    private static readonly OptionGrammar Grammar = new(
        Flags,
        static name => KnownOptions.Contains(name),
        static _ => "honua doctor received an unknown option.");

    private static bool ReadExplicitBoolean(Dictionary<string, string> options, string name)
    {
        string value = CliOptions.Require(options, name, $"--{name} must be explicitly true or false.");
        return value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new CliArgumentException($"--{name} must be explicitly true or false.")
        };
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
            runtime = new { framework = RuntimeInformation.FrameworkDescription, target = "net11.0", os = RuntimeInformation.OSDescription },
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

          honua forms import|export ...   XLSForm workbooks; run 'honua forms --help'.
        """;
}
