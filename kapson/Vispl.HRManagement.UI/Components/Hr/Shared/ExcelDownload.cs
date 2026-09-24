using Microsoft.JSInterop;
using Vispl.HRManagement.BM.ITF.Shared;

namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>
/// Builds a workbook on the server and hands it to the browser. Every portal "Export" goes through
/// here so all downloads are real .xlsx files with the same look.
/// </summary>
public static class ExcelDownload
{
    public static ValueTask SaveAsync(IJSRuntime js, IExcelExporter excel, string fileName, params ExcelSheet[] sheets) =>
        SaveAsync(js, fileName, excel.Build(sheets));

    public static ValueTask SaveAsync(IJSRuntime js, string fileName, byte[] workbook) =>
        js.InvokeVoidAsync("hrPayroll.downloadBase64", fileName, Convert.ToBase64String(workbook), ExcelContentTypes.Xlsx);

    /// <summary>A row of cell values, written in column order.</summary>
    public static IReadOnlyList<object?> Row(params object?[] values) => values;
}
