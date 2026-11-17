// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;

namespace Honua.Sdk.Cli;

/// <summary>The standard output and error writers a command reports to.</summary>
internal sealed record CliStreams(TextWriter Output, TextWriter Error);

/// <summary>Parsed <c>honua forms</c> arguments: the verb, its one positional target and named options.</summary>
internal sealed record FormsInvocation(string Verb, string Target, IReadOnlyDictionary<string, string> Options)
{
    internal bool Has(string name) => Options.ContainsKey(name);

    internal string? Get(string name)
        => Options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    internal string Require(string name, string message) => Get(name) ?? throw new CliArgumentException(message);
}

/// <summary><c>honua forms import|export</c>: XLSForm workbooks against a server or converted locally.</summary>
internal static class FormsCommand
{
    internal const string Name = "forms";

    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal) { "help", "json", "local" };

    private static readonly HashSet<string> ValueOptions = new(StringComparer.Ordinal)
    {
        "base-url", "layer", "output", "service", "timeout-ms"
    };

    internal static async Task<int> RunAsync(string[] args, CliStreams streams, HttpClient client, CancellationToken cancellationToken)
    {
        (TextWriter output, TextWriter error) = streams;
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            await output.WriteLineAsync(Usage).ConfigureAwait(false);
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            FormsInvocation invocation = Parse(args);
            FormsResult result = await ExecuteAsync(invocation, output, client, cancellationToken).ConfigureAwait(false);
            Task report = invocation.Has("json")
                ? FormsReport.WriteJsonAsync(result, output)
                : FormsReport.WriteTextAsync(result, streams);
            await report.ConfigureAwait(false);
            return result.ExitCode;
        }
        catch (CliArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or OperationCanceledException
            or JsonException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException)
        {
            await error.WriteLineAsync("honua forms failed: " + Describe(exception)).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<FormsResult> ExecuteAsync(
        FormsInvocation invocation,
        TextWriter output,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        if (invocation.Has("help"))
        {
            await output.WriteLineAsync(Usage).ConfigureAwait(false);
            return FormsResult.Help;
        }

        if (invocation.Has("local"))
        {
            RejectServerOptions(invocation);
            return invocation.Verb == "import"
                ? FormsLocalConverter.Import(invocation.Target, invocation.Get("output"))
                : await FormsLocalConverter.ExportAsync(invocation.Target, invocation.Get("output"), cancellationToken).ConfigureAwait(false);
        }

        FormsServer server = FormsServer.Create(invocation, client);
        return invocation.Verb == "import"
            ? await server.ImportAsync(invocation.Target, ReadImportTarget(invocation), cancellationToken).ConfigureAwait(false)
            : await server.ExportAsync(invocation.Target, invocation.Get("output"), cancellationToken).ConfigureAwait(false);
    }

    internal static FormsInvocation Parse(string[] args)
    {
        string verb = args[0];
        if (verb is not ("import" or "export"))
            throw new CliArgumentException("Unknown forms command. Use 'honua forms import' or 'honua forms export'.");

        Dictionary<string, string> options = new(StringComparer.Ordinal);
        List<string> positional = [];
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument is "-o")
                argument = "--output";
            if (argument.StartsWith("--", StringComparison.Ordinal))
                index = ReadOption(args, index, argument[2..], options);
            else
                positional.Add(argument);
        }

        if (options.ContainsKey("help"))
            return new FormsInvocation(verb, string.Empty, options);
        if (positional.Count != 1)
        {
            string expected = verb == "import" ? "<workbook.xlsx>" : "<formId>";
            throw new CliArgumentException($"honua forms {verb} requires exactly one {expected} argument.");
        }
        if (verb == "export" && !options.ContainsKey("local") && positional[0].Trim() is "" or "." or "..")
            throw new CliArgumentException("honua forms export requires a form id.");
        return new FormsInvocation(verb, positional[0], options);
    }

    private static int ReadOption(string[] args, int index, string token, Dictionary<string, string> options)
    {
        int equals = token.IndexOf('=', StringComparison.Ordinal);
        string name = equals >= 0 ? token[..equals] : token;
        string value;
        if (Flags.Contains(name))
        {
            if (equals >= 0)
                throw new CliArgumentException($"--{name} does not accept a value.");
            value = "true";
        }
        else if (!ValueOptions.Contains(name))
        {
            throw new CliArgumentException($"honua forms received an unknown option --{name}.");
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

        if (!options.TryAdd(name, value))
            throw new CliArgumentException($"--{name} may be specified only once.");
        return index;
    }

    private static FormsImportTarget ReadImportTarget(FormsInvocation invocation)
    {
        string service = invocation.Require("service", "honua forms import requires --service <id> (or --local).");
        string layerValue = invocation.Require("layer", "honua forms import requires --layer <id> (or --local).");
        if (!int.TryParse(layerValue, NumberStyles.None, CultureInfo.InvariantCulture, out int layer))
            throw new CliArgumentException("--layer must be a non-negative integer layer id.");
        return new FormsImportTarget(service, layer);
    }

    private static void RejectServerOptions(FormsInvocation invocation)
    {
        string[] serverOnly = ["base-url", "service", "layer", "timeout-ms"];
        string? present = serverOnly.FirstOrDefault(invocation.Has);
        if (present is not null)
            throw new CliArgumentException($"--{present} cannot be combined with --local.");
    }

    private static string Describe(Exception exception) => exception switch
    {
        OperationCanceledException => "the request timed out or was cancelled.",
        JsonException => "the input or response is not valid JSON.",
        _ => exception.Message
    };

    internal const string Usage = """
        Usage:
          honua forms import <workbook.xlsx> --service <id> --layer <id> [--base-url <url>] [--json]
          honua forms export <formId> [-o <file.xlsx>] [--base-url <url>] [--json]

          honua forms import <workbook.xlsx> --local [-o <form.json>]
          honua forms export <form.json> --local [-o <file.xlsx>] [--json]

        The server URL comes from --base-url or HONUA_BASE_URL and the admin API key from
        HONUA_API_KEY. Import creates a draft form; it is not published. --local converts with
        the Honua.Sdk.Field XLSForm converter without a server. Rejected workbooks print each
        blocking cell (sheet, row, column, message) and exit 1; usage errors exit 2.
        """;
}
