namespace RadaTik.Domain.FaultDiagnosis;

/// <summary>وقائع جاهزة لمحرك العزل — بدون اعتماد على قاعدة البيانات أو MikroTik.</summary>
public sealed record SubscriberFaultFacts
{
    public DateTime Now { get; init; } = DateTime.Now;
    public bool IsAccountActive { get; init; } = true;
    public DateTime? AccountExpirationDate { get; init; }
    public bool HasMikroTikServer { get; init; }
    public bool ServerApiReachable { get; init; }
    public bool HasPppSession { get; init; }

    /// <summary>عدد المشتركين النشطين على نفس السيرفر بما فيهم هذا المشترك.</summary>
    public int ServerClientCount { get; init; } = 1;

    /// <summary>كم منهم لديه جلسة PPPoE حية.</summary>
    public int ServerConnectedCount { get; init; }

    public int SectorClientCount { get; init; }
    public int SectorConnectedCount { get; init; }
    public int ReceiverClientCount { get; init; }
    public int ReceiverConnectedCount { get; init; }

    public string? SectorIp { get; init; }
    public string? ReceiverIp { get; init; }
    public string? ClientIp { get; init; }

    /// <summary>null = لم يُفحص.</summary>
    public bool? SectorPingOk { get; init; }
    public bool? ReceiverPingOk { get; init; }
    public bool? ClientPingOk { get; init; }
    public string? SectorPingMessage { get; init; }
    public string? ReceiverPingMessage { get; init; }
    public string? ClientPingMessage { get; init; }

    public bool SectorRadioDegraded { get; init; }
    public int? SectorNoiseFloorDbm { get; init; }
    public int? SectorSnrDb { get; init; }
    public int? SectorCcqPercent { get; init; }

    public SubscriberFaultLedAnswers Led { get; init; } = new();
    public SubscriberFaultLastMileStats? LastMileHistory { get; init; }
}
