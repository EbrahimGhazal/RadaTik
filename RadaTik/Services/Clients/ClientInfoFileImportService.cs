using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

/// <summary>
/// استيراد معلومات المشتركين من Excel/CSV:
/// المطابقة بالحساب (UserName)، ثم تحديث الاسم الكامل والجوال وآخر تجديد والبناء وتاريخ الإضافة عند توفرها فقط.
/// إن تكرر الحساب أو الاسم الكامل في قاعدة البيانات تُطبَّق نفس المعلومات على كل التكرارات.
/// إذا كان تاريخ آخر تجديد لم يتجاوز 30 يوماً من تاريخ الاستعادة يُجدَّد الاشتراك حتى يوم 10 من الشهر التالي.
/// </summary>
public sealed class ClientInfoFileImportService(
    ApplicationDbContext context,
    ILogger<ClientInfoFileImportService> logger)
    : ApplicationServiceBase(context), IClientInfoFileImportService
{
    private const int RecentRenewalDaysThreshold = 30;
    private const int NextMonthRenewalDay = 10;

    private static readonly string PreferredSheetNameMarker = "مشتركين كامل";

    private static readonly string[] UserNameHeaders =
    [
        "اسم المستخدم", "الحساب", "الحساب في السيرفر", "username", "user", "user_name", "login", "account"
    ];

    private static readonly string[] NameHeaders =
    [
        "الاسم الكامل", "الاسم", "الاسم الثلاثي", "name", "full_name", "fullname"
    ];

    private static readonly string[] PhoneHeaders =
    [
        "الجوال", "الهاتف", "الموبايل", "رقم الجوال", "phone", "mobile", "phone_number"
    ];

    private static readonly string[] RenewalHeaders =
    [
        "آخر تجديد", "تاريخ التجديد", "تاريخ آخر تجديد", "renewal", "last_renewal", "renewal_date"
    ];

    private static readonly string[] BuildingHeaders =
    [
        "البناء", "المبنى", "building"
    ];

    private static readonly string[] CreatedDateHeaders =
    [
        "تاريخ الإضافة", "تاريخ الاضافة", "تاريخ الإضافه", "تاريخ الإضافة في قاعدة البيانات",
        "تاريخ الإضافة في القاعدة", "created", "created_date", "createddate", "date_added", "added_date"
    ];

    public byte[] BuildTemplateWorkbook()
    {
        using XLWorkbook wb = new XLWorkbook();
        IXLWorksheet ws = wb.Worksheets.Add(PreferredSheetNameMarker);
        ws.Cell(1, 1).Value = "الحساب";
        ws.Cell(1, 2).Value = "الاسم الكامل";
        ws.Cell(1, 3).Value = "الجوال";
        ws.Cell(1, 4).Value = "آخر تجديد";
        ws.Cell(1, 5).Value = "البناء";
        ws.Cell(1, 6).Value = "تاريخ الإضافة";
        ws.Row(1).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "user@example.com";
        ws.Cell(2, 2).Value = "اسم المشترك الكامل";
        ws.Cell(2, 3).Value = "0944123456";
        ws.Cell(2, 4).Value = DateTime.Today;
        ws.Cell(2, 4).Style.DateFormat.Format = "yyyy-mm-dd";
        ws.Cell(2, 5).Value = "بناء أ";
        ws.Cell(2, 6).Value = DateTime.Today.AddMonths(-2);
        ws.Cell(2, 6).Style.DateFormat.Format = "yyyy-mm-dd";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using MemoryStream ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<ClientInfoFileImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        int networkId,
        CancellationToken ct = default)
    {
        if (fileStream == null || !fileStream.CanRead)
        {
            return ClientInfoFileImportResult.Fail("الملف غير صالح.");
        }

        string ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        List<Dictionary<string, string>> rows;
        string? sheetNote = null;
        try
        {
            if (ext is ".xlsx" or ".xlsm")
            {
                (rows, sheetNote) = ReadExcelRows(fileStream);
            }
            else if (ext is ".csv" or ".txt")
            {
                rows = ReadCsvRows(fileStream);
                sheetNote = "ملف CSV لا يحتوي أوراقاً متعددة؛ تم قراءة الملف كاملاً.";
            }
            else
            {
                throw new InvalidOperationException("صيغة الملف غير مدعومة. استخدم Excel (.xlsx) أو CSV.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "تعذر قراءة ملف استيراد معلومات المشتركين {FileName}", fileName);
            return ClientInfoFileImportResult.Fail(ex.Message);
        }

        if (rows.Count == 0)
        {
            return ClientInfoFileImportResult.Fail(
                (sheetNote != null ? sheetNote + " " : string.Empty) +
                "الملف لا يحتوي على صفوف بيانات.");
        }

        List<Client> clients = await Db.Clients
            .Where(c => c.NetworkId == networkId)
            .ToListAsync(ct);

        Dictionary<string, List<Client>> byUserName = clients
            .Where(c => !string.IsNullOrWhiteSpace(c.UserName))
            .GroupBy(c => c.UserName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, List<Client>> byFullName = clients
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        int updated = 0;
        int skipped = 0;
        int failed = 0;
        int renewedToNextMonth10 = 0;
        List<string> details = [];
        int rowNumber = 1;
        DateTime restoreDate = DateTime.Today;

        foreach (Dictionary<string, string> row in rows)
        {
            rowNumber++;
            string? userName = GetField(row, UserNameHeaders);
            string? name = GetField(row, NameHeaders);
            string? phone = GetField(row, PhoneHeaders);
            string? building = GetField(row, BuildingHeaders);
            string? renewalRaw = GetField(row, RenewalHeaders);
            string? createdRaw = GetField(row, CreatedDateHeaders);

            if (string.IsNullOrWhiteSpace(userName))
            {
                skipped++;
                details.Add($"صف {rowNumber}: لا يوجد حساب للمطابقة — تم التخطي.");
                continue;
            }

            bool hasAnyUpdatable =
                !string.IsNullOrWhiteSpace(name) ||
                !string.IsNullOrWhiteSpace(phone) ||
                !string.IsNullOrWhiteSpace(building) ||
                !string.IsNullOrWhiteSpace(renewalRaw) ||
                !string.IsNullOrWhiteSpace(createdRaw);

            if (!hasAnyUpdatable)
            {
                skipped++;
                details.Add($"صف {rowNumber}: لا توجد حقول قابلة للاستيراد للحساب «{userName}» — تم التخطي.");
                continue;
            }

            if (!byUserName.TryGetValue(userName.Trim(), out List<Client>? accountMatches) || accountMatches.Count == 0)
            {
                failed++;
                details.Add($"صف {rowNumber}: لا يوجد حساب مطابق لـ «{userName.Trim()}» في الشبكة الحالية.");
                continue;
            }

            // الهدف: كل تكرارات الحساب + إن وُجد اسم كامل مكرر في DB لنفس الاسم المستورد/الموجود تُحدَّث كلها
            Dictionary<int, Client> targets = accountMatches.ToDictionary(c => c.Id);
            string? nameForDuplicates = !string.IsNullOrWhiteSpace(name)
                ? name.Trim()
                : accountMatches[0].Name?.Trim();

            if (!string.IsNullOrWhiteSpace(nameForDuplicates) &&
                byFullName.TryGetValue(nameForDuplicates, out List<Client>? nameMatches) &&
                nameMatches.Count > 1)
            {
                foreach (Client duplicate in nameMatches)
                {
                    targets[duplicate.Id] = duplicate;
                }
            }

            try
            {
                DateTime? renewalDate = null;
                if (!string.IsNullOrWhiteSpace(renewalRaw))
                {
                    if (!TryParseDate(renewalRaw, out DateTime parsedRenewal))
                    {
                        failed++;
                        details.Add($"صف {rowNumber}: تاريخ «آخر تجديد» غير صالح ({renewalRaw}).");
                        continue;
                    }

                    renewalDate = parsedRenewal;
                }

                DateTime? createdDate = null;
                if (!string.IsNullOrWhiteSpace(createdRaw))
                {
                    if (!TryParseDate(createdRaw, out DateTime parsedCreated))
                    {
                        failed++;
                        details.Add($"صف {rowNumber}: تاريخ «تاريخ الإضافة» غير صالح ({createdRaw}).");
                        continue;
                    }

                    createdDate = parsedCreated;
                }

                int rowUpdated = 0;
                int rowRenewed = 0;
                foreach (Client client in targets.Values)
                {
                    if (ApplyClientInfoUpdates(
                            client,
                            name,
                            phone,
                            building,
                            renewalDate,
                            createdDate,
                            restoreDate,
                            out bool grantedNextMonth10))
                    {
                        rowUpdated++;
                        updated++;
                    }

                    if (grantedNextMonth10)
                    {
                        rowRenewed++;
                        renewedToNextMonth10++;
                    }
                }

                if (rowUpdated == 0)
                {
                    skipped++;
                    details.Add($"صف {rowNumber}: لا تغيير على الحساب {userName.Trim()}.");
                }
                else if (targets.Count > 1)
                {
                    details.Add(
                        $"صف {rowNumber}: تم تحديث {rowUpdated} سجلاً مكرراً لنفس الحساب/الاسم «{userName.Trim()}».");
                }

                if (rowRenewed > 0)
                {
                    DateTime expiration = ComputeNextMonthDay10(restoreDate);
                    details.Add(
                        $"صف {rowNumber}: تم تجديد الاشتراك لـ {rowRenewed} سجلاً حتى {expiration:yyyy/MM/dd} (يوم {NextMonthRenewalDay} من الشهر التالي) لأن آخر تجديد خلال {RecentRenewalDaysThreshold} يوماً من الاستعادة.");
                }
            }
            catch (Exception ex)
            {
                failed++;
                details.Add($"صف {rowNumber}: {ex.Message}");
                logger.LogWarning(ex, "فشل تحديث صف استيراد معلومات مشترك رقم {Row}", rowNumber);
            }
        }

        if (updated > 0)
        {
            await Db.SaveChangesAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(sheetNote))
        {
            details.Insert(0, sheetNote);
        }

        string message =
            (string.IsNullOrWhiteSpace(sheetNote) ? string.Empty : sheetNote + " ") +
            $"تم تحديث {updated} مشترك من أصل {rows.Count} صف" +
            (skipped > 0 ? $"، تخطي {skipped}" : "") +
            (failed > 0 ? $"، فشل {failed}" : "") +
            (renewedToNextMonth10 > 0
                ? $"، تجديد حتى يوم {NextMonthRenewalDay} للشهر التالي لـ {renewedToNextMonth10}"
                : "") +
            ".";

        return ClientInfoFileImportResult.Ok(message, rows.Count, updated, skipped, failed, details);
    }

    private static bool ApplyClientInfoUpdates(
        Client client,
        string? name,
        string? phone,
        string? building,
        DateTime? renewalDate,
        DateTime? createdDate,
        DateTime restoreDate,
        out bool grantedNextMonth10)
    {
        bool changed = false;
        grantedNextMonth10 = false;

        if (!string.IsNullOrWhiteSpace(name) &&
            !string.Equals(client.Name, name.Trim(), StringComparison.Ordinal))
        {
            client.Name = name.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            string normalized = NormalizePhone(phone);
            if (normalized.Length >= 7 &&
                !string.Equals(NormalizePhone(client.PhoneNumber ?? string.Empty), normalized, StringComparison.Ordinal))
            {
                client.PhoneNumber = phone.Trim();
                changed = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(building) &&
            !string.Equals(client.Building ?? string.Empty, building.Trim(), StringComparison.Ordinal))
        {
            client.Building = building.Trim();
            changed = true;
        }

        if (createdDate.HasValue && client.CreatedDate.Date != createdDate.Value.Date)
        {
            // الحفاظ على وقت اليوم إن وُجد، وإلا منتصف الليل كما في ملف الإكسل
            client.CreatedDate = createdDate.Value.TimeOfDay == TimeSpan.Zero
                ? createdDate.Value.Date
                : createdDate.Value;
            changed = true;
        }

        if (renewalDate.HasValue)
        {
            if (client.LastRenewalDate?.Date != renewalDate.Value.Date)
            {
                client.LastRenewalDate = renewalDate.Value.Date;
                changed = true;
            }

            // إن لم يتجاوز آخر تجديد 30 يوماً من تاريخ الاستعادة → انتهاء الاشتراك = 10 من الشهر التالي
            if (IsWithinRecentRenewalWindow(renewalDate.Value, restoreDate))
            {
                DateTime expiration = ComputeNextMonthDay10(restoreDate);
                if (client.AccountExpirationDate?.Date != expiration)
                {
                    client.AccountExpirationDate = expiration;
                    changed = true;
                    grantedNextMonth10 = true;
                }
            }
        }

        if (changed)
        {
            client.LastUpdated = DateTime.Now;
        }

        return changed;
    }

    private static bool IsWithinRecentRenewalWindow(DateTime lastRenewalDate, DateTime restoreDate) =>
        (restoreDate.Date - lastRenewalDate.Date).TotalDays <= RecentRenewalDaysThreshold;

    private static DateTime ComputeNextMonthDay10(DateTime fromDate)
    {
        DateTime nextMonth = fromDate.Date.AddMonths(1);
        return new DateTime(nextMonth.Year, nextMonth.Month, NextMonthRenewalDay);
    }

    private static (List<Dictionary<string, string>> Rows, string SheetNote) ReadExcelRows(Stream stream)
    {
        using XLWorkbook workbook = new(stream);
        List<IXLWorksheet> sheets = workbook.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible).ToList();
        if (sheets.Count == 0)
        {
            throw new InvalidOperationException("الملف لا يحتوي على أوراق عمل ظاهرة.");
        }

        IXLWorksheet worksheet;
        string sheetNote;

        if (sheets.Count == 1)
        {
            worksheet = sheets[0];
            bool nameMatches = worksheet.Name.Contains(PreferredSheetNameMarker, StringComparison.OrdinalIgnoreCase);
            sheetNote = nameMatches
                ? $"ملاحظة: تم استخدام الورقة «{worksheet.Name}»."
                : $"ملاحظة: الملف يحتوي ورقة واحدة («{worksheet.Name}») وتم استخدامها. عند وجود أكثر من ورقة يُبحث عن ورقة اسمها يحتوي «{PreferredSheetNameMarker}».";
        }
        else
        {
            IXLWorksheet? matched = sheets.FirstOrDefault(w =>
                w.Name.Contains(PreferredSheetNameMarker, StringComparison.OrdinalIgnoreCase));

            if (matched == null)
            {
                string available = string.Join("، ", sheets.Select(s => s.Name));
                throw new InvalidOperationException(
                    $"ملاحظة: الملف يحتوي {sheets.Count} أوراق. يجب وجود ورقة اسمها يحتوي «{PreferredSheetNameMarker}». الأوراق الموجودة: {available}.");
            }

            worksheet = matched;
            sheetNote =
                $"ملاحظة: وُجد أكثر من ورقة ({sheets.Count})؛ تم اختيار الورقة «{worksheet.Name}» لأنها تحتوي الاسم «{PreferredSheetNameMarker}».";
        }

        IXLRange? used = worksheet.RangeUsed();
        if (used == null)
        {
            return ([], sheetNote + " الورقة المحددة فارغة.");
        }

        int firstRow = used.FirstRow().RowNumber();
        int lastRow = used.LastRow().RowNumber();
        int firstCol = used.FirstColumn().ColumnNumber();
        int lastCol = used.LastColumn().ColumnNumber();

        Dictionary<int, string> headers = new();
        for (int col = firstCol; col <= lastCol; col++)
        {
            string header = NormalizeHeader(GetCellText(worksheet.Cell(firstRow, col)));
            if (!string.IsNullOrWhiteSpace(header))
            {
                headers[col] = header;
            }
        }

        if (headers.Count == 0)
        {
            throw new InvalidOperationException(sheetNote + " تعذر قراءة عناوين الأعمدة من الورقة المحددة.");
        }

        List<Dictionary<string, string>> rows = [];
        for (int row = firstRow + 1; row <= lastRow; row++)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            bool any = false;
            foreach ((int col, string header) in headers)
            {
                string value = GetCellText(worksheet.Cell(row, col));
                if (!string.IsNullOrWhiteSpace(value))
                {
                    any = true;
                }

                map[header] = value;
            }

            if (any)
            {
                rows.Add(map);
            }
        }

        return (rows, sheetNote);
    }

    private static List<Dictionary<string, string>> ReadCsvRows(Stream stream)
    {
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return [];
        }

        char sep = headerLine.Contains(';') && !headerLine.Contains(',') ? ';' : ',';
        string[] headers = SplitCsvLine(headerLine, sep)
            .Select(NormalizeHeader)
            .ToArray();

        List<Dictionary<string, string>> rows = [];
        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] values = SplitCsvLine(line, sep);
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            bool any = false;
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(headers[i]))
                {
                    continue;
                }

                string value = i < values.Length ? values[i].Trim().Trim('"') : string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    any = true;
                }

                map[headers[i]] = value;
            }

            if (any)
            {
                rows.Add(map);
            }
        }

        return rows;
    }

    private static string[] SplitCsvLine(string line, char sep)
    {
        List<string> parts = [];
        StringBuilder current = new();
        bool inQuotes = false;
        foreach (char ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == sep && !inQuotes)
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        parts.Add(current.ToString());
        return parts.ToArray();
    }

    private static string GetCellText(IXLCell cell)
    {
        if (cell.TryGetValue(out DateTime dt))
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (cell.TryGetValue(out double number))
        {
            // Excel serial date
            if (cell.Style.DateFormat.Format.Contains('y', StringComparison.OrdinalIgnoreCase) ||
                cell.Style.DateFormat.Format.Contains('d', StringComparison.OrdinalIgnoreCase) ||
                cell.Style.DateFormat.Format.Contains('m', StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return DateTime.FromOADate(number).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch
                {
                    // fall through
                }
            }

            return number.ToString(CultureInfo.InvariantCulture);
        }

        return cell.GetString()?.Trim() ?? string.Empty;
    }

    private static string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        string value = header.Trim().ToLowerInvariant();
        value = value.Replace('\u0640', ' ');
        value = Regex.Replace(value, @"\s+", " ");
        return value;
    }

    private static string? GetField(Dictionary<string, string> row, string[] aliases)
    {
        foreach (string alias in aliases)
        {
            string key = NormalizeHeader(alias);
            if (row.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        // أيضاً مطابقة مرنة: يحتوي العنوان على أحد الأسماء
        foreach ((string header, string value) in row)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (string alias in aliases)
            {
                string a = NormalizeHeader(alias);
                if (header == a || header.Contains(a, StringComparison.OrdinalIgnoreCase))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }

    private static string NormalizePhone(string phone)
    {
        return Regex.Replace(phone ?? string.Empty, @"[^\d]", string.Empty);
    }

    private static bool TryParseDate(string raw, out DateTime date)
    {
        raw = raw.Trim();
        string[] formats =
        [
            "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "dd-MM-yyyy",
            "yyyy-M-d", "dd/M/yyyy", "d/M/yyyy", "MM/dd/yyyy"
        ];

        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateTime.TryParse(raw, new CultureInfo("ar-SY"), DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double oa))
        {
            try
            {
                date = DateTime.FromOADate(oa);
                return true;
            }
            catch
            {
                // ignore
            }
        }

        date = default;
        return false;
    }
}
