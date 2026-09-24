using Syncfusion.XlsIO;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.BM.Shared;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// Every HR portal download goes through <see cref="SyncfusionExcelExporter"/>. These read the
/// produced bytes back with XlsIO, so they check the workbook a bank or an auditor opens.
/// </summary>
public sealed class ExcelExporterTests
{
    private static readonly ExcelSheet Advice = new(
        "Salary advice: Oct/2026",
        "Salary transfer advice — October 2026",
        "Debit account SBI - KAPSONS0034",
        [
            new("Employee Number"),
            new("Account Number"),
            new("Amount", ExcelFormat.Money),
            new("Transfer Date", ExcelFormat.Date)
        ],
        [
            [ "EMP-10040", "0012345678", 41_200.50m, new DateOnly(2026, 10, 31) ],
            [ "EMP-10045", "0098765432", 1_25_000m, new DateOnly(2026, 10, 31) ]
        ],
        [ "Total", null, 1_66_200.50m, null ]);

    [Fact]
    public void Produces_a_workbook_with_title_header_rows_and_totals()
    {
        using var workbook = Open(new SyncfusionExcelExporter().Build(Advice));
        var sheet = workbook.Worksheets[0];

        Assert.Equal("Salary transfer advice — October 2026", sheet.Range["A1"].Text);
        Assert.Equal("Debit account SBI - KAPSONS0034", sheet.Range["A2"].Text);
        Assert.Equal("Employee Number", sheet.Range["A4"].Text);
        Assert.Equal("EMP-10040", sheet.Range["A5"].Text);
        Assert.Equal("Total", sheet.Range["A7"].Text);
        Assert.Equal(166_200.50, sheet.Range["C7"].Number);
        Assert.True(sheet.Range["A7"].CellStyle.Font.Bold);
    }

    [Fact]
    public void Account_numbers_stay_text_and_keep_leading_zeros()
    {
        using var workbook = Open(new SyncfusionExcelExporter().Build(Advice));
        var cell = workbook.Worksheets[0].Range["B5"];

        Assert.False(cell.HasNumber);
        Assert.Equal("0012345678", cell.Text);
    }

    [Fact]
    public void Money_and_dates_are_real_numbers_with_formats()
    {
        using var workbook = Open(new SyncfusionExcelExporter().Build(Advice));
        var sheet = workbook.Worksheets[0];

        Assert.Equal(41_200.50, sheet.Range["C5"].Number);
        Assert.Contains("₹", sheet.Range["C5"].NumberFormat);
        Assert.True(sheet.Range["D5"].HasDateTime);
        Assert.Equal(new DateTime(2026, 10, 31), sheet.Range["D5"].DateTime);
    }

    [Fact]
    public void Sheet_names_are_made_legal_and_several_sheets_are_kept()
    {
        var second = Advice with { Name = "Employees" };
        using var workbook = Open(new SyncfusionExcelExporter().Build(Advice, second));

        // Without SYNCFUSION_LICENSE_KEY, XlsIO appends its own trial-notice sheet after ours.
        Assert.True(workbook.Worksheets.Count >= 2);
        Assert.Equal("Salary advice Oct2026", workbook.Worksheets[0].Name);
        Assert.Equal("Employees", workbook.Worksheets[1].Name);
    }

    private static OpenedWorkbook Open(byte[] bytes) => new(bytes);

    /// <summary>Keeps the engine alive for as long as the workbook is being read.</summary>
    private sealed class OpenedWorkbook : IDisposable
    {
        private readonly ExcelEngine engine = new();
        private readonly MemoryStream stream;
        private readonly IWorkbook workbook;

        public OpenedWorkbook(byte[] bytes)
        {
            stream = new MemoryStream(bytes);
            workbook = engine.Excel.Workbooks.Open(stream);
        }

        public IWorksheets Worksheets => workbook.Worksheets;

        public void Dispose()
        {
            workbook.Close();
            stream.Dispose();
            engine.Dispose();
        }
    }
}
