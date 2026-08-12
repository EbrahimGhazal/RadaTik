using ClosedXML.Excel;

namespace RadaTik.Helpers;

public static class CompanyBusinessExcelHelper
{
    public static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "export" : name.Trim();
    }

    public static byte[] BuildWorkbook(Action<IXLWorksheet> fillFirstSheet)
    {
        using XLWorkbook wb = new XLWorkbook();
        IXLWorksheet ws = wb.Worksheets.Add("البيانات");
        fillFirstSheet(ws);
        ws.Columns().AdjustToContents();
        using MemoryStream ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
