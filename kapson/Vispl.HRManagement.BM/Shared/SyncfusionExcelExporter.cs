using System.Globalization;
using Syncfusion.XlsIO;
using Vispl.HRManagement.BM.ITF.Shared;

namespace Vispl.HRManagement.BM.Shared;

/// <summary>
/// <see cref="IExcelExporter"/> on Syncfusion XlsIO.
///
/// Every workbook has the same shape so a download from any screen looks like it came from one
/// product: a bold title and subtitle, a brand-blue header row that stays frozen while scrolling,
/// an autofilter over the data, Indian-grouped rupee columns, and a bold totals row.
/// </summary>
public sealed class SyncfusionExcelExporter : IExcelExporter
{
    // Lakh grouping for large values, ordinary thousands below a lakh.
    private const string MoneyFormat =
        "[>=10000000]\"₹\"##\\,##\\,##\\,##0.00;[>=100000]\"₹\"##\\,##\\,##0.00;\"₹\"##,##0.00";

    private static readonly Syncfusion.Drawing.Color HeaderFill = Syncfusion.Drawing.Color.FromArgb(51, 117, 234);
    private static readonly Syncfusion.Drawing.Color TotalsFill = Syncfusion.Drawing.Color.FromArgb(238, 241, 246);
    private static readonly Syncfusion.Drawing.Color TitleText = Syncfusion.Drawing.Color.FromArgb(17, 24, 39);
    private static readonly Syncfusion.Drawing.Color SubtitleText = Syncfusion.Drawing.Color.FromArgb(100, 116, 139);

    public byte[] Build(params ExcelSheet[] sheets)
    {
        if (sheets.Length == 0)
        {
            throw new ArgumentException("A workbook needs at least one sheet.", nameof(sheets));
        }

        using var engine = new ExcelEngine();
        var application = engine.Excel;
        application.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = application.Workbooks.Create(sheets.Length);

        for (var index = 0; index < sheets.Length; index++)
        {
            Write(workbook.Worksheets[index], sheets[index]);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        workbook.Close();

        return stream.ToArray();
    }

    private static void Write(IWorksheet worksheet, ExcelSheet sheet)
    {
        var columns = Math.Max(sheet.Columns.Count, 1);
        worksheet.Name = SafeSheetName(sheet.Name);

        // Title block.
        worksheet.Range[1, 1].Text = sheet.Title;
        worksheet.Range[1, 1, 1, columns].Merge();
        worksheet.Range[1, 1].CellStyle.Font.Bold = true;
        worksheet.Range[1, 1].CellStyle.Font.Size = 14;
        worksheet.Range[1, 1].CellStyle.Font.RGBColor = TitleText;

        var headerRow = 3;

        if (!string.IsNullOrWhiteSpace(sheet.Subtitle))
        {
            worksheet.Range[2, 1].Text = sheet.Subtitle;
            worksheet.Range[2, 1, 2, columns].Merge();
            worksheet.Range[2, 1].CellStyle.Font.Size = 10;
            worksheet.Range[2, 1].CellStyle.Font.RGBColor = SubtitleText;
            headerRow = 4;
        }

        // Header row.
        for (var column = 0; column < sheet.Columns.Count; column++)
        {
            var cell = worksheet.Range[headerRow, column + 1];
            cell.Text = sheet.Columns[column].Header;
            cell.CellStyle.Font.Bold = true;
            cell.CellStyle.Font.RGBColor = Syncfusion.Drawing.Color.White;
            cell.CellStyle.Color = HeaderFill;
            cell.CellStyle.VerticalAlignment = ExcelVAlign.VAlignCenter;
        }

        worksheet.SetRowHeight(headerRow, 20);

        // Data rows.
        var row = headerRow + 1;
        foreach (var values in sheet.Rows)
        {
            WriteRow(worksheet, sheet.Columns, row, values);
            row++;
        }

        var lastDataRow = row - 1;

        if (sheet.Totals is { } totals)
        {
            WriteRow(worksheet, sheet.Columns, row, totals);
            var totalsRange = worksheet.Range[row, 1, row, columns];
            totalsRange.CellStyle.Font.Bold = true;
            totalsRange.CellStyle.Color = TotalsFill;
        }

        if (sheet.Rows.Count > 0)
        {
            worksheet.AutoFilters.FilterRange = worksheet.Range[headerRow, 1, lastDataRow, columns];
        }

        worksheet.Range[headerRow + 1, 1].FreezePanes();

        for (var column = 0; column < sheet.Columns.Count; column++)
        {
            if (sheet.Columns[column].Width is { } width)
            {
                worksheet.SetColumnWidth(column + 1, width);
            }
            else
            {
                worksheet.AutofitColumn(column + 1);

                // Autofit reads the merged title too; keep columns readable but not absurdly wide.
                var fitted = worksheet.GetColumnWidth(column + 1);
                worksheet.SetColumnWidth(column + 1, Math.Clamp(fitted, 8, 45));
            }
        }
    }

    private static void WriteRow(IWorksheet worksheet, IReadOnlyList<ExcelColumn> columns, int row, IReadOnlyList<object?> values)
    {
        for (var column = 0; column < columns.Count && column < values.Count; column++)
        {
            var value = values[column];
            if (value is null)
            {
                continue;
            }

            var cell = worksheet.Range[row, column + 1];
            var format = columns[column].Format;

            switch (format)
            {
                case ExcelFormat.Money when TryNumber(value, out var money):
                    cell.Number = money;
                    cell.NumberFormat = MoneyFormat;
                    break;

                case ExcelFormat.Decimal when TryNumber(value, out var number):
                    cell.Number = number;
                    cell.NumberFormat = "#,##0.##";
                    break;

                case ExcelFormat.Integer when TryNumber(value, out var integer):
                    cell.Number = integer;
                    cell.NumberFormat = "0";
                    break;

                case ExcelFormat.Percent when TryNumber(value, out var fraction):
                    cell.Number = fraction;
                    cell.NumberFormat = "0.0%";
                    break;

                case ExcelFormat.Date when TryDate(value, out var date):
                    cell.DateTime = date;
                    cell.NumberFormat = "dd-mmm-yyyy";
                    break;

                case ExcelFormat.DateTime when TryDate(value, out var moment):
                    cell.DateTime = moment;
                    cell.NumberFormat = "dd-mmm-yyyy hh:mm";
                    break;

                default:
                    // Text is written as text on purpose: an account number or employee number
                    // must never be turned into a number and lose its leading zeros.
                    cell.Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    break;
            }
        }
    }

    private static bool TryNumber(object value, out double number)
    {
        switch (value)
        {
            case decimal d: number = (double)d; return true;
            case double d: number = d; return true;
            case float f: number = f; return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            default: number = 0; return false;
        }
    }

    private static bool TryDate(object value, out DateTime date)
    {
        switch (value)
        {
            case DateOnly d: date = d.ToDateTime(TimeOnly.MinValue); return true;
            case DateTime dt: date = dt; return true;
            case DateTimeOffset dto: date = dto.LocalDateTime; return true;
            default: date = default; return false;
        }
    }

    /// <summary>Excel sheet names: at most 31 characters, none of \ / ? * [ ] :.</summary>
    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(character => "\\/?*[]:".IndexOf(character) < 0).ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "Sheet1" : cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
