using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using tik4net;

namespace RadaTik.Services.MikroTik;

public sealed class MikroTikProbeService(
    ApplicationDbContext db,
    MikroTikConnectionSupport connection,
    ILogger<MikroTikProbeService> logger) : IMikroTikProbeService
{
    public async Task<IReadOnlyDictionary<string, MikroTikPingHopResult>> PingManyAsync(
        int serverId,
        IReadOnlyList<string> addresses,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, MikroTikPingHopResult> results = new(StringComparer.OrdinalIgnoreCase);
        List<string> unique = addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unique.Count == 0)
        {
            return results;
        }

        MikroTikServer? server = await db.MikroTikServers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId, cancellationToken);
        if (server == null || !server.IsActive)
        {
            foreach (string address in unique)
            {
                results[address] = new MikroTikPingHopResult
                {
                    Address = address,
                    Attempted = false,
                    Reached = false,
                    StatusMessage = "السيرفر غير موجود أو غير نشط."
                };
            }

            return results;
        }

        try
        {
            using ITikConnection tik = connection.CreateConnectionWithRetry(server, maxRetries: 1);
            foreach (string address in unique)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[address] = PingOne(tik, address);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "تعذر فتح اتصال MikroTik لفحص Ping. ServerId={ServerId}", serverId);
            foreach (string address in unique)
            {
                results.TryAdd(address, new MikroTikPingHopResult
                {
                    Address = address,
                    Attempted = false,
                    Reached = false,
                    StatusMessage = "تعذر الاتصال بالسيرفر لإجراء Ping."
                });
            }
        }

        return results;
    }

    private MikroTikPingHopResult PingOne(ITikConnection tik, string address)
    {
        try
        {
            ITikCommand pingCmd = tik.CreateCommand("/ping");
            pingCmd.AddParameter("address", address);
            pingCmd.AddParameter("count", "1");
            IEnumerable<ITikReSentence> rows = pingCmd.ExecuteList();
            List<(string Received, string Time, string Status, string PacketLoss)> parsed = rows
                .Select(row => (
                    MikroTikApiSupport.GetSafeValue(row, "received"),
                    MikroTikApiSupport.GetSafeValue(row, "time"),
                    MikroTikApiSupport.GetSafeValue(row, "status"),
                    MikroTikApiSupport.GetSafeValue(row, "packet-loss")))
                .ToList();

            bool reached = MikroTikPingParser.IsReachable(parsed);
            return new MikroTikPingHopResult
            {
                Address = address,
                Attempted = true,
                Reached = reached,
                StatusMessage = reached ? "يرد" : "لا يرد"
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "فشل Ping للعنوان {Address}", address);
            return new MikroTikPingHopResult
            {
                Address = address,
                Attempted = true,
                Reached = false,
                StatusMessage = "فشل أمر Ping"
            };
        }
    }
}
