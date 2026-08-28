using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Domain.FaultDiagnosis;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

public sealed class SubscriberFaultDiagnosisService(
    ApplicationDbContext db,
    IMikroTikPppoeUserService pppoe,
    IMikroTikProbeService probe,
    ILogger<SubscriberFaultDiagnosisService> logger)
    : ApplicationServiceBase(db), ISubscriberFaultDiagnosisService
{
    private static readonly TimeSpan RadioSampleMaxAge = TimeSpan.FromMinutes(30);

    public async Task<SubscriberFaultDiagnosisDto> DiagnoseAsync(
        int clientId,
        int selectedNetworkId,
        CancellationToken cancellationToken = default)
    {
        List<int> companyNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsForSelectedAsync(
            Db,
            selectedNetworkId,
            cancellationToken);

        Client? client = await Db.Clients
            .AsNoTracking()
            .Include(c => c.Receiver)
                .ThenInclude(r => r!.Sector)
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        if (client == null)
        {
            return Fail("NotFound", "المشترك غير موجود.");
        }

        if (!client.NetworkId.HasValue || !companyNetworkIds.Contains(client.NetworkId.Value))
        {
            return Fail("Forbidden", "لا يمكن تشخيص مشترك خارج الشبكة المحددة.");
        }

        SubscriberFaultFacts facts = await CollectFactsAsync(client, companyNetworkIds, cancellationToken);
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(facts);
        return ToDto(result);
    }

    private async Task<SubscriberFaultFacts> CollectFactsAsync(
        Client client,
        IReadOnlyList<int> companyNetworkIds,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.Now;
        int? sectorId = client.Receiver?.SectorId;
        int? receiverId = client.ReceiverId;
        int? serverId = client.MikroTikServerId ?? client.Receiver?.Sector?.MikroTikServerId;

        List<PeerRow> peers = await Db.Clients
            .AsNoTracking()
            .Where(c =>
                c.IsActive
                && c.NetworkId.HasValue
                && companyNetworkIds.Contains(c.NetworkId.Value))
            .Select(c => new PeerRow(
                c.Id,
                c.UserName,
                c.MikroTikServerId,
                c.ReceiverId,
                c.Receiver != null ? c.Receiver.SectorId : null))
            .ToListAsync(cancellationToken);

        HashSet<int> connectedIds = [];
        bool serverApiReachable = false;
        if (serverId.HasValue)
        {
            try
            {
                List<Client> sessions = await pppoe.GetActivePPPoEUsers(serverId.Value);
                serverApiReachable = true;
                Dictionary<int, IReadOnlyCollection<string>> byServer = new()
                {
                    [serverId.Value] = sessions
                        .Select(s => ClientLiveConnectionMatcher.NormalizeUserName(s.UserName))
                        .Where(name => name.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                };
                List<Client> matchSet = peers
                    .Where(p => !string.IsNullOrWhiteSpace(p.UserName))
                    .Select(p => new Client
                    {
                        Id = p.Id,
                        UserName = p.UserName,
                        MikroTikServerId = p.MikroTikServerId ?? serverId
                    })
                    .ToList();
                if (matchSet.All(c => c.Id != client.Id) && !string.IsNullOrWhiteSpace(client.UserName))
                {
                    matchSet.Add(new Client
                    {
                        Id = client.Id,
                        UserName = client.UserName,
                        MikroTikServerId = serverId
                    });
                }

                connectedIds = ClientLiveConnectionMatcher.Match(matchSet, byServer);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "تعذر جلب جلسات PPPoE أثناء تشخيص المشترك {ClientId}", client.Id);
                serverApiReachable = false;
            }
        }

        List<PeerRow> serverPeers = serverId.HasValue
            ? peers.Where(p => p.MikroTikServerId == serverId).ToList()
            : [];
        if (serverId.HasValue && serverPeers.All(p => p.Id != client.Id))
        {
            serverPeers.Add(new PeerRow(client.Id, client.UserName, serverId, receiverId, sectorId));
        }

        List<PeerRow> sectorPeers = sectorId.HasValue
            ? peers.Where(p => p.SectorId == sectorId).ToList()
            : [];
        List<PeerRow> receiverPeers = receiverId.HasValue
            ? peers.Where(p => p.ReceiverId == receiverId).ToList()
            : [];

        if (sectorId.HasValue && sectorPeers.All(p => p.Id != client.Id))
        {
            sectorPeers.Add(new PeerRow(client.Id, client.UserName, serverId, receiverId, sectorId));
        }

        if (receiverId.HasValue && receiverPeers.All(p => p.Id != client.Id))
        {
            receiverPeers.Add(new PeerRow(client.Id, client.UserName, serverId, receiverId, sectorId));
        }

        bool? sectorPing = null;
        bool? receiverPing = null;
        bool? clientPing = null;
        if (serverApiReachable && serverId.HasValue)
        {
            List<string> hopAddresses = [];
            string? sectorIp = client.Receiver?.Sector?.IPAddress;
            string? receiverIp = client.Receiver?.IPAddress;
            string? clientIp = client.Address;
            if (!string.IsNullOrWhiteSpace(sectorIp))
            {
                hopAddresses.Add(sectorIp);
            }

            if (!string.IsNullOrWhiteSpace(receiverIp))
            {
                hopAddresses.Add(receiverIp);
            }

            if (!string.IsNullOrWhiteSpace(clientIp)
                && !string.Equals(clientIp, receiverIp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(clientIp, sectorIp, StringComparison.OrdinalIgnoreCase))
            {
                hopAddresses.Add(clientIp);
            }

            if (hopAddresses.Count > 0)
            {
                try
                {
                    IReadOnlyDictionary<string, MikroTikPingHopResult> hops =
                        await probe.PingManyAsync(serverId.Value, hopAddresses, cancellationToken);
                    sectorPing = LookupPing(hops, sectorIp);
                    receiverPing = LookupPing(hops, receiverIp);
                    clientPing = LookupPing(hops, clientIp);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "تعذر تنفيذ سلسلة Ping أثناء تشخيص المشترك {ClientId}", client.Id);
                }
            }
        }

        bool radioDegraded = false;
        int? noise = null;
        int? snr = null;
        int? ccq = null;
        if (sectorId.HasValue)
        {
            SectorRadioMetricSample? sample = await Db.SectorRadioMetricSamples
                .AsNoTracking()
                .Where(s => s.SectorId == sectorId.Value)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (sample != null && now - sample.CapturedAt <= RadioSampleMaxAge)
            {
                noise = sample.NoiseFloorDbm;
                snr = sample.SnrDb;
                ccq = sample.CcqPercent;
                Sector? sector = client.Receiver?.Sector;
                int noiseThreshold = sector?.NoiseAlertThresholdDbm ?? -90;
                int snrMin = sector?.SnrAlertMinDb ?? 20;
                int ccqMin = sector?.CcqAlertMinPercent ?? 70;
                radioDegraded =
                    (noise.HasValue && noise.Value > noiseThreshold)
                    || (snr.HasValue && snr.Value < snrMin)
                    || (ccq.HasValue && ccq.Value < ccqMin);
            }
        }

        return new SubscriberFaultFacts
        {
            Now = now,
            IsAccountActive = client.IsActive,
            AccountExpirationDate = client.AccountExpirationDate,
            HasMikroTikServer = serverId.HasValue,
            ServerApiReachable = serverApiReachable,
            HasPppSession = connectedIds.Contains(client.Id),
            ServerClientCount = Math.Max(serverPeers.Count, serverId.HasValue ? 1 : 0),
            ServerConnectedCount = CountConnected(serverPeers, connectedIds),
            SectorClientCount = sectorPeers.Count,
            SectorConnectedCount = CountConnected(sectorPeers, connectedIds),
            ReceiverClientCount = receiverPeers.Count,
            ReceiverConnectedCount = CountConnected(receiverPeers, connectedIds),
            SectorPingOk = sectorPing,
            ReceiverPingOk = receiverPing,
            ClientPingOk = clientPing,
            SectorRadioDegraded = radioDegraded,
            SectorNoiseFloorDbm = noise,
            SectorSnrDb = snr,
            SectorCcqPercent = ccq
        };
    }

    private static int CountConnected(IReadOnlyList<PeerRow> peers, HashSet<int> connectedIds) =>
        peers.Count(p => connectedIds.Contains(p.Id));

    private static bool? LookupPing(IReadOnlyDictionary<string, MikroTikPingHopResult> hops, string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return hops.TryGetValue(address.Trim(), out MikroTikPingHopResult? hop) && hop.Attempted
            ? hop.Reached
            : null;
    }

    private static SubscriberFaultDiagnosisDto ToDto(SubscriberFaultDiagnosisResult result) =>
        new()
        {
            Success = true,
            Status = "Ok",
            Cause = result.Cause.ToString(),
            CauseLabel = result.CauseLabel,
            Confidence = result.Confidence.ToString(),
            ConfidenceLabel = result.ConfidenceLabel,
            Summary = result.Summary,
            SuggestedAction = result.SuggestedAction,
            SuggestedMaintenanceType = result.SuggestedMaintenanceType?.ToString(),
            SuggestedMaintenanceLabel = MaintenanceLabel(result.SuggestedMaintenanceType),
            Evidence = result.Evidence
                .Select(e => new SubscriberFaultEvidenceDto
                {
                    Code = e.Code,
                    Label = e.Label,
                    Value = e.Value,
                    IsAlert = e.IsAlert
                })
                .ToList()
        };

    private static SubscriberFaultDiagnosisDto Fail(string status, string message) =>
        new()
        {
            Success = false,
            Status = status,
            Message = message
        };

    private static string? MaintenanceLabel(MaintenanceType? type)
    {
        if (!type.HasValue)
        {
            return null;
        }

        FieldInfo? field = typeof(MaintenanceType).GetField(type.Value.ToString());
        DescriptionAttribute? attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? type.Value.ToString();
    }

    private sealed record PeerRow(
        int Id,
        string? UserName,
        int? MikroTikServerId,
        int? ReceiverId,
        int? SectorId);
}
