using RadTik.Models;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using tik4net;

namespace RadTik.Services;

public partial class MikroTikService
{
    /// <summary>
    /// إنشاء اتصال مع MikroTik مع إعادة المحاولة ومعالجة الأخطاء
    /// </summary>
    private ITikConnection CreateConnectionWithRetry(MikroTikServer server, int maxRetries = 3)
    {
        ITikConnection? connection = null;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation($"🔗 محاولة الاتصال بالخادم {server.Host}:{server.Port} (المحاولة {attempt}/{maxRetries})");

                connection = ConnectionFactory.OpenConnection(
                    TikConnectionType.Api,
                    server.Host,
                    server.Port,
                    server.User,
                    server.Pass);

                var testCmd = connection.CreateCommand("/system/resource/print");
                testCmd.ExecuteList();

                _logger.LogInformation($"✅ تم إنشاء الاتصال بنجاح في المحاولة {attempt}");
                return connection;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning($"⚠️ فشلت محاولة الاتصال {attempt}/{maxRetries}: {ex.Message}");

                try
                {
                    connection?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogDebug(disposeEx, "Failed to dispose failed MikroTik connection on retry.");
                }

                connection = null;

                if (attempt < maxRetries)
                {
                    int delay = (int)Math.Pow(2, attempt) * 500;
                    _logger.LogInformation($"⏳ الانتظار {delay}ms قبل المحاولة التالية...");
                    Thread.Sleep(delay);
                }
            }
        }

        _logger.LogError($"❌ فشل الاتصال بالخادم بعد {maxRetries} محاولات");
        throw new InvalidOperationException($"فشل الاتصال بالخادم {server.Host} بعد {maxRetries} محاولات", lastException);
    }

    /// <summary>
    /// تنفيذ أمر في MikroTik مع إعادة المحاولة
    /// </summary>
    private async Task<T> ExecuteWithRetry<T>(MikroTikServer server, Func<ITikConnection, T> operation, int maxRetries = 2)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            ITikConnection? connection = null;
            try
            {
                connection = CreateConnectionWithRetry(server, maxRetries: 3);
                var result = operation(connection);
                connection.Dispose();
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning($"⚠️ فشلت العملية في المحاولة {attempt}/{maxRetries}: {ex.Message}");

                try
                {
                    connection?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogDebug(disposeEx, "Failed to dispose MikroTik connection after operation failure.");
                }

                if (attempt < maxRetries && (
                    ex.Message.Contains("connection") ||
                    ex.Message.Contains("transport") ||
                    ex.Message.Contains("forcibly closed") ||
                    ex.Message.Contains("timeout")))
                {
                    int delay = (int)Math.Pow(2, attempt) * 1000;
                    _logger.LogInformation($"⏳ الانتظار {delay}ms قبل إعادة المحاولة...");
                    await Task.Delay(delay);
                    continue;
                }

                throw;
            }
        }

        throw new InvalidOperationException($"فشلت العملية بعد {maxRetries} محاولات", lastException);
    }

    private string GenerateUniqueSID() => DateTime.Now.Ticks.ToString()[^10..];

    private string GenerateDefaultPassword() => Guid.NewGuid().ToString()[..8];

    private string ConvertToMikroTikDate(DateTime date)
    {
        string[] monthNames = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
        string month = monthNames[date.Month - 1];
        return $"{month}/{date.Day:D2}/{date.Year}";
    }

    private string? ConvertToMikroTikRateLimit(Profile profile)
    {
        if (profile.DownloadSpeed <= 0) return null;
        string downloadLimit = SpeedToMikroTikFormat(profile.DownloadSpeed, profile.DownloadSpeedUnit);
        var uploadUnit = profile.UploadSpeedUnit ?? profile.DownloadSpeedUnit;
        string uploadLimit = profile.UploadSpeed.HasValue && profile.UploadSpeed > 0
            ? SpeedToMikroTikFormat(profile.UploadSpeed.Value, uploadUnit)
            : downloadLimit;
        return $"{downloadLimit}/{uploadLimit}";
    }

    private static string SpeedToMikroTikFormat(int value, SpeedUnit unit) => unit switch
    {
        SpeedUnit.Kbps => $"{value}k",
        SpeedUnit.Mbps => $"{value}M",
        SpeedUnit.Gbps => $"{value}G",
        _ => $"{value}M"
    };

    private decimal ParseSpeedFromRateLimit(string rateLimit, bool isDownload)
    {
        if (string.IsNullOrEmpty(rateLimit)) return 10;
        try
        {
            var parts = rateLimit.Split('/');
            if (parts.Length == 2)
            {
                var speedPart = isDownload ? parts[0] : parts[1];
                return ParseMikroTikSpeed(speedPart);
            }
            else if (parts.Length == 1)
            {
                return ParseMikroTikSpeed(parts[0]);
            }
        }
        catch { }
        return 10;
    }

    private (int Value, SpeedUnit Unit) ParseSpeedFromRateLimitToIntUnit(string? rateLimit, bool isDownload)
    {
        if (string.IsNullOrEmpty(rateLimit)) return (10, SpeedUnit.Mbps);
        try
        {
            var parts = rateLimit.Split('/');
            var speedPart = parts.Length >= 2 ? (isDownload ? parts[0] : parts[1]) : parts[0];
            return ParseMikroTikSpeedToIntUnit(speedPart);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse MikroTik speed unit from rate limit.");
        }
        return (10, SpeedUnit.Mbps);
    }

    private (int Value, SpeedUnit Unit) ParseMikroTikSpeedToIntUnit(string speed)
    {
        if (string.IsNullOrEmpty(speed)) return (10, SpeedUnit.Mbps);
        speed = speed.Trim();
        var match = Regex.Match(speed, @"([\d\.]+)");
        if (!match.Success) return (10, SpeedUnit.Mbps);
        var value = (int)decimal.Parse(match.Value);
        if (speed.EndsWith("k", StringComparison.OrdinalIgnoreCase)) return (value, SpeedUnit.Kbps);
        if (speed.EndsWith("G", StringComparison.OrdinalIgnoreCase)) return (value, SpeedUnit.Gbps);
        return (value, SpeedUnit.Mbps);
    }

    private decimal ParseMikroTikSpeed(string speed)
    {
        if (string.IsNullOrEmpty(speed)) return 0;
        speed = speed.Trim();
        var match = Regex.Match(speed, @"([\d\.]+)");
        if (!match.Success) return 0;
        decimal value = decimal.Parse(match.Value);
        if (speed.EndsWith("k", StringComparison.OrdinalIgnoreCase)) return value / 1000;
        if (speed.EndsWith("G", StringComparison.OrdinalIgnoreCase)) return value * 1000;
        return value;
    }

    private ProfileType GetProfileTypeFromService(string? service) => service?.ToLower() switch
    {
        "pptp" => ProfileType.IPTV,
        "l2tp" => ProfileType.VoIP,
        _ => ProfileType.Internet
    };

    private async Task DisableExpiredAccount(string username, int serverId)
    {
        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null) throw new InvalidOperationException("الخادم غير موجود");

        try
        {
            using (var connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass))
            {
                var findCmd = connection.CreateCommand("/ppp/secret/print");
                var allUsers = findCmd.ExecuteList();
                var targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == username);
                if (targetUser != null)
                {
                    var userId = GetSafeValue(targetUser, ".id");
                    var setCmd = connection.CreateCommand("/ppp/secret/set");
                    setCmd.AddParameter(".id", userId);
                    setCmd.AddParameter("disabled", "yes");
                    setCmd.ExecuteNonQuery();
                    _logger.LogInformation($"✅ تم إيقاف الحساب {username} في MikroTik");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في إيقاف الحساب {username} في MikroTik: {ex.Message}");
            throw;
        }
    }

    private string GetSafeValue(ITikReSentence row, string key) => row.Words.ContainsKey(key) ? row.Words[key] : "";

    private static (string ip, string mask)? TryParseCidr(string? cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr)) return null;
        var parts = cidr.Split('/');
        if (parts.Length != 2) return null;
        if (!IPAddress.TryParse(parts[0], out var ipAddress)) return null;
        var ip = ipAddress.ToString();
        if (ip.Contains(':')) return null;
        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32) return null;
        return (ip, PrefixToMask(prefix));
    }

    private static string PrefixToMask(int prefix)
    {
        uint mask = prefix == 0 ? 0u : 0xffffffffu << (32 - prefix);
        return string.Join(".",
            (mask >> 24) & 255,
            (mask >> 16) & 255,
            (mask >> 8) & 255,
            mask & 255);
    }
}
