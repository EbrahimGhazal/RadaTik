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

namespace RadaTik.Services.Sectors;

/// <summary>
/// استيراد مرسلات (قطاعات) جديدة من Excel/CSV وربطها بخادم MikroTik موجود في الشبكة الحالية.
/// </summary>
public sealed class SectorExcelImportService(
    ApplicationDbContext context,
    ILogger<SectorExcelImportService> logger)
    : ApplicationServiceBase(context), ISectorExcelImportService
{
    private const int MaxImportRows = 1000;
    private const string PreferredSheetNameMarker = "مرسلات";
    private const string DefaultNetworkMask = "255.255.255.0";

    private static readonly Regex Ipv4Regex = new(
        @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] NameHeaders =
    [
        "اسم المرسل", "اسم القطاع", "المرسل", "القطاع", "name", "sector", "transmitter", "sector_name"
    ];

    private static readonly string[] ServerHeaders =
    [
        "اسم الخادم", "الخادم", "السيرفر", "اسم السيرفر", "server", "server_name", "mikrotik", "mikrotik_server"
    ];

    private static readonly string[] IpHeaders =
    [
        "عنوان ip", "عنوان الـ ip", "الايبي", "ip", "ipaddress", "ip_address", "address"
    ];

    private static readonly string[] LongitudeHeaders =
    [
        "خط الطول", "الطول", "longitude", "lng", "lon", "long"
    ];

    private static readonly string[] LatitudeHeaders =
    [
        "خط العرض", "العرض", "latitude", "lat"
    ];

    private static readonly string[] DirectionHeaders =
    [
        "الاتجاه", "اتجاه", "direction", "azimuth", "heading"
    ];

    private static readonly string[] AngleHeaders =
    [
        "الزاوية", "زاوية", "زاوية الانتشار", "angle", "coverage_angle", "beamwidth"
    ];

    private static readonly string[] RangeHeaders =
    [
        "المدى", "مدى", "مدى الانتشار", "range", "coverage_range", "coverage"
    ];

    private static readonly string[] AntennaHeaders =
    [
        "ارتفاع الهوائي", "ارتفاع الهوائي عن الأرض", "الهوائي", "antenna", "antenna_height", "agl"
    ];

    public byte[] BuildTemplateWorkbook()
    {
        using XLWorkbook wb = new XLWorkbook();
        IXLWorksheet ws = wb.Worksheets.Add(PreferredSheetNameMarker);
        ws.Cell(1, 1).Value = "اسم المرسل";
        ws.Cell(1, 2).Value = "اسم الخادم";
        ws.Cell(1, 3).Value = "عنوان IP";
        ws.Cell(1, 4).Value = "خط الطول";
        ws.Cell(1, 5).Value = "خط العرض";
        ws.Cell(1, 6).Value = "الاتجاه";
        ws.Cell(1, 7).Value = "الزاوية";
        ws.Cell(1, 8).Value = "المدى";
        ws.Cell(1, 9).Value = "ارتفاع الهوائي";
        ws.Row(1).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "قطاع الشمال";
        ws.Cell(2, 2).Value = "اسم الخادم كما هو في النظام";
        ws.Cell(2, 3).Value = "10.1.1.10";
        ws.Cell(2, 4).Value = 36.2900;
        ws.Cell(2, 5).Value = 33.5100;
        ws.Cell(2, 6).Value = 45;
        ws.Cell(2, 7).Value = 90;
        ws.Cell(2, 8).Value = 5;
        ws.Cell(2, 9).Value = 18;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using MemoryStream ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<SectorExcelImportParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        int networkId,
        CancellationToken ct = default)
    {
        if (fileStream == null || !fileStream.CanRead)
        {
            return SectorExcelImportParseResult.Fail("الملف غير صالح.");
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
            logger.LogWarning(ex, "تعذر قراءة ملف استيراد المرسلات {FileName}", fileName);
            return SectorExcelImportParseResult.Fail(ex.Message);
        }

        if (rows.Count == 0)
        {
            return SectorExcelImportParseResult.Fail(
                (sheetNote != null ? sheetNote + " " : string.Empty) +
                "الملف لا يحتوي على صفوف بيانات.");
        }

        if (rows.Count > MaxImportRows)
        {
            return SectorExcelImportParseResult.Fail(
                $"عدد الصفوف أكبر من الحد المسموح ({MaxImportRows}). قسّم الملف ثم أعد المحاولة.");
        }

        List<MikroTikServer> servers = await Db.MikroTikServers
            .AsNoTracking()
            .Where(s => s.NetworkId == networkId)
            .Select(s => new MikroTikServer { Id = s.Id, Name = s.Name, Host = s.Host })
            .ToListAsync(ct);

        if (servers.Count == 0)
        {
            return SectorExcelImportParseResult.Fail("لا يوجد خادم MikroTik في الشبكة الحالية لربط المرسلات به.");
        }

        var existing = await Db.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId == networkId)
            .Select(s => new { s.Name, s.IPAddress })
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
        List<Sector> sectors = [];
        int rowNumber = 1;

        foreach (Dictionary<string, string> row in rows)
        {
            rowNumber++;
            string? name = GetField(row, NameHeaders, exactPreferred: true);
            string? serverName = GetField(row, ServerHeaders, exactPreferred: true);
            string? ipRaw = GetField(row, IpHeaders, exactPreferred: true);
            string? longitudeRaw = GetField(row, LongitudeHeaders, exactPreferred: true);
            string? latitudeRaw = GetField(row, LatitudeHeaders, exactPreferred: true);
            string? directionRaw = GetField(row, DirectionHeaders, exactPreferred: true);
            string? angleRaw = GetField(row, AngleHeaders, exactPreferred: true);
            string? rangeRaw = GetField(row, RangeHeaders, exactPreferred: true);
            string? antennaRaw = GetField(row, AntennaHeaders, exactPreferred: true);

            if (IsCompletelyEmpty(name, serverName, ipRaw, longitudeRaw, latitudeRaw, directionRaw, angleRaw, rangeRaw, antennaRaw))
            {
                skipped++;
                continue;
            }

            List<string> rowErrors = [];
            if (string.IsNullOrWhiteSpace(name))
            {
                rowErrors.Add("اسم المرسل مطلوب");
            }
            else if (name.Trim().Length > 100)
            {
                rowErrors.Add("اسم المرسل أطول من 100 حرف");
            }

            MikroTikServer? server = ResolveServer(servers, serverName);
            if (server == null)
            {
                rowErrors.Add(string.IsNullOrWhiteSpace(serverName)
                    ? "اسم الخادم مطلوب"
                    : $"لا يوجد خادم باسم «{serverName.Trim()}» في الشبكة الحالية");
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

            if (!TryParseDouble(directionRaw, out double direction) || direction < 0 || direction > 360)
            {
                rowErrors.Add("الاتجاه يجب أن يكون رقماً بين 0 و 360");
            }

            if (!TryParseDouble(angleRaw, out double angle) || angle < 0 || angle > 360)
            {
                rowErrors.Add("الزاوية يجب أن تكون رقماً بين 0 و 360");
            }

            if (!TryParseDouble(rangeRaw, out double range) || range < 0.1 || range > 1000)
            {
                rowErrors.Add("المدى يجب أن يكون رقماً بين 0.1 و 1000 كم");
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
                details.Add($"صف {rowNumber}: المرسل «{name.Trim()}» موجود مسبقاً — تم التخطي.");
                continue;
            }

            if (existingIps.Contains(ip))
            {
                skipped++;
                details.Add($"صف {rowNumber}: عنوان IP «{ip}» مستخدم مسبقاً — تم التخطي.");
                continue;
            }

            Sector sector = new Sector
            {
                Name = name.Trim(),
                IPAddress = ip,
                NetworkMask = mask,
                Latitude = latitude,
                Longitude = longitude,
                Direction = direction,
                CoverageAngle = angle,
                CoverageRange = range,
                AntennaHeightAglMeters = antennaHeight,
                MikroTikServerId = server!.Id,
                NetworkId = networkId,
                IsActive = true,
                CreatedDate = DateTime.Now,
                RadioInterfaceName = name.Trim()
            };

            sectors.Add(sector);
            existingNames.Add(nameKey);
            existingIps.Add(ip);
        }

        string message = BuildParseMessage(sectors.Count, skipped, failed, sheetNote);
        return SectorExcelImportParseResult.FromRows(message, rows.Count, skipped, failed, sectors, details);
    }

    public async Task<SectorExcelImportResult> CommitAsync(
        IReadOnlyList<Sector> sectors,
        CancellationToken ct = default)
    {
        if (sectors == null || sectors.Count == 0)
        {
            return SectorExcelImportResult.Fail("لا توجد مرسلات صالحة للحفظ.");
        }

        Db.Sectors.AddRange(sectors);
        await Db.SaveChangesAsync(ct);
        return SectorExcelImportResult.Ok(
            $"تم إضافة {sectors.Count} مرسل من الملف.",
            sectors.Count,
            0,
            0,
            []);
    }

    private static string BuildParseMessage(int importable, int skipped, int failed, string? sheetNote)
    {
        string note = string.IsNullOrWhiteSpace(sheetNote) ? string.Empty : sheetNote.Trim() + " ";
        if (importable == 0)
        {
            return note + $"لا توجد مرسلات جديدة صالحة للاستيراد. تم تخطي {skipped} وفشل {failed}.";
        }

        return note + $"جاهز لإضافة {importable} مرسل. تم تخطي {skipped} وفشل التحقق من {failed}.";
    }

    private static bool IsCompletelyEmpty(params string?[] values) =>
        values.All(string.IsNullOrWhiteSpace);

    private static MikroTikServer? ResolveServer(List<MikroTikServer> servers, string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return null;
        }

        string key = rawName.Trim();
        MikroTikServer? exact = servers.FirstOrDefault(s =>
            string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        MikroTikServer? byHost = servers.FirstOrDefault(s =>
            string.Equals(s.Host, key, StringComparison.OrdinalIgnoreCase));
        return byHost;
    }

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
        return string.Join(".",
            (bits >> 24) & 255,
            (bits >> 16) & 255,
            (bits >> 8) & 255,
            bits & 255);
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
            if (matched == null)
            {
                worksheet = sheets[0];
                sheetNote = $"الملف يحتوي {sheets.Count} أوراق؛ تم استخدام الورقة الأولى «{worksheet.Name}».";
            }
            else
            {
                worksheet = matched;
                sheetNote = $"تم اختيار الورقة «{worksheet.Name}».";
            }
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

        string value = header.Trim().ToLowerInvariant();
        value = value.Replace('\u0640', ' ');
        value = Regex.Replace(value, @"\s+", " ");
        return value;
    }

    private static string? GetField(Dictionary<string, string> row, string[] aliases, bool exactPreferred)
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
                if (header == a ||
                    header.StartsWith(a + " ", StringComparison.OrdinalIgnoreCase) ||
                    (!exactPreferred && a.Length >= 4 && header.Contains(a, StringComparison.OrdinalIgnoreCase)))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }
}
