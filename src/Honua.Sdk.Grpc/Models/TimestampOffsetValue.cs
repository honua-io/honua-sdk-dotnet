// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;

namespace Honua.Sdk.Grpc.Models;

/// <summary>Attribute value carrying an RFC 3339 timestamp with its original UTC offset preserved.</summary>
public readonly record struct TimestampOffsetValue
{
    private const string Rfc3339Format = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

    /// <summary>Initializes a new timestamp-offset attribute value.</summary>
    public TimestampOffsetValue(DateTimeOffset value)
    {
        Value = value;
    }

    /// <summary>Underlying timestamp value with preserved UTC offset.</summary>
    public DateTimeOffset Value { get; }

    /// <summary>Formats the timestamp as an RFC 3339 string retaining its UTC offset.</summary>
    public string ToRfc3339String()
        => Value.ToString(Rfc3339Format, CultureInfo.InvariantCulture);

    /// <summary>Parses an RFC 3339 timestamp string retaining its UTC offset.</summary>
    public static TimestampOffsetValue Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parsed = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new TimestampOffsetValue(parsed);
    }
}
