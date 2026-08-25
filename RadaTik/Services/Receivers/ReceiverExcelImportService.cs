using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;

namespace RadaTik.Services.Receivers;

public sealed class ReceiverExcelImportService(
    ApplicationDbContext context,
    ILogger<ReceiverExcelImportService> logger)
    : ApplicationServiceBase(context), IReceiverExcelImportService
{
    private const int MaxImportRows = 1000;
    private const string PreferredSheetNameMarker = "مستقبلات";
    private const string DefaultNetworkMask = "255.255.255.0";

    private static readonly Regex Ipv4Regex = new(
        @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] NameHeaders =
        ["اسم المستقبل", "اسم اللاقط", "المستقبل", "اللاقط", "name", "receiver"];
    private static readonly string[] SectorHeaders =
        ["اسم المرسل", "المرسل", "القطاع", "اسم القطاع", "sector", "sector_name", "transmitter"];
    private static readonly string[] IpHeaders =
        ["عنوان ip", "عنوان الـ ip", "الايبي", "ip", "ipaddress", "ip_address"];
    private static readonly string[] LongitudeHeaders =
        ["خط الطول", "الطول", "longitude", "lng", "lon", "long"];
    private static readonly string[] LatitudeHeaders =
        ["خط العرض", "العرض", "latitude", "lat"];
    private static readonly string[] AntennaHeaders =
        ["ارتفاع الهوائي", "ارتفاع الهوائي عن الأرض", "الهوائي", "antenna", "antenna_height", "agl"];

    public byte[] BuildTemplateWorkbook()
    {
        using XLWorkbook wb = new XLWorkbook();
        IXLWorksheet ws = CreateSheetWithHeaders(wb);
        ws.Cell(2, 1).Value = "لاقط 1";
        ws.Cell(2, 2).Value = "اسم المرسل كما هو في النظام";
        ws.Cell(2, 3).Value = "10.1.1.20";
        ws.Cell(2, 4).Value = 36.2910;
        ws.Cell(2, 5).Value = 33.5110;
        ws.Cell(2, 6).Value = 6;
        ws.Columns().AdjustToContents();
        using MemoryStream ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> BuildExportWorkbookAsync(int networkId, CancellationToken ct = default)
    {
        List<Receiver> receivers = await Db.Receivers
            .AsNoTracking()
            .Include(r => r.Sector)
            .Where(r => r.NetworkId == networkId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        using XLWorkbook wb = new XLWorkbook();
        IXLWorksheet ws = CreateSheetWithHeaders(wb);
        int row = 2;
        foreach (Receiver receiver in receivers)
        {
            ws.Cell(row, 1).Value = receiver.Name ?? string.Empty;
            ws.Cell(row, 2).Value = receiver.Sector?.Name ?? string.Empty;
            ws.Cell(row, 3).Value = FormatIpForExport(receiver.IPAddress, receiver.NetworkMask);
            ws.Cell(row, 4).Value = receiver.Longitude;
            ws.Cell(row, 5).Value = receiver.Latitude;
            if (receiver.AntennaHeightAglMeters.HasValue)
            {
                ws.Cell(row, 6).Value = receiver.AntennaHeightAglMeters.Value;
            }

            row++;
        }

        ws.Columns().AdjustToContents();
        using MemoryStream ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<ReceiverExcelImportParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        int networkId,
        CancellationToken ct = default)
    {
        if (fileStream == null || !fileStream.CanRead)
        {
            return ReceiverExcelImportParseResult.Fail("الملف غير صالح.");
        }

        string ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        List<Dictionary<string, string>> rows;
        string? sheetNote;
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
            logger.LogWarning(ex, "تعذر قراءة ملف استيراد المستقبلات {FileName}", fileName);
            return ReceiverExcelImportParseResult.Fail(ex.Message);
        }

        if (rows.Count == 0)
        {
            return ReceiverExcelImportParseResult.Fail(
                (sheetNote != null ? sheetNote + " " : string.Empty) + "الملف لا يحتوي على صفوف بيانات.");
        }

        if (rows.Count > MaxImportRows)
        {
            return ReceiverExcelImportParseResult.Fail(
                $"عدد الصفوف أكبر من الحد المسموح ({MaxImportRows}). قسّم الملف ثم أعد المحاولة.");
        }

        List<Sector> sectors = await Db.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId == networkId)
            .Select(s => new Sector { Id = s.Id, Name = s.Name })
            .ToListAsync(ct);

        if (sectors.Count == 0)
        {
            return ReceiverExcelImportParseResult.Fail("لا يوجد مرسل في الشبكة الحالية لربط المستقبلات به.");
        }

        var existing = await Db.Receivers
            .AsNoTracking()
            .Where(r => r.NetworkId == networkId)
            .Select(r => new { r.Name, r.IPAddress })
            .ToListAsync(ct);

        HashSet<string> existingNames = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name!.Trim().ToLowerInvariant())
            .ToHashSet();
        HashSet<string> existingIps = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.IPAddress))
            .Select(x => x.IPAddress!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int skipped = 0;
        int failed = 0;
        List<string> details = [];
        List<Receiver> receivers = [];
        int rowNumber = 1;

        foreach (Dictionary<string, string> row in rows)
        {
            rowNumber++;
            string? name = GetField(row, NameHeaders);
            string? sectorName = GetField(row, SectorHeaders);
            string? ipRaw = GetField(row, IpHeaders);
            string? longitudeRaw = GetField(row, LongitudeHeaders);
            string? latitudeRaw = GetField(row, LatitudeHeaders);
            string? antennaRaw = GetField(row, AntennaHeaders);

            if (IsCompletelyEmpty(name, sectorName, ipRaw, longitudeRaw, latitudeRaw, antennaRaw))
            {
                skipped++;
                continue;
            }

            List<string> rowErrors = [];
            if (string.IsNullOrWhiteSpace(name))
            {
                rowErrors.Add("اسم المستقبل مطلوب");
            }
            else if (name.Trim().Length > 100)
            {
                rowErrors.Add("اسم المستقبل أطول من 100 حرف");
            }

            Sector? sector = ResolveSector(sectors, sectorName);
            if (sector == null)
            {
                rowErrors.Add(string.IsNullOrWhiteSpace(sectorName)
                    ? "اسم المرسل مطلوب"
                    : $"لا يوجد مرسل باسم «{sectorName.Trim()}» في الشبكة الحالية");
            }

            if (!TryParseIpAndMask(ipRaw, out string ip, out string mask, out string? ipError))
            {
                rowErrors.Add(ipError ?? "عنوان IP غير صحيح");
            }

            if (!TryParseDouble(latitudeRaw, out double latitude) || latitude < -90 || latitude > 90)
            {
                rowErrors.Add("خط العرض يجب أن يكون رقماً بين -90 و 90");
            }

            if (!TryParseDouble(longitudeRaw, out double longitude) || longitude < -180 || longitude > 180)
            {
                rowErrors.Add("خط الطول يجب أن يكون رقماً بين -180 و 180");
            }

            double? antennaHeight = null;
            if (!string.IsNullOrWhiteSpace(antennaRaw))
            {
                if (!TryParseDouble(antennaRaw, out double parsedAntenna) || parsedAntenna < 0 || parsedAntenna > 500)
                {
                    rowErrors.Add("ارتفاع الهوائي يجب أن يكون رقماً بين 0 و 500 متر");
                }
                else
                {
                    antennaHeight = parsedAntenna;
                }
            }

            if (rowErrors.Count > 0)
            {
                failed++;
                details.Add($"صف {rowNumber}: {string.Join("؛ ", rowErrors)}.");
                continue;
            }

            string nameKey = name!.Trim().ToLowerInvariant();
            if (existingNames.Contains(nameKey))
            {
                skipped++;
                details.Add($"صف {rowNumber}: المستقبل «{name.Trim()}» موجود مسبقاً — تم التخطي.");
                continue;
            }

            if (existingIps.Contains(ip))
            {
                skipped++;
                details.Add($"صف {rowNumber}: عنوان IP «{ip}» مستخدم مسبقاً — تم التخطي.");
                continue;
            }

            receivers.Add(new Receiver
            {
                Name = name.Trim(),
                IPAddress = ip,
                NetworkMask = mask,
                Latitude = latitude,
                Longitude = longitude,
                AntennaHeightAglMeters = antennaHeight,
                SectorId = sector!.Id,
                NetworkId = networkId,
                IsActive = true,
                CreatedDate = DateTime.Now
            });
            existingNames.Add(nameKey);
            existingIps.Add(ip);
        }

        string note = string.IsNullOrWhiteSpace(sheetNote) ? string.Empty : sheetNote.Trim() + " ";
        string message = receivers.Count == 0
            ? note + $"لا توجد مستقبلات جديدة صالحة للاستيراد. تم تخطي {skipped} وفشل {failed}."
            : note + $"جاهز لإضافة {receivers.Count} مستقبل. تم تخطي {skipped} وفشل التحقق من {failed}.";
        return ReceiverExcelImportParseResult.FromRows(message, rows.Count, skipped, failed, receivers, details);
    }

    public async Task<ReceiverExcelImportResult> CommitAsync(
        IReadOnlyList<Receiver> receivers,
        CancellationToken ct = default)
    {
        if (receivers == null || receivers.Count == 0)
        {
            return ReceiverExcelImportResult.Fail("لا توجد مستقبلات صالحة للحفظ.");
        }

        Db.Receivers.AddRange(receivers);
        await Db.SaveChangesAsync(ct);
        return ReceiverExcelImportResult.Ok($"تم إضافة {receivers.Count} مستقبل من الملف.", receivers.Count);
    }

    private static IXLWorksheet CreateSheetWithHeaders(XLWorkbook wb)
    {
        IXLWorksheet ws = wb.Worksheets.Add(PreferredSheetNameMarker);
        ws.Cell(1, 1).Value = "اسم المستقبل";
        ws.Cell(1, 2).Value = "اسم المرسل";
        ws.Cell(1, 3).Value = "عنوان IP";
        ws.Cell(1, 4).Value = "خط الطول";
        ws.Cell(1, 5).Value = "خط العرض";
        ws.Cell(1, 6).Value = "ارتفاع الهوائي";
        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        return ws;
    }

    private static string FormatIpForExport(string? ip, string? mask)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(mask) || mask == DefaultNetworkMask)
        {
            return ip.Trim();
        }

        int? prefix = MaskToPrefix(mask);
        return prefix.HasValue ? $"{ip.Trim()}/{prefix.Value}" : ip.Trim();
    }

    private static int? MaskToPrefix(string mask)
    {
        if (!IPAddress.TryParse(mask, out IPAddress? address))
        {
            return null;
        }

        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return null;
        }

        uint bits = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        int prefix = 0;
        bool seenZero = false;
        for (int i = 31; i >= 0; i--)
        {
            bool bit = (bits & (1u << i)) != 0;
            if (bit)
            {
                if (seenZero)
                {
                    return null;
                }

                prefix++;
            }
            else
            {
                seenZero = true;
            }
        }

        return prefix;
    }

    private static Sector? ResolveSector(List<Sector> sectors, string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return null;
        }

        string key = rawName.Trim();
        return sectors.FirstOrDefault(s => string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCompletelyEmpty(params string?[] values) =>
        values.All(string.IsNullOrWhiteSpace);

    private static bool TryParseIpAndMask(string? raw, out string ip, out string mask, out string? error)
    {
        ip = string.Empty;
        mask = DefaultNetworkMask;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "عنوان IP مطلوب";
            return false;
        }

        string value = NormalizeDigits(raw.Trim());
        int slash = value.IndexOf('/');
        if (slash > 0)
        {
            string ipPart = value[..slash].Trim();
            string prefixPart = value[(slash + 1)..].Trim();
            if (!Ipv4Regex.IsMatch(ipPart) || !IPAddress.TryParse(ipPart, out IPAddress? parsedIp))
            {
                error = "عنوان IP غير صحيح";
                return false;
            }

            if (!int.TryParse(prefixPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int prefix)
                || prefix < 0 || prefix > 32)
            {
                error = "قناع الشبكة في صيغة CIDR غير صحيح";
                return false;
            }

            ip = parsedIp.ToString();
            mask = PrefixToMask(prefix);
            return true;
        }

        if (!Ipv4Regex.IsMatch(value) || !IPAddress.TryParse(value, out IPAddress? address))
        {
            error = "عنوان IP غير صحيح";
            return false;
        }

        ip = address.ToString();
        return true;
    }

    private static string PrefixToMask(int prefix)
    {
        uint bits = prefix == 0 ? 0u : 0xffffffffu << (32 - prefix);
        return string.Join(".", (bits >> 24) & 255, (bits >> 16) & 255, (bits >> 8) & 255, bits & 255);
    }

    private static bool TryParseDouble(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string normalized = NormalizeDigits(raw.Trim()).Replace('،', '.').Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeDigits(string value)
    {
        StringBuilder sb = new(value.Length);
        foreach (char ch in value)
        {
            if (ch is >= '٠' and <= '٩')
            {
                sb.Append((char)('0' + (ch - '٠')));
            }
            else if (ch is >= '۰' and <= '۹')
            {
                sb.Append((char)('0' + (ch - '۰')));
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
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
            sheetNote = $"تم استخدام الورقة «{worksheet.Name}».";
        }
        else
        {
            IXLWorksheet? matched = sheets.FirstOrDefault(w =>
                w.Name.Contains(PreferredSheetNameMarker, StringComparison.OrdinalIgnoreCase));
            worksheet = matched ?? sheets[0];
            sheetNote = matched == null
                ? $"الملف يحتوي {sheets.Count} أوراق؛ تم استخدام الورقة الأولى «{worksheet.Name}»."
                : $"تم اختيار الورقة «{worksheet.Name}».";
        }

        IXLRange? used = worksheet.RangeUsed();
        if (used == null)
        {
            return ([], sheetNote + " الورقة فارغة.");
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
            throw new InvalidOperationException(sheetNote + " تعذر قراءة عناوين الأعمدة.");
        }

        List<Dictionary<string, string>> rows = [];
        for (int row = firstRow + 1; row <= lastRow; row++)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            bool any = false;
            foreach ((int col, string header) in headers)
            {
                string value = GetCellText(worksheet.Cell(row, col));
                any |= !string.IsNullOrWhiteSpace(value);
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
        string[] headers = SplitCsvLine(headerLine, sep).Select(NormalizeHeader).ToArray();
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
                any |= !string.IsNullOrWhiteSpace(value);
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
        if (cell.TryGetValue(out double number))
        {
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

        string value = header.Trim().ToLowerInvariant().Replace('\u0640', ' ');
        return Regex.Replace(value, @"\s+", " ");
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

        foreach ((string header, string value) in row)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (string alias in aliases)
            {
                string a = NormalizeHeader(alias);
                if (header == a || header.StartsWith(a + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }
}
