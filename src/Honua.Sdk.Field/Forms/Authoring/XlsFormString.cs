// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace Honua.Sdk.Field.Forms.Authoring;

internal static class XlsFormString
{
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    public static string Decode(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (IsEscape(text, i))
            {
                result.Append((char)ushort.Parse(text.AsSpan(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 6;
            }
            else
            {
                result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    public static string Encode(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var value = text[i];
            if (IsEscape(text, i) || (value < ' ' && value is not ('\t' or '\n'))
                || char.IsSurrogate(value) || value is '\uFFFE' or '\uFFFF')
            {
                result.Append("_x").Append(((ushort)value).ToString("X4", CultureInfo.InvariantCulture)).Append('_');
            }
            else
            {
                result.Append(value);
            }
        }

        return result.ToString();
    }

    private static bool IsEscape(string text, int offset)
        => offset + 6 < text.Length && text[offset] == '_' && text[offset + 1] == 'x' && text[offset + 6] == '_'
            && text.AsSpan(offset + 2, 4).IndexOfAnyExcept(HexDigits) < 0;
}
