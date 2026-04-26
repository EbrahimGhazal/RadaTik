using System.Globalization;
using System.Text.RegularExpressions;
using RadTik.Models;
using tik4net;

namespace RadTik.Services.SectorRadio;

public sealed class MikroTikSectorRadioAdapter : ISectorRadioAdapter
{
    private readonly ILogger<MikroTikSectorRadioAdapter> _logger;

    public MikroTikSectorRadioAdapter(ILogger<MikroTikSectorRadioAdapter> logger)
    {
        _logger = logger;
    }

    public Task<SectorRadioMetricsResult> ReadMetricsAsync(
        Sector sector,
        MikroTikServer server,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass);

            // PoC strategy:
            // 1) Try reading wireless metrics directly from MikroTik radio interface.
            // 2) If not available, fallback to connectivity probe against sector IP.
            var interfaceName = GetPreferredWirelessInterface(connection, sector);
            if (!string.IsNullOrWhiteSpace(interfaceName))
            {
                var monitorResult = ReadWirelessMonitor(connection, interfaceName!);
                if (monitorResult.Success)
                {
                    return Task.FromResult(monitorResult);
                }
            }

            var probeResult = ProbeSectorReachability(connection, sector);
            return Task.FromResult(probeResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read sector radio metrics. SectorId={SectorId}, ServerId={ServerId}", sector.Id, server.Id);
            return Task.FromResult(new SectorRadioMetricsResult
            {
                Success = false,
                StatusMessage = "تعذر الاتصال بخادم MikroTik لقراءة القياسات."
            });
        }
    }

    private static string? GetPreferredWirelessInterface(ITikConnection connection, Sector sector)
    {
        var rows = ExecuteRadioInterfacesPrintWithFallback(connection);
        if (!rows.Any())
        {
            return null;
        }

        // 1) Explicit mapping from Sector configuration
        if (!string.IsNullOrWhiteSpace(sector.RadioInterfaceName))
        {
            var mapped = rows.FirstOrDefault(r =>
                string.Equals(GetSafeValue(r, "name"), sector.RadioInterfaceName, StringComparison.OrdinalIgnoreCase));
            if (mapped != null)
            {
                return GetSafeValue(mapped, "name");
            }
        }

        // 2) Heuristic mapping from sector name tokens -> interface name
        var sectorTokens = BuildTokens(sector.Name);
        var best = rows
            .Select(r =>
            {
                var ifName = GetSafeValue(r, "name");
                var score = ScoreInterfaceName(ifName, sectorTokens);
                var running = string.Equals(GetSafeValue(r, "running"), "true", StringComparison.OrdinalIgnoreCase);
                var disabled = string.Equals(GetSafeValue(r, "disabled"), "true", StringComparison.OrdinalIgnoreCase);
                return new { ifName, score, running, disabled };
            })
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.running)
            .ThenBy(x => x.disabled)
            .FirstOrDefault();

        if (best is { score: > 0 })
        {
            return best.ifName;
        }

        // 3) Safe fallback: first running/non-disabled if possible, else first
        var runningPreferred = rows.FirstOrDefault(r =>
            string.Equals(GetSafeValue(r, "running"), "true", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(GetSafeValue(r, "disabled"), "true", StringComparison.OrdinalIgnoreCase));
        if (runningPreferred != null)
        {
            return GetSafeValue(runningPreferred, "name");
        }

        return GetSafeValue(rows.First(), "name");
    }

    private static List<dynamic> ExecuteRadioInterfacesPrintWithFallback(ITikConnection connection)
    {
        var commands = new[]
        {
            "/interface/wireless/print",
            "/interface/wifi/print",
            "/interface/wifiwave2/print"
        };

        foreach (var command in commands)
        {
            try
            {
                var rows = connection.CreateCommand(command).ExecuteList();
                if (rows != null)
                {
                    return rows.Cast<dynamic>().ToList();
                }
            }
            catch
            {
                // keep trying the next command style
            }
        }

        return [];
    }

    private static List<string> BuildTokens(string? source)
    {
        return (source ?? string.Empty)
            .Split([' ', '-', '_', '/', '\\', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 2)
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private static int ScoreInterfaceName(string interfaceName, List<string> sectorTokens)
    {
        if (string.IsNullOrWhiteSpace(interfaceName) || sectorTokens.Count == 0)
        {
            return 0;
        }

        var lower = interfaceName.ToLowerInvariant();
        var score = 0;
        foreach (var token in sectorTokens)
        {
            if (lower.Contains(token))
            {
                score += token.Length;
            }
        }
        return score;
    }

    private static SectorRadioMetricsResult ReadWirelessMonitor(ITikConnection connection, string interfaceName)
    {
        try
        {
            var monitorCmd = connection.CreateCommand("/interface/wireless/monitor");
            monitorCmd.AddParameter("numbers", interfaceName);
            monitorCmd.AddParameter("once", "");
            var rows = monitorCmd.ExecuteList();
            var row = rows.FirstOrDefault();
            if (row == null)
            {
                return new SectorRadioMetricsResult
                {
                    Success = false,
                    StatusMessage = "لم يتم استلام بيانات monitor من الواجهة اللاسلكية."
                };
            }

            var frequency = ParseInt(GetSafeValue(row, "frequency"));
            var noise = ParseDbm(GetSafeValue(row, "noise-floor"));
            var signal = ParseDbm(GetSafeValue(row, "signal-strength"));
            var ccq = ParsePercent(GetSafeValue(row, "overall-tx-ccq"));
            var txRate = ParseRateMbps(GetSafeValue(row, "tx-rate"));
            var rxRate = ParseRateMbps(GetSafeValue(row, "rx-rate"));
            var channelWidth = ParseInt(GetSafeValue(row, "channel-width"));

            int? snr = null;
            if (signal.HasValue && noise.HasValue)
            {
                snr = signal.Value - noise.Value;
            }

            return new SectorRadioMetricsResult
            {
                Success = true,
                StatusMessage = "تمت قراءة القياسات من واجهة MikroTik اللاسلكية.",
                FrequencyMhz = frequency,
                ChannelWidthMhz = channelWidth,
                NoiseFloorDbm = noise,
                SignalDbm = signal,
                SnrDb = snr,
                CcqPercent = ccq,
                TxRateMbps = txRate,
                RxRateMbps = rxRate
            };
        }
        catch
        {
            return new SectorRadioMetricsResult
            {
                Success = false,
                StatusMessage = "الواجهة اللاسلكية غير مدعومة أو لا تعيد metrics كافية."
            };
        }
    }

    private static SectorRadioMetricsResult ProbeSectorReachability(ITikConnection connection, Sector sector)
    {
        if (string.IsNullOrWhiteSpace(sector.IPAddress))
        {
            return new SectorRadioMetricsResult
            {
                Success = false,
                StatusMessage = "القطاع لا يحتوي على IP إدارة، لا يمكن إجراء probe."
            };
        }

        try
        {
            var pingCmd = connection.CreateCommand("/ping");
            pingCmd.AddParameter("address", sector.IPAddress);
            pingCmd.AddParameter("count", "3");
            var rows = pingCmd.ExecuteList();
            var success = rows.Any();
            return new SectorRadioMetricsResult
            {
                Success = success,
                StatusMessage = success
                    ? "تم التحقق من الوصول للقطاع (fallback probe)."
                    : "تعذر الوصول للقطاع عبر probe."
            };
        }
        catch
        {
            return new SectorRadioMetricsResult
            {
                Success = false,
                StatusMessage = "فشل probe للقطاع عبر MikroTik."
            };
        }
    }

    private static string GetSafeValue(dynamic row, string key)
    {
        try
        {
            var value = row[key];
            return value?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int? ParseInt(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        var match = Regex.Match(value ?? string.Empty, @"-?\d+");
        if (match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }
        return null;
    }

    private static int? ParseDbm(string value)
    {
        return ParseInt(value);
    }

    private static int? ParsePercent(string value)
    {
        return ParseInt(value);
    }

    private static decimal? ParseRateMbps(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"\d+(\.\d+)?");
        if (!match.Success)
        {
            return null;
        }

        if (!decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var raw))
        {
            return null;
        }

        if (value.Contains("Gbps", StringComparison.OrdinalIgnoreCase))
        {
            return raw * 1000m;
        }
        if (value.Contains("Kbps", StringComparison.OrdinalIgnoreCase))
        {
            return raw / 1000m;
        }
        return raw;
    }
}
