using System.IO.Compression;
using Honua.Sdk.Field.Forms.Authoring;

namespace Honua.Sdk.Field.Tests.Authoring;

public sealed class XlsFormWorkbookTests
{
    [Fact]
    public void WriteRead_PreservesUnicodeAndFormulaLikeTextWithoutExecutingIt()
    {
        var workbook = new XlsFormWorkbook
        {
            Survey = [new XlsFormRow { RowNumber = 2, Cells = new Dictionary<string, string>
            { ["type"] = "text", ["name"] = "name", ["label"] = "=SUM(1,2) & <名前>" } }],
            Settings = [new XlsFormRow { RowNumber = 2, Cells = new Dictionary<string, string>
            { ["form_id"] = "test", ["form_title"] = "A form" } }]
        };
        using var stream = new MemoryStream();
        XlsFormWorkbookIO.Write(stream, workbook);
        Assert.True(stream.CanWrite);
        stream.Position = 0;
        var result = XlsFormWorkbookIO.Read(stream);
        var actual = Assert.IsType<XlsFormWorkbook>(result.Value);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(workbook.Survey[0].Cells["label"], actual.Survey[0].Cells["label"]);
    }

    [Fact]
    public void Read_RejectsFormulaEvenWithCachedValue()
    {
        using var stream = WorkbookWithSurvey("<row r=\"2\"><c r=\"A2\"><f>CONCAT(1,2)</f><v>12</v></c></row>");
        var result = XlsFormWorkbookIO.Read(stream);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.RowNumber == 2 && d.Message.Contains("formula", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_RejectsDuplicateCellReferences()
    {
        using var stream = WorkbookWithSurvey("<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"A2\"><v>2</v></c></row>");
        Assert.Null(XlsFormWorkbookIO.Read(stream).Value);
    }

    [Fact]
    public void Read_RejectsMalformedZipWithDiagnostic()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var result = XlsFormWorkbookIO.Read(stream);
        Assert.Null(result.Value);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Theory]
    [InlineData("<row r=\"2\"><c r=\"A3\"><v>1</v></c></row>")]
    [InlineData("<row r=\"10001\"><c r=\"A10001\"><v>1</v></c></row>")]
    [InlineData("<row r=\"2\"><c r=\"A2\" t=\"s\"><v>99</v></c></row>")]
    public void Read_RejectsInvalidReferencesAndSharedStringIndexes(string rows)
    {
        using var stream = WorkbookWithSurvey(rows);
        Assert.Null(XlsFormWorkbookIO.Read(stream).Value);
    }

    [Fact]
    public void Read_RejectsOversizedCompressedInput()
    {
        using var stream = new MemoryStream(new byte[8 * 1024 * 1024 + 1]);
        var result = XlsFormWorkbookIO.Read(stream);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("8 MiB", StringComparison.Ordinal));
    }

    [Fact]
    public void Write_RejectsInvalidRowsBeforeWritingOutput()
    {
        var workbook = new XlsFormWorkbook
        {
            Survey = [new XlsFormRow { RowNumber = 1, Cells = new Dictionary<string, string> { ["type"] = "text" } }]
        };
        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => XlsFormWorkbookIO.Write(stream, workbook));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Write_RejectsSparseRowsThatWouldExceedReaderCellLimit()
    {
        var workbook = new XlsFormWorkbook
        {
            Survey = Enumerable.Range(2, 1563).Select(number => new XlsFormRow
            {
                RowNumber = number,
                Cells = new Dictionary<string, string> { [$"column_{number % 64}"] = "value" }
            }).ToArray()
        };
        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => XlsFormWorkbookIO.Write(stream, workbook));
        Assert.Equal(0, stream.Length);
    }

    private static MemoryStream WorkbookWithSurvey(string rows)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Add(zip, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"survey\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add(zip, "xl/worksheets/sheet1.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>type</t></is></c></row>" + rows + "</sheetData></worksheet>");
        }

        stream.Position = 0;
        return stream;
    }

    private static void Add(ZipArchive zip, string path, string value)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open());
        writer.Write(value);
    }
}
