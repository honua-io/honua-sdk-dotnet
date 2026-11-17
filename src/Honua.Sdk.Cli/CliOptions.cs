// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;

namespace Honua.Sdk.Cli;

internal static class CliOptions
{
    internal static int Read(
        string[] args,
        int index,
        string token,
        IReadOnlySet<string> flags,
        Func<string, bool> acceptsValue,
        Dictionary<string, string> options,
        Func<string, string> unknownOption)
    {
        int equals = token.IndexOf('=', StringComparison.Ordinal);
        string name = equals >= 0 ? token[..equals] : token;
        string value;
        if (flags.Contains(name))
        {
            if (equals >= 0)
                throw new CliArgumentException($"--{name} does not accept a value.");
            value = "true";
        }
        else if (!acceptsValue(name))
        {
            throw new CliArgumentException(unknownOption(name));
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

    internal static int ReadInteger(string? value, string name, int defaultValue, int minimum, int maximum, string? rangeMessage = null)
    {
        if (value is null)
            return defaultValue;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            || parsed < minimum
            || parsed > maximum)
        {
            throw new CliArgumentException(rangeMessage ?? $"--{name} is outside the supported range.");
        }

        return parsed;
    }
}
