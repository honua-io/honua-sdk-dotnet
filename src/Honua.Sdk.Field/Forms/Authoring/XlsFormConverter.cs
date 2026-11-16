// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Forms.Authoring;

/// <summary>Converts the supported XLSForm subset without discarding unsupported behavior.</summary>
public static class XlsFormConverter
{
    /// <summary>Imports worksheet rows, returning cell diagnostics instead of a partial form.</summary>
    public static XlsFormResult<FormDefinition> Import(XlsFormWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return new XlsFormImport(workbook).Convert();
    }

    /// <summary>Exports a portable form, returning diagnostics for behavior XLSForm cannot represent.</summary>
    public static XlsFormResult<XlsFormWorkbook> Export(FormDefinition form)
    {
        ArgumentNullException.ThrowIfNull(form);
        return new XlsFormExport().Convert(form);
    }
}
