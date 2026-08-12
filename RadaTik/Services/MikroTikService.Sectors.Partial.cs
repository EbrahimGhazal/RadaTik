using Microsoft.EntityFrameworkCore;
using RadaTik.Models;
using tik4net;

namespace RadaTik.Services;

public partial class MikroTikService
{
    private sealed record SectorNameIpSnapshot(string? Name, string? IPAddress);

    /// <summary>
    /// معاينة استيراد القطاعات من MikroTik قبل التنفيذ
    /// </summary>
    public async Task<ImportSectorsPreviewResult> BuildSectorsImportPreviewAsync(int serverId, int networkId)
    {
        ImportSectorsPreviewResult preview = new ImportSectorsPreviewResult();

        MikroTikServer? server = await _context.MikroTikServers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId);
        if (server == null)
        {
            return preview;
        }

        (HashSet<string> existingNames, HashSet<string> existingIps) = await LoadExistingSectorKeysAsync(serverId, networkId);

        try
        {
            await ExecuteWithRetry(server, connection =>
            {
                PopulateSectorImportPreview(connection, preview, existingNames, existingIps);
                return 0;
            });
        }
        catch (TikNoSuchCommandException ex)
        {
            _logger.LogWarning(ex, "Unsupported radio interface command while building sectors import preview for server {ServerId}", serverId);
            preview.IsRadioInterfaceCommandUnsupported = true;
            preview.PreviewNote = $"هذا السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2). آخر خطأ: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build sectors import preview for server {ServerId}", serverId);
            preview.PreviewNote = BuildSectorImportPreviewErrorMessage(ex);
        }

        return preview;
    }

    /// <summary>
    /// استيراد القطاعات المرتبطة بواجهات wireless من MikroTik
    /// </summary>
    public async Task<ImportSectorsResult> ImportSectorsFromMikroTikAsync(int serverId, int networkId)
    {
        ImportSectorsResult result = new ImportSectorsResult();

        MikroTikServer? server = await _context.MikroTikServers
            .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId);
        if (server == null)
        {
            result.Success = false;
            result.Message = "الخادم غير موجود ضمن الشبكة الحالية.";
            return result;
        }

        (HashSet<string> existingNames, HashSet<string> existingIps) = await LoadExistingSectorKeysAsync(serverId, networkId);

        try
        {
            (List<ITikReSentence> interfaceRows, Dictionary<string, List<string>> ipByInterface) =
                await ExecuteWithRetry(server, connection =>
                {
                    (List<ITikReSentence> rows, bool isSupported, string? lastFailedCommand) radioInterfaces =
                        ExecuteRadioInterfacesPrintWithFallback(connection);
                    if (!radioInterfaces.isSupported)
                    {
                        string lastFailed = string.IsNullOrWhiteSpace(radioInterfaces.lastFailedCommand)
                            ? "غير محدد"
                            : radioInterfaces.lastFailedCommand;
                        throw new InvalidOperationException(
                            $"السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2/interface print). آخر مسار فشل: {lastFailed}.");
                    }

                    Dictionary<string, List<string>> ipMap =
                        BuildIpAddressByInterface(connection.CreateCommand("/ip/address/print").ExecuteList());
                    return (radioInterfaces.rows, ipMap);
                });

            foreach (ITikReSentence iface in interfaceRows)
            {
                string interfaceName = GetSafeValue(iface, "name");
                if (string.IsNullOrWhiteSpace(interfaceName))
                {
                    continue;
                }

                result.TotalOnServer++;
                string interfaceKey = interfaceName.Trim().ToLowerInvariant();
                if (existingNames.Contains(interfaceKey))
                {
                    result.SkippedExisting++;
                    continue;
                }

                (string ip, string mask)? firstValid = ResolveInterfaceAddress(ipByInterface, interfaceName);
                if (!firstValid.HasValue)
                {
                    result.SkippedMissingIp++;
                    result.Errors.Add($"الواجهة {interfaceName}: لا يوجد IP صالح مرتبط بها.");
                    continue;
                }

                (string ip, string mask) parsed = firstValid.Value;
                if (existingIps.Contains(parsed.ip))
                {
                    result.SkippedExisting++;
                    continue;
                }

                Sector sector = new Sector
                {
                    Name = interfaceName,
                    IPAddress = parsed.ip,
                    NetworkMask = parsed.mask,
                    MikroTikServerId = serverId,
                    NetworkId = networkId,
                    IsActive = !string.Equals(GetSafeValue(iface, "disabled"), "true", StringComparison.OrdinalIgnoreCase),
                    Latitude = 0,
                    Longitude = 0,
                    Direction = 0,
                    CoverageAngle = 60,
                    CoverageRange = 1,
                    RadioInterfaceName = interfaceName
                };

                _context.Sectors.Add(sector);
                existingNames.Add(interfaceKey);
                existingIps.Add(parsed.ip);
                result.AddedCount++;
            }

            await _context.SaveChangesAsync();

            result.Success = true;
            result.Message = $"تم استيراد {result.AddedCount} قطاع، تم تخطي {result.SkippedExisting} موجود مسبقاً، و{result.SkippedMissingIp} بدون IP.";
            return result;
        }
        catch (TikNoSuchCommandException ex)
        {
            _logger.LogWarning(ex, "Unsupported radio interface command while importing sectors for server {ServerId}", serverId);
            result.Success = false;
            result.Message = "السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2).";
            result.Errors.Add(ex.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import sectors from MikroTik for server {ServerId}", serverId);
            result.Success = false;
            result.Message = BuildSectorImportPreviewErrorMessage(ex);
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    private async Task<(HashSet<string> Names, HashSet<string> Ips)> LoadExistingSectorKeysAsync(int serverId, int networkId)
    {
        List<SectorNameIpSnapshot> existingSectors = await _context.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId == networkId && s.MikroTikServerId == serverId)
            .Select(s => new SectorNameIpSnapshot(s.Name, s.IPAddress))
            .ToListAsync();

        HashSet<string> existingNames = existingSectors
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name!.Trim().ToLowerInvariant())
            .ToHashSet();

        HashSet<string> existingIps = existingSectors
            .Where(x => !string.IsNullOrWhiteSpace(x.IPAddress))
            .Select(x => x.IPAddress!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (existingNames, existingIps);
    }

    private void PopulateSectorImportPreview(
        ITikConnection connection,
        ImportSectorsPreviewResult preview,
        HashSet<string> existingNames,
        HashSet<string> existingIps)
    {
        (List<ITikReSentence> rows, bool isSupported, string? lastFailedCommand) radioInterfaces =
            ExecuteRadioInterfacesPrintWithFallback(connection);
        if (!radioInterfaces.isSupported)
        {
            preview.IsRadioInterfaceCommandUnsupported = true;
            string lastFailed = string.IsNullOrWhiteSpace(radioInterfaces.lastFailedCommand)
                ? "غير محدد"
                : radioInterfaces.lastFailedCommand;
            preview.PreviewNote =
                $"هذا السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2/interface print). آخر مسار فشل: {lastFailed}.";
            return;
        }

        List<ITikReSentence> interfaceRows = radioInterfaces.rows;
        Dictionary<string, List<string>> ipByInterface =
            BuildIpAddressByInterface(connection.CreateCommand("/ip/address/print").ExecuteList());

        preview.TotalInterfacesOnServer = interfaceRows.Count;

        foreach (ITikReSentence iface in interfaceRows)
        {
            string interfaceName = GetSafeValue(iface, "name");
            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                preview.InvalidInterfacesCount++;
                continue;
            }

            if (existingNames.Contains(interfaceName.Trim().ToLowerInvariant()))
            {
                preview.ExistingSectorsCount++;
                continue;
            }

            (string ip, string mask)? parsed = ResolveInterfaceAddress(ipByInterface, interfaceName);
            if (!parsed.HasValue)
            {
                preview.MissingIpCount++;
                continue;
            }

            if (existingIps.Contains(parsed.Value.ip))
            {
                preview.ExistingSectorsCount++;
                continue;
            }

            preview.ImportableSectorsCount++;
        }
    }

    private static Dictionary<string, List<string>> BuildIpAddressByInterface(IEnumerable<ITikReSentence> ipRows)
    {
        Dictionary<string, List<string>> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (ITikReSentence row in ipRows)
        {
            string iface = GetSafeValue(row, "interface").Trim();
            string address = GetSafeValue(row, "address").Trim();
            if (string.IsNullOrWhiteSpace(iface) || string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            if (!map.TryGetValue(iface, out List<string>? addresses))
            {
                addresses = new List<string>();
                map[iface] = addresses;
            }

            addresses.Add(address);
        }

        return map;
    }

    private static (string ip, string mask)? ResolveInterfaceAddress(
        Dictionary<string, List<string>> ipByInterface,
        string interfaceName)
    {
        if (ipByInterface.TryGetValue(interfaceName.Trim(), out List<string>? cidrs))
        {
            (string ip, string mask)? parsed = cidrs
                .Select(TryParseCidr)
                .FirstOrDefault(x => x.HasValue);
            if (parsed.HasValue)
            {
                return parsed;
            }
        }

        string normalized = interfaceName.Trim();
        int slashIndex = normalized.IndexOf('/');
        if (slashIndex > 0)
        {
            normalized = normalized[..slashIndex];
        }

        if (!string.Equals(normalized, interfaceName.Trim(), StringComparison.Ordinal)
            && ipByInterface.TryGetValue(normalized, out List<string>? aliasCidrs))
        {
            return aliasCidrs
                .Select(TryParseCidr)
                .FirstOrDefault(x => x.HasValue);
        }

        return null;
    }

    private static string BuildSectorImportPreviewErrorMessage(Exception ex)
    {
        string message = ex.InnerException?.Message ?? ex.Message ?? string.Empty;
        string combined = $"{ex.Message} {message}".Trim();

        if (combined.Contains("cannot connect", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("refused", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("No connection", StringComparison.OrdinalIgnoreCase))
        {
            return "تعذر الاتصال بالسيرفر — تحقق من عنوان IP والمنفذ (8728) وأن خدمة API مفعّلة وأن الجدار الناري يسمح بالوصول.";
        }

        if (combined.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return "انتهت مهلة الاتصال بالسيرفر — تحقق من الشبكة بين RadaTik و MikroTik.";
        }

        if (combined.Contains("password", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("invalid user", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("login", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("denied", StringComparison.OrdinalIgnoreCase))
        {
            return "فشل تسجيل الدخول إلى MikroTik — تحقق من اسم المستخدم وكلمة المرور وصلاحيات API للمستخدم.";
        }

        if (combined.Contains("no such command", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("not enough permissions", StringComparison.OrdinalIgnoreCase))
        {
            return "المستخدم لا يملك صلاحية قراءة الواجهات أو العناوين — امنحه policy read/write على API.";
        }

        if (ex is InvalidOperationException && combined.StartsWith("فشل الاتصال", StringComparison.Ordinal))
        {
            return combined;
        }

        if (ex is InvalidOperationException && combined.Contains("لا يدعم أوامر", StringComparison.Ordinal))
        {
            return combined;
        }

        if (!string.IsNullOrWhiteSpace(combined) && combined.Length <= 180)
        {
            return $"تعذرت معاينة الاستيراد: {combined}";
        }

        return "تعذرت معاينة الاستيراد حالياً لهذا السيرفر. تحقق من الاتصال وصلاحيات API ثم أعد المحاولة.";
    }

    private (List<ITikReSentence> rows, bool isSupported, string? lastFailedCommand) ExecuteRadioInterfacesPrintWithFallback(ITikConnection connection)
    {
        string[] commands = new[]
        {
            "/interface/wireless/print",
            "/interface/wifi/print",
            "/interface/wifiwave2/print"
        };
        string? lastFailedCommand = null;

        foreach (string? command in commands)
        {
            try
            {
                List<ITikReSentence> rows = connection.CreateCommand(command).ExecuteList().ToList();
                if (rows.Count > 0)
                {
                    return (rows, true, null);
                }
            }
            catch (TikNoSuchCommandException ex)
            {
                lastFailedCommand = command;
                _logger.LogDebug(ex, "Radio interface command not supported: {Command}", command);
            }
        }

        try
        {
            List<ITikReSentence> wirelessFromGenericPrint = connection.CreateCommand("/interface/print")
                .ExecuteList()
                .Where(IsWirelessLikeInterface)
                .ToList();
            if (wirelessFromGenericPrint.Count > 0)
            {
                return (wirelessFromGenericPrint, true, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Generic interface/print fallback failed while listing wireless interfaces");
            lastFailedCommand ??= "/interface/print";
        }

        return (new List<ITikReSentence>(), false, lastFailedCommand);
    }

    private static bool IsWirelessLikeInterface(ITikReSentence row)
    {
        string type = GetSafeValue(row, "type").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(type))
        {
            if (type.Contains("wlan") || type.Contains("wifi") || type is "cap")
            {
                return true;
            }

            if (type is "ether" or "vlan" or "bridge" or "loopback" or "ovpn" or "pppoe" or "pptp" or "sstp" or "l2tp" or "gre" or "ipip" or "bonding" or "vrrp" or "vxlan" or "lte")
            {
                return false;
            }
        }

        string name = GetSafeValue(row, "name").Trim().ToLowerInvariant();
        return name.StartsWith("wlan", StringComparison.Ordinal)
            || name.StartsWith("wifi", StringComparison.Ordinal)
            || name.Contains("wireless", StringComparison.Ordinal);
    }
}
