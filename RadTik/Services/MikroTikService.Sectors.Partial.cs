using Microsoft.EntityFrameworkCore;
using RadTik.Models;
using tik4net;

namespace RadTik.Services;

public partial class MikroTikService
{
    /// <summary>
    /// معاينة استيراد القطاعات من MikroTik قبل التنفيذ
    /// </summary>
    public async Task<ImportSectorsPreviewResult> BuildSectorsImportPreviewAsync(int serverId, int networkId)
    {
        var preview = new ImportSectorsPreviewResult();

        var server = await _context.MikroTikServers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId);
        if (server == null)
        {
            return preview;
        }

        try
        {
            using var connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass);

            var radioInterfaces = ExecuteRadioInterfacesPrintWithFallback(connection);
            if (!radioInterfaces.isSupported)
            {
                preview.IsRadioInterfaceCommandUnsupported = true;
                var lastFailed = string.IsNullOrWhiteSpace(radioInterfaces.lastFailedCommand)
                    ? "غير محدد"
                    : radioInterfaces.lastFailedCommand;
                preview.PreviewNote = $"هذا السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2). آخر مسار فشل: {lastFailed}.";
                return preview;
            }
            var interfaceRows = radioInterfaces.rows;
            var ipRows = connection.CreateCommand("/ip/address/print").ExecuteList();

            preview.TotalInterfacesOnServer = interfaceRows.Count();

            var ipByInterface = ipRows
                .GroupBy(r => GetSafeValue(r, "interface"))
                .ToDictionary(g => g.Key, g => g.Select(r => GetSafeValue(r, "address")).ToList());

            var existingSectors = await _context.Sectors
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId && s.MikroTikServerId == serverId)
                .Select(s => new { s.Name, s.IPAddress })
                .ToListAsync();

            var existingNames = existingSectors
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!.Trim().ToLowerInvariant())
                .ToHashSet();

            var existingIps = existingSectors
                .Where(x => !string.IsNullOrWhiteSpace(x.IPAddress))
                .Select(x => x.IPAddress!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var iface in interfaceRows)
            {
                var interfaceName = GetSafeValue(iface, "name");
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

                ipByInterface.TryGetValue(interfaceName, out var cidrs);
                var parsed = cidrs?
                    .Select(c => TryParseCidr(c))
                    .FirstOrDefault(x => x.HasValue);

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
        catch (tik4net.TikNoSuchCommandException ex)
        {
            _logger.LogWarning(ex, "Unsupported radio interface command while building sectors import preview for server {ServerId}", serverId);
            preview.IsRadioInterfaceCommandUnsupported = true;
            preview.PreviewNote = $"هذا السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2). آخر خطأ: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build sectors import preview for server {ServerId}", serverId);
            preview.PreviewNote = "تعذرت معاينة الاستيراد حالياً لهذا السيرفر. تحقق من الاتصال وصلاحيات API ثم أعد المحاولة.";
        }

        return preview;
    }

    /// <summary>
    /// استيراد القطاعات المرتبطة بواجهات wireless من MikroTik
    /// </summary>
    public async Task<ImportSectorsResult> ImportSectorsFromMikroTikAsync(int serverId, int networkId)
    {
        var result = new ImportSectorsResult();

        var server = await _context.MikroTikServers
            .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId);
        if (server == null)
        {
            result.Success = false;
            result.Message = "الخادم غير موجود ضمن الشبكة الحالية.";
            return result;
        }

        try
        {
            using var connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass);

            var radioInterfaces = ExecuteRadioInterfacesPrintWithFallback(connection);
            if (!radioInterfaces.isSupported)
            {
                result.Success = false;
                var lastFailed = string.IsNullOrWhiteSpace(radioInterfaces.lastFailedCommand)
                    ? "غير محدد"
                    : radioInterfaces.lastFailedCommand;
                result.Message = $"السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2). آخر مسار فشل: {lastFailed}.";
                return result;
            }
            var interfaceRows = radioInterfaces.rows;
            var ipRows = connection.CreateCommand("/ip/address/print").ExecuteList();

            var ipByInterface = ipRows
                .GroupBy(r => GetSafeValue(r, "interface"))
                .ToDictionary(g => g.Key, g => g.Select(r => GetSafeValue(r, "address")).ToList());

            var existingSectors = await _context.Sectors
                .Where(s => s.NetworkId == networkId && s.MikroTikServerId == serverId)
                .Select(s => new { s.Name, s.IPAddress })
                .ToListAsync();

            var existingNames = existingSectors
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!.Trim().ToLowerInvariant())
                .ToHashSet();

            var existingIps = existingSectors
                .Where(x => !string.IsNullOrWhiteSpace(x.IPAddress))
                .Select(x => x.IPAddress!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var iface in interfaceRows)
            {
                var interfaceName = GetSafeValue(iface, "name");
                if (string.IsNullOrWhiteSpace(interfaceName))
                {
                    continue;
                }

                result.TotalOnServer++;
                var interfaceKey = interfaceName.Trim().ToLowerInvariant();
                if (existingNames.Contains(interfaceKey))
                {
                    result.SkippedExisting++;
                    continue;
                }

                ipByInterface.TryGetValue(interfaceName, out var cidrs);
                var firstValid = cidrs?
                    .Select(c => TryParseCidr(c))
                    .FirstOrDefault(x => x.HasValue);

                if (!firstValid.HasValue)
                {
                    result.SkippedMissingIp++;
                    result.Errors.Add($"الواجهة {interfaceName}: لا يوجد IP صالح مرتبط بها.");
                    continue;
                }

                var parsed = firstValid.Value;
                if (existingIps.Contains(parsed.ip))
                {
                    result.SkippedExisting++;
                    continue;
                }

                var sector = new Sector
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
        catch (tik4net.TikNoSuchCommandException ex)
        {
            _logger.LogWarning(ex, "Unsupported radio interface command while importing sectors for server {ServerId}", serverId);
            result.Success = false;
            result.Message = "السيرفر لا يدعم أوامر واجهات الراديو المعروفة (wireless/wifi/wifiwave2).";
            result.Errors.Add(ex.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ فشل استيراد القطاعات من MikroTik للخادم {ServerId}", serverId);
            result.Success = false;
            result.Message = "فشل الاتصال بالسيرفر أو قراءة بيانات القطاعات.";
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    private (List<ITikReSentence> rows, bool isSupported, string? lastFailedCommand) ExecuteRadioInterfacesPrintWithFallback(ITikConnection connection)
    {
        var commands = new[]
        {
            "/interface/wireless/print",
            "/interface/wifi/print",
            "/interface/wifiwave2/print"
        };
        string? lastFailedCommand = null;

        foreach (var command in commands)
        {
            try
            {
                var rows = connection.CreateCommand(command).ExecuteList().ToList();
                return (rows, true, null);
            }
            catch (tik4net.TikNoSuchCommandException ex)
            {
                lastFailedCommand = command;
                _logger.LogDebug(ex, "Radio interface command not supported: {Command}", command);
            }
        }

        return (new List<ITikReSentence>(), false, lastFailedCommand);
    }
}
