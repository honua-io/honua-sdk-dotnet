// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Collections.ObjectModel;

namespace Honua.Sdk.Field.Records;

/// <summary>
/// One captured row of a repeatable <see cref="Forms.FormSection"/> (a Survey123
/// "repeat" / Fulcrum "repeatable section" instance). Each instance carries its
/// own field values and media so repeat data is part of the portable record
/// contract and round-trips through serialization, sync, and export.
/// </summary>
public sealed class FieldRepeatInstance
{
    /// <summary>Captured field values for this row, keyed by form field identifier.</summary>
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Portable media metadata captured against this row.</summary>
    public Collection<FieldMediaAttachment> Media { get; init; } = [];
}
