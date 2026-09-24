namespace Vispl.HRManagement.BM.ITF.Shared;

/// <summary>How a column's cells are written and formatted in the workbook.</summary>
public enum ExcelFormat
{
    Text,
    Integer,
    Decimal,

    /// <summary>Rupees with Indian lakh grouping, e.g. ₹12,34,567.00.</summary>
    Money,

    Date,
    DateTime,

    /// <summary>A fraction (0.25) shown as 25%.</summary>
    Percent
}

public sealed record ExcelColumn(string Header, ExcelFormat Format = ExcelFormat.Text, double? Width = null);

/// <summary>
/// One worksheet: a title block, a header row, the data rows, and an optional bold totals row.
/// Cell values are plain CLR values (string, int, decimal, DateOnly, DateTimeOffset …); the
/// column's <see cref="ExcelFormat"/> decides how they are written.
/// </summary>
public sealed record ExcelSheet(
    string Name,
    string Title,
    string? Subtitle,
    IReadOnlyList<ExcelColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    IReadOnlyList<object?>? Totals = null);

/// <summary>
/// Builds real .xlsx workbooks (Syncfusion XlsIO) for every download on the HR portal and the
/// employee app. Output is bytes in memory; nothing is written to disk.
/// </summary>
public interface IExcelExporter
{
    byte[] Build(params ExcelSheet[] sheets);
}

public static class ExcelContentTypes
{
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
