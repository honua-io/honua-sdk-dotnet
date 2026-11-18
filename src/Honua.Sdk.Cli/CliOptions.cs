// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;

namespace Honua.Sdk.Cli;

internal sealed record OptionGrammar(
    IReadOnlySet<string> Flags,
    Func<string, bool> AcceptsValue,
    Func<string, string> UnknownOption);

internal readonly record struct OptionCursor(string[] Args, int Index, string Token);

internal readonly record struct OptionRead(OptionCursor Cursor, Dictionary<string, string> Options, OptionGrammar Grammar);

internal sealed class IntegerBounds
{
    internal required string Name { get; init; }
    internal required int DefaultValue { get; init; }
    internal required int Minimum { get; init; }
    internal required int Maximum { get; init; }
    internal string? Message { get; init; }
}

internal static class CliOptions
{
    internal static string? Get(IReadOnlyDictionary<string, string> options, string name)
        => options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    internal static string Require(IReadOnlyDictionary<string, string> options, string name, string message)
        => Get(options, name) ?? throw new CliArgumentException(message);

    internal static string RequireBaseUrl(string? optionValue, string missingMessage)
        => optionValue ?? Environment.GetEnvironmentVariable("HONUA_BASE_URL") ?? throw new CliArgumentException(missingMessage);

    internal static int Read(OptionRead read)
    {
        int index = read.Cursor.Index;
        int equals = read.Cursor.Token.IndexOf('=', StringComparison.Ordinal);
        string name = equals >= 0 ? read.Cursor.Token[..equals] : read.Cursor.Token;
        string value;
        if (read.Grammar.Flags.Contains(name))
        {
            if (equals >= 0)
                throw new CliArgumentException($"--{name} does not accept a value.");
            value = "true";
        }
        else if (!read.Grammar.AcceptsValue(name))
        {
            throw new CliArgumentException(read.Grammar.UnknownOption(name));
        }
        else if (equals >= 0)
        {
            value = read.Cursor.Token[(equals + 1)..];
        }
        else
        {
            if (++index >= read.Cursor.Args.Length || read.Cursor.Args[index].StartsWith("--", StringComparison.Ordinal))
                throw new CliArgumentException($"--{name} requires a value.");
            value = read.Cursor.Args[index];
        }

        if (!read.Options.TryAdd(name, value))
            throw new CliArgumentException($"--{name} may be specified only once.");
        return index;
    }

    internal static int ReadInteger(string? value, IntegerBounds bounds)
    {
        if (value is null)
            return bounds.DefaultValue;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            || parsed < bounds.Minimum
            || parsed > bounds.Maximum)
        {
            throw new CliArgumentException(bounds.Message ?? $"--{bounds.Name} is outside the supported range.");
        }

        return parsed;
    }
}
