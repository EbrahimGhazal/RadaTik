namespace RadaTik.Services.SectorRadio;

public sealed class RadioStationSignal
{
    public string? MacAddress { get; init; }
    public string? LastIp { get; init; }
    public string? InterfaceName { get; init; }
    public int? SignalDbm { get; init; }
    public int? SnrDb { get; init; }
    public int? CcqPercent { get; init; }
    public decimal? TxRateMbps { get; init; }
    public decimal? RxRateMbps { get; init; }
}

public sealed class SectorRadioStationsResult
{
    public bool Success { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string? InterfaceName { get; init; }
    public int? FrequencyMhz { get; init; }
    public int? NoiseFloorDbm { get; init; }
    public IReadOnlyList<RadioStationSignal> Stations { get; init; } = [];
}

public sealed record RadioStationMatch(RadioStationSignal Station, string Reason);

public static class RadioStationMatcher
{
    public static RadioStationMatch? Pick(
        IReadOnlyList<RadioStationSignal> stations,
        string? receiverIp,
        string? mac,
        string? preferredInterface)
    {
        if (stations == null || stations.Count == 0)
        {
            return null;
        }

        string macNorm = NormalizeMac(mac);
        if (macNorm.Length >= 12)
        {
            RadioStationSignal? byMac = stations.FirstOrDefault(s => NormalizeMac(s.MacAddress) == macNorm);
            if (byMac != null)
            {
                return new RadioStationMatch(byMac, "mac");
            }
        }

        string ip = (receiverIp ?? string.Empty).Trim();
        if (ip.Length > 0)
        {
            RadioStationSignal? byIp = stations.FirstOrDefault(s =>
                string.Equals((s.LastIp ?? string.Empty).Trim(), ip, StringComparison.OrdinalIgnoreCase));
            if (byIp != null)
            {
                return new RadioStationMatch(byIp, "ip");
            }
        }

        IReadOnlyList<RadioStationSignal> scoped = stations;
        if (!string.IsNullOrWhiteSpace(preferredInterface))
        {
            List<RadioStationSignal> onInterface = stations
                .Where(s => string.Equals(s.InterfaceName, preferredInterface, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (onInterface.Count > 0)
            {
                scoped = onInterface;
            }
        }

        if (scoped.Count == 1)
        {
            return new RadioStationMatch(scoped[0], "single");
        }

        return null;
    }

    public static string NormalizeMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[mac.Length];
        int n = 0;
        foreach (char c in mac)
        {
            if (Uri.IsHexDigit(c))
            {
                buffer[n++] = char.ToUpperInvariant(c);
            }
        }

        return n == 0 ? string.Empty : new string(buffer[..n]);
    }
}
