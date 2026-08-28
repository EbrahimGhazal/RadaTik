using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<SubscriberFaultDiagnosisDto> DiagnoseAsync(
        int clientId,
        int selectedNetworkId,
        SubscriberFaultLedAnswers? led = null,
        string? createdByUserId = null,
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

        SubscriberFaultLastMileStats history = await LoadLastMileHistoryAsync(companyNetworkIds, cancellationToken);
        SubscriberFaultFacts facts = await CollectFactsAsync(
            client,
            companyNetworkIds,
            led ?? new SubscriberFaultLedAnswers(),
            history,
            cancellationToken);
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(facts);
        SubscriberFaultDiagnosisRun run = ToRun(client, facts, result, createdByUserId);
        Db.SubscriberFaultDiagnosisRuns.Add(run);
        await Db.SaveChangesAsync(cancellationToken);
        return ToDto(result, run);
    }

    public async Task<SubscriberFaultDiagnosisDto> LinkToMaintenanceRequestAsync(
        long diagnosisId,
        int maintenanceRequestId,
        CancellationToken cancellationToken = default)
    {
        SubscriberFaultDiagnosisRun? run = await Db.SubscriberFaultDiagnosisRuns
            .FirstOrDefaultAsync(r => r.Id == diagnosisId, cancellationToken);
        if (run == null)
        {
            return Fail("NotFound", "سجل التشخيص غير موجود.");
        }

        run.MaintenanceRequestId = maintenanceRequestId;
        await Db.SaveChangesAsync(cancellationToken);
        return ToDtoFromRun(run);
    }

    public async Task ConfirmFromMaintenanceAsync(
        int maintenanceRequestId,
        IReadOnlyList<MaintenanceType> selectedTypes,
        string? confirmedByUserId,
        CancellationToken cancellationToken = default)
    {
        SubscriberFaultDiagnosisRun? run = await Db.SubscriberFaultDiagnosisRuns
            .Where(r => r.MaintenanceRequestId == maintenanceRequestId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (run == null)
        {
            MaintenanceRequest? request = await Db.MaintenanceRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == maintenanceRequestId, cancellationToken);
            if (request == null)
            {
                return;
            }

            run = await Db.SubscriberFaultDiagnosisRuns
                .Where(r => r.ClientId == request.ClientId && r.MaintenanceRequestId == null)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (run == null)
            {
                return;
            }

            run.MaintenanceRequestId = maintenanceRequestId;
        }

        SubscriberFaultComponent confirmed = SubscriberFaultConfirmationMapper.FromMaintenanceTypes(selectedTypes);
        run.ConfirmedCause = confirmed;
        run.ConfirmedMaintenanceType = selectedTypes.Count > 0 ? selectedTypes[0] : null;
        run.ConfirmedAt = DateTime.Now;
        run.ConfirmedByUserId = confirmedByUserId;
        run.SuggestionMatched = SubscriberFaultConfirmationMapper.MatchesSuggestion(run.Cause, confirmed);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubscriberFaultDiagnosisDto?> GetForMaintenanceRequestAsync(
        int maintenanceRequestId,
        CancellationToken cancellationToken = default)
    {
        SubscriberFaultDiagnosisRun? run = await Db.SubscriberFaultDiagnosisRuns
            .AsNoTracking()
            .Where(r => r.MaintenanceRequestId == maintenanceRequestId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return run == null ? null : ToDtoFromRun(run);
    }

    public async Task<SubscriberFaultDiagnosisDto?> GetByIdAsync(
        long diagnosisId,
        CancellationToken cancellationToken = default)
    {
        SubscriberFaultDiagnosisRun? run = await Db.SubscriberFaultDiagnosisRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == diagnosisId, cancellationToken);
        return run == null ? null : ToDtoFromRun(run);
    }

    private async Task<SubscriberFaultLastMileStats> LoadLastMileHistoryAsync(
        IReadOnlyList<int> companyNetworkIds,
        CancellationToken cancellationToken)
    {
        List<SubscriberFaultComponent> confirmed = await Db.SubscriberFaultDiagnosisRuns
            .AsNoTracking()
            .Where(r =>
                r.ConfirmedCause != null
                && r.NetworkId.HasValue
                && companyNetworkIds.Contains(r.NetworkId.Value))
            .OrderByDescending(r => r.ConfirmedAt)
            .Take(200)
            .Select(r => r.ConfirmedCause!.Value)
            .ToListAsync(cancellationToken);

        return new SubscriberFaultLastMileStats(
            CableCount: confirmed.Count(c => c == SubscriberFaultComponent.Cable),
            SwitchCount: confirmed.Count(c => c == SubscriberFaultComponent.Switch),
            RouterCount: confirmed.Count(c => c == SubscriberFaultComponent.Router),
            ReceiverCount: confirmed.Count(c => c == SubscriberFaultComponent.Receiver),
            SampleCount: confirmed.Count);
    }

    private async Task<SubscriberFaultFacts> CollectFactsAsync(
        Client client,
        IReadOnlyList<int> companyNetworkIds,
        SubscriberFaultLedAnswers led,
        SubscriberFaultLastMileStats history,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.Now;
        int? sectorId = client.Receiver?.SectorId;
        int? receiverId = client.ReceiverId;
        int? serverId = client.MikroTikServerId ?? client.Receiver?.Sector?.MikroTikServerId;
        string? sectorIp = client.Receiver?.Sector?.IPAddress;
        string? receiverIp = client.Receiver?.IPAddress;
        string? clientIp = client.Address;

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
        string? sectorPingMessage = null;
        string? receiverPingMessage = null;
        string? clientPingMessage = null;
        if (serverApiReachable && serverId.HasValue)
        {
            List<string> hopAddresses = [];
            AddHop(hopAddresses, sectorIp);
            AddHop(hopAddresses, receiverIp);
            if (!string.IsNullOrWhiteSpace(clientIp)
                && !string.Equals(clientIp, receiverIp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(clientIp, sectorIp, StringComparison.OrdinalIgnoreCase))
            {
                hopAddresses.Add(clientIp.Trim());
            }

            if (hopAddresses.Count > 0)
            {
                try
                {
                    IReadOnlyDictionary<string, MikroTikPingHopResult> hops =
                        await probe.PingManyAsync(serverId.Value, hopAddresses, cancellationToken);
                    (sectorPing, sectorPingMessage) = LookupPing(hops, sectorIp);
                    (receiverPing, receiverPingMessage) = LookupPing(hops, receiverIp);
                    (clientPing, clientPingMessage) = LookupPing(hops, clientIp);
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
            SectorIp = sectorIp,
            ReceiverIp = receiverIp,
            ClientIp = clientIp,
            SectorPingOk = sectorPing,
            ReceiverPingOk = receiverPing,
            ClientPingOk = clientPing,
            SectorPingMessage = sectorPingMessage,
            ReceiverPingMessage = receiverPingMessage,
            ClientPingMessage = clientPingMessage,
            SectorRadioDegraded = radioDegraded,
            SectorNoiseFloorDbm = noise,
            SectorSnrDb = snr,
            SectorCcqPercent = ccq,
            Led = led,
            LastMileHistory = history
        };
    }

    private static void AddHop(List<string> addresses, string? ip)
    {
        if (!string.IsNullOrWhiteSpace(ip))
        {
            addresses.Add(ip.Trim());
        }
    }

    private static int CountConnected(IReadOnlyList<PeerRow> peers, HashSet<int> connectedIds) =>
        peers.Count(p => connectedIds.Contains(p.Id));

    private static (bool? Ok, string? Message) LookupPing(
        IReadOnlyDictionary<string, MikroTikPingHopResult> hops,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return (null, null);
        }

        if (hops.TryGetValue(address.Trim(), out MikroTikPingHopResult? hop) && hop.Attempted)
        {
            return (hop.Reached, hop.StatusMessage);
        }

        return (null, null);
    }

    private static SubscriberFaultDiagnosisRun ToRun(
        Client client,
        SubscriberFaultFacts facts,
        SubscriberFaultDiagnosisResult result,
        string? createdByUserId) =>
        new()
        {
            ClientId = client.Id,
            NetworkId = client.NetworkId,
            CreatedAt = facts.Now,
            CreatedByUserId = createdByUserId,
            Cause = result.Cause,
            Confidence = result.Confidence,
            CauseLabel = result.CauseLabel,
            Summary = Truncate(result.Summary, 800),
            SuggestedAction = Truncate(result.SuggestedAction, 400),
            SuggestedMaintenanceType = result.SuggestedMaintenanceType,
            HasPppSession = facts.HasPppSession,
            HasMikroTikServer = facts.HasMikroTikServer,
            ServerApiReachable = facts.ServerApiReachable,
            ServerClientCount = facts.ServerClientCount,
            ServerConnectedCount = facts.ServerConnectedCount,
            SectorClientCount = facts.SectorClientCount,
            SectorConnectedCount = facts.SectorConnectedCount,
            ReceiverClientCount = facts.ReceiverClientCount,
            ReceiverConnectedCount = facts.ReceiverConnectedCount,
            SectorIp = facts.SectorIp,
            SectorPingOk = facts.SectorPingOk,
            SectorPingMessage = Truncate(facts.SectorPingMessage, 120),
            ReceiverIp = facts.ReceiverIp,
            ReceiverPingOk = facts.ReceiverPingOk,
            ReceiverPingMessage = Truncate(facts.ReceiverPingMessage, 120),
            ClientIp = facts.ClientIp,
            ClientPingOk = facts.ClientPingOk,
            ClientPingMessage = Truncate(facts.ClientPingMessage, 120),
            SectorRadioDegraded = facts.SectorRadioDegraded,
            SectorNoiseFloorDbm = facts.SectorNoiseFloorDbm,
            SectorSnrDb = facts.SectorSnrDb,
            SectorCcqPercent = facts.SectorCcqPercent,
            RouterPowerOn = facts.Led.RouterPowerOn,
            InternetLedOn = facts.Led.InternetLedOn,
            WanLedOn = facts.Led.WanLedOn,
            NeighborsOnSwitchDown = facts.Led.NeighborsOnSwitchDown,
            EvidenceJson = JsonSerializer.Serialize(result.Evidence, JsonOptions)
        };

    private static SubscriberFaultDiagnosisDto ToDto(SubscriberFaultDiagnosisResult result, SubscriberFaultDiagnosisRun run)
    {
        SubscriberFaultDiagnosisDto dto = ToDtoFromRun(run);
        return new SubscriberFaultDiagnosisDto
        {
            Success = true,
            Status = "Ok",
            DiagnosisId = run.Id,
            ClientId = run.ClientId,
            MaintenanceRequestId = run.MaintenanceRequestId,
            Cause = result.Cause.ToString(),
            CauseLabel = result.CauseLabel,
            Confidence = result.Confidence.ToString(),
            ConfidenceLabel = result.ConfidenceLabel,
            Summary = result.Summary,
            SuggestedAction = result.SuggestedAction,
            SuggestedMaintenanceType = result.SuggestedMaintenanceType?.ToString(),
            SuggestedMaintenanceLabel = MaintenanceLabel(result.SuggestedMaintenanceType),
            CanCreateMaintenance = result.Cause != SubscriberFaultComponent.Account,
            Hops = dto.Hops,
            Evidence = result.Evidence
                .Select(e => new SubscriberFaultEvidenceDto
                {
                    Code = e.Code,
                    Label = e.Label,
                    Value = e.Value,
                    IsAlert = e.IsAlert
                })
                .ToList(),
            CreatedAt = run.CreatedAt
        };
    }

    private static SubscriberFaultDiagnosisDto ToDtoFromRun(SubscriberFaultDiagnosisRun run)
    {
        List<SubscriberFaultEvidenceDto> evidence = [];
        if (!string.IsNullOrWhiteSpace(run.EvidenceJson))
        {
            try
            {
                evidence = JsonSerializer.Deserialize<List<SubscriberFaultEvidenceDto>>(run.EvidenceJson, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                evidence = [];
            }
        }

        return new SubscriberFaultDiagnosisDto
        {
            Success = true,
            Status = "Ok",
            DiagnosisId = run.Id,
            ClientId = run.ClientId,
            MaintenanceRequestId = run.MaintenanceRequestId,
            Cause = run.Cause.ToString(),
            CauseLabel = run.CauseLabel,
            Confidence = run.Confidence.ToString(),
            ConfidenceLabel = run.Confidence switch
            {
                SubscriberFaultConfidence.High => "عالية",
                SubscriberFaultConfidence.Medium => "متوسطة",
                _ => "منخفضة"
            },
            Summary = run.Summary,
            SuggestedAction = run.SuggestedAction,
            SuggestedMaintenanceType = run.SuggestedMaintenanceType?.ToString(),
            SuggestedMaintenanceLabel = MaintenanceLabel(run.SuggestedMaintenanceType),
            CanCreateMaintenance = run.Cause != SubscriberFaultComponent.Account && run.MaintenanceRequestId == null,
            Hops =
            [
                new SubscriberFaultHopDto { Name = "المرسل", Address = run.SectorIp, Status = HopStatus(run.SectorPingOk) },
                new SubscriberFaultHopDto { Name = "اللاقط", Address = run.ReceiverIp, Status = HopStatus(run.ReceiverPingOk) },
                new SubscriberFaultHopDto { Name = "المشترك", Address = run.ClientIp, Status = HopStatus(run.ClientPingOk) }
            ],
            Evidence = evidence,
            ConfirmedCause = run.ConfirmedCause?.ToString(),
            ConfirmedCauseLabel = run.ConfirmedCause.HasValue
                ? SubscriberFaultDiagnosisEngine.LabelOf(run.ConfirmedCause.Value)
                : null,
            ConfirmedMaintenanceLabel = MaintenanceLabel(run.ConfirmedMaintenanceType),
            SuggestionMatched = run.SuggestionMatched,
            CreatedAt = run.CreatedAt
        };
    }

    private static string HopStatus(bool? ok) => ok switch
    {
        true => "يرد",
        false => "لا يرد",
        _ => "لم يُفحص"
    };

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value[..max];
    }

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

        return MaintenanceCatalog.GetDisplayName(type.Value);
    }

    private sealed record PeerRow(
        int Id,
        string? UserName,
        int? MikroTikServerId,
        int? ReceiverId,
        int? SectorId);
}
