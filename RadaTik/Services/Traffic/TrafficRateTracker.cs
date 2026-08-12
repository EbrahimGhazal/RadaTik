using System.Collections.Concurrent;

namespace RadaTik.Services.Traffic;

/// <summary>Computes approximate RX/TX rates from byte counter deltas between polls.</summary>
public sealed class TrafficRateTracker
{
    private readonly ConcurrentDictionary<(string StreamKey, int ServerId, string IfName), (long Rx, long Tx, DateTime Utc)> _last =
        new();

    public (double RxBps, double TxBps) UpdateAndComputeRates(
        int serverId,
        string ifName,
        long rxBytes,
        long txBytes,
        DateTime utcNow,
        string streamKey = "live")
    {
        var key = (streamKey, serverId, ifName);
        if (!_last.TryGetValue(key, out var prev))
        {
            _last[key] = (rxBytes, txBytes, utcNow);
            return (0, 0);
        }

        var elapsed = (utcNow - prev.Utc).TotalSeconds;
        if (elapsed <= 0.001)
        {
            return (0, 0);
        }

        var dRx = Math.Max(0, rxBytes - prev.Rx);
        var dTx = Math.Max(0, txBytes - prev.Tx);
        _last[key] = (rxBytes, txBytes, utcNow);
        return (dRx * 8.0 / elapsed, dTx * 8.0 / elapsed);
    }
}
