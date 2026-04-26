namespace RadTik.Dtos.Traffic;

public sealed class TrafficSnapshotPayload
{
    public int NetworkId { get; set; }
    public int ServerId { get; set; }
    public string ServerName { get; set; } = "";
    public string UtcIso { get; set; } = "";
    public IReadOnlyList<InterfaceTrafficLineDto> Interfaces { get; set; } = Array.Empty<InterfaceTrafficLineDto>();
}

/// <summary>
/// One row per RouterOS interface from <c>/interface/print stats=yes</c>
/// (Lists → Interfaces → Interface: name, type, Rx/Tx bytes, Rx/Tx packets, etc.).
/// </summary>
public sealed class InterfaceTrafficLineDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Running { get; set; }
    public bool IsBridge { get; set; }
    /// <summary>When this interface is a bridge port, the bridge name it belongs to.</summary>
    public string? MemberOfBridge { get; set; }
    /// <summary>Cumulative RX bytes (API: rx-byte).</summary>
    public long RxBytes { get; set; }
    /// <summary>Cumulative TX bytes (API: tx-byte).</summary>
    public long TxBytes { get; set; }
    /// <summary>Cumulative RX packet count (API: rx-packet).</summary>
    public long RxPackets { get; set; }
    /// <summary>Cumulative TX packet count (API: tx-packet).</summary>
    public long TxPackets { get; set; }
    /// <summary>Receive rate in bits per second (approximate from delta between polls).</summary>
    public double RxBps { get; set; }
    /// <summary>Transmit rate in bits per second.</summary>
    public double TxBps { get; set; }
}

public sealed class ManagerMikroTikServerOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int NetworkId { get; set; }
}

public sealed class TrafficPeriodStatisticsDto
{
    public string PeriodKey { get; set; } = "";
    public string FromUtcIso { get; set; } = "";
    public string ToUtcIso { get; set; } = "";
    public int Samples { get; set; }
    public double? RxMinBps { get; set; }
    public double? RxAvgBps { get; set; }
    public double? RxMaxBps { get; set; }
    public double? TxMinBps { get; set; }
    public double? TxAvgBps { get; set; }
    public double? TxMaxBps { get; set; }
}

public sealed class TrafficStatisticsOverviewDto
{
    public int ServerId { get; set; }
    public string ServerName { get; set; } = "";
    public string GeneratedAtUtcIso { get; set; } = "";
    public IReadOnlyList<TrafficPeriodStatisticsDto> Periods { get; set; } = Array.Empty<TrafficPeriodStatisticsDto>();
}

public sealed class TrafficTrendPointDto
{
    public string BucketUtcIso { get; set; } = "";
    public double RxAvgBps { get; set; }
    public double TxAvgBps { get; set; }
}

public sealed class TrafficTrendResponseDto
{
    public int ServerId { get; set; }
    public string PeriodKey { get; set; } = "";
    public string GeneratedAtUtcIso { get; set; } = "";
    public IReadOnlyList<TrafficTrendPointDto> Points { get; set; } = Array.Empty<TrafficTrendPointDto>();
}

public sealed class TrafficKpiThresholdsDto
{
    public double PeakRxWarnBps { get; set; }
    public double PeakRxCriticalBps { get; set; }
    public double PeakTxWarnBps { get; set; }
    public double PeakTxCriticalBps { get; set; }
    public int LoadIndexWarnPercent { get; set; }
    public int LoadIndexCriticalPercent { get; set; }
}

public sealed class ClientLiveTrafficDto
{
    public bool Connected { get; set; }
    public string UtcIso { get; set; } = "";
    public string UserName { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string? Address { get; set; }
    public string? MacAddress { get; set; }
    public string? Uptime { get; set; }
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public long RxPackets { get; set; }
    public long TxPackets { get; set; }
    public double RxBps { get; set; }
    public double TxBps { get; set; }
}

public sealed class ClientTrafficTestStatusDto
{
    public bool TestActive { get; set; }
    public bool CanStartTest { get; set; }
    public string? ActiveUntilUtcIso { get; set; }
    public string? NextEligibleUtcIso { get; set; }
    public int DurationSeconds { get; set; }
    public int CooldownHours { get; set; }
    public decimal ChargeAmount { get; set; }
    public decimal CurrentBalance { get; set; }
    public int SecondsRemaining { get; set; }
}
