using RadaTik.Models;
using RadaTik.Services.MikroTik;
using System.Net;
using System.Text.RegularExpressions;
using tik4net;

namespace RadaTik.Services;

public partial class MikroTikService
{
    private ITikConnection CreateConnectionWithRetry(MikroTikServer server, int maxRetries = 3) =>
        _connection.CreateConnectionWithRetry(server, maxRetries);

    private Task<T> ExecuteWithRetry<T>(MikroTikServer server, Func<ITikConnection, T> operation, int maxRetries = 2) =>
        _connection.ExecuteWithRetry(server, operation, maxRetries);

    private string GenerateUniqueSID() => DateTime.Now.Ticks.ToString()[^10..];

    private static string GenerateDefaultPassword() => MikroTikApiSupport.GenerateDefaultPassword();

    private string ConvertToMikroTikDate(DateTime date)
    {
        string[] monthNames = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
        string month = monthNames[date.Month - 1];
        return $"{month}/{date.Day:D2}/{date.Year}";
    }

    private string? ConvertToMikroTikRateLimit(Profile profile)
    {
        if (profile.DownloadSpeed <= 0)
        {
            return null;
        }

        string downloadLimit = SpeedToMikroTikFormat(profile.DownloadSpeed, profile.DownloadSpeedUnit);
        SpeedUnit uploadUnit = profile.UploadSpeedUnit ?? profile.DownloadSpeedUnit;
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
        if (string.IsNullOrEmpty(rateLimit))
        {
            return 10;
        }

        try
        {
            string[] parts = rateLimit.Split('/');
            if (parts.Length == 2)
            {
                string speedPart = isDownload ? parts[0] : parts[1];
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
        if (string.IsNullOrEmpty(rateLimit))
        {
            return (10, SpeedUnit.Mbps);
        }

        try
        {
            string[] parts = rateLimit.Split('/');
            string speedPart = parts.Length >= 2 ? (isDownload ? parts[0] : parts[1]) : parts[0];
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
        if (string.IsNullOrEmpty(speed))
        {
            return (10, SpeedUnit.Mbps);
        }

        speed = speed.Trim();
        Match match = Regex.Match(speed, @"([\d\.]+)");
        if (!match.Success)
        {
            return (10, SpeedUnit.Mbps);
        }

        int value = (int)decimal.Parse(match.Value);
        if (speed.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            return (value, SpeedUnit.Kbps);
        }

        if (speed.EndsWith("G", StringComparison.OrdinalIgnoreCase))
        {
            return (value, SpeedUnit.Gbps);
        }

        return (value, SpeedUnit.Mbps);
    }

    private decimal ParseMikroTikSpeed(string speed)
    {
        if (string.IsNullOrEmpty(speed))
        {
            return 0;
        }

        speed = speed.Trim();
        Match match = Regex.Match(speed, @"([\d\.]+)");
        if (!match.Success)
        {
            return 0;
        }

        decimal value = decimal.Parse(match.Value);
        if (speed.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            return value / 1000;
        }

        if (speed.EndsWith("G", StringComparison.OrdinalIgnoreCase))
        {
            return value * 1000;
        }

        return value;
    }

    private ProfileType GetProfileTypeFromService(string? service) => service?.ToLower() switch
    {
        "pptp" => ProfileType.IPTV,
        "l2tp" => ProfileType.VoIP,
        _ => ProfileType.Internet
    };

    private static string GetSafeValue(ITikReSentence row, string key) => MikroTikApiSupport.GetSafeValue(row, key);

    private static (string ip, string mask)? TryParseCidr(string? cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
        {
            return null;
        }

        string[] parts = cidr.Split('/');
        if (parts.Length != 2)
        {
            return null;
        }

        if (!IPAddress.TryParse(parts[0], out IPAddress? ipAddress))
        {
            return null;
        }

        string ip = ipAddress.ToString();
        if (ip.Contains(':'))
        {
            return null;
        }

        if (!int.TryParse(parts[1], out int prefix) || prefix < 0 || prefix > 32)
        {
            return null;
        }

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
