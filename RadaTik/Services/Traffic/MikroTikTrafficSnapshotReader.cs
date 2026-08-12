using System.Globalization;
using RadaTik.Dtos.Traffic;
using RadaTik.Models;
using tik4net;

namespace RadaTik.Services.Traffic;

public sealed class MikroTikTrafficSnapshotReader
{
    private readonly TrafficRateTracker _rateTracker;
    private readonly ILogger<MikroTikTrafficSnapshotReader> _logger;

    public MikroTikTrafficSnapshotReader(TrafficRateTracker rateTracker, ILogger<MikroTikTrafficSnapshotReader> logger)
    {
        _rateTracker = rateTracker;
        _logger = logger;
    }

    public TrafficSnapshotPayload BuildSnapshot(MikroTikServer server, int networkId, string streamKey = "live")
    {
        var utc = DateTime.UtcNow;
        using var connection = ConnectionFactory.OpenConnection(
            TikConnectionType.Api,
            server.Host,
            server.Port,
            server.User,
            server.Pass);

        var bridgeMemberOf = TryLoadBridgePortMap(connection);
        // RouterOS: /interface/print =stats=yes — counters (rx-byte, tx-byte, rx-packet, …).
        // tik4net ExecuteList() defaults AddParameter to *filter* (?key=value). Plain AddParameter("stats", …)
        // becomes ?stats= and returns zero rows. Force NameValue so the API sends =stats=yes.
        List<ITikReSentence> rows = ExecuteInterfacePrintWithStatsFallback(connection);
        var lines = new List<InterfaceTrafficLineDto>(rows.Count);

        foreach (var row in rows)
        {
            var name = CoalesceWordCi(row, "name", "default-name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = GetWordCi(row, ".id");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var type = GetWordCi(row, "type");
            var running = string.Equals(GetWordCi(row, "running"), "true", StringComparison.OrdinalIgnoreCase);
            var rx = ParseLong(GetWordCi(row, "rx-byte"));
            var tx = ParseLong(GetWordCi(row, "tx-byte"));
            var rxPackets = ParseLong(
                CoalesceWordCi(row, "rx-packet", "rx-packets"));
            var txPackets = ParseLong(
                CoalesceWordCi(row, "tx-packet", "tx-packets"));
            var (rxBps, txBps) = _rateTracker.UpdateAndComputeRates(server.Id, name, rx, tx, utc, streamKey);
            var isBridge = type.Contains("bridge", StringComparison.OrdinalIgnoreCase);
            bridgeMemberOf.TryGetValue(name, out var memberOf);

            lines.Add(new InterfaceTrafficLineDto
            {
                Name = name,
                Type = type,
                Running = running,
                IsBridge = isBridge,
                MemberOfBridge = string.IsNullOrWhiteSpace(memberOf) ? null : memberOf,
                RxBytes = rx,
                TxBytes = tx,
                RxPackets = rxPackets,
                TxPackets = txPackets,
                RxBps = rxBps,
                TxBps = txBps,
            });
        }

        lines.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return new TrafficSnapshotPayload
        {
            NetworkId = networkId,
            ServerId = server.Id,
            ServerName = server.Name,
            UtcIso = utc.ToString("o", CultureInfo.InvariantCulture),
            Interfaces = lines,
        };
    }

    private Dictionary<string, string> TryLoadBridgePortMap(ITikConnection connection)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var tryCommands = new[] { "/interface/bridge/port/print", "/interface bridge port print" };
        foreach (var path in tryCommands)
        {
            try
            {
                var cmd = connection.CreateCommand(path);
                foreach (var row in cmd.ExecuteList())
                {
                    var iface = GetSafeValue(row, "interface");
                    var bridge = GetSafeValue(row, "bridge");
                    if (!string.IsNullOrWhiteSpace(iface) && !string.IsNullOrWhiteSpace(bridge))
                    {
                        map[iface] = bridge;
                    }
                }

                return map;
            }
            catch (TikNoSuchCommandException ex)
            {
                _logger.LogDebug(ex, "Bridge port list not available via {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Bridge port list failed for {Path}", path);
            }
        }

        return map;
    }

    private List<ITikReSentence> ExecuteInterfacePrintWithStatsFallback(ITikConnection connection)
    {
        try
        {
            var cmd = connection.CreateCommand("/interface/print");
            cmd.AddParameter("stats", "yes", TikCommandParameterFormat.NameValue);
            var withStats = cmd.ExecuteList().ToList();
            if (withStats.Count > 0)
            {
                return withStats;
            }

            _logger.LogWarning(
                "interface/print with =stats=yes returned 0 rows; falling back to plain print (no byte/packet stats).");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "interface/print with stats failed; retrying without stats");
        }

        return connection.CreateCommand("/interface/print").ExecuteList().ToList();
    }

    private static string GetWordCi(ITikReSentence row, string key)
    {
        foreach (var kv in row.Words)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value ?? "";
            }
        }

        return "";
    }

    private static string CoalesceWordCi(ITikReSentence row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var v = GetWordCi(row, key);
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v;
            }
        }

        return "";
    }

    private static string GetSafeValue(ITikReSentence row, string key) => GetWordCi(row, key);

    private static string CoalesceWord(ITikReSentence row, params string[] keys) => CoalesceWordCi(row, keys);

    private static long ParseLong(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return 0;
        }

        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
