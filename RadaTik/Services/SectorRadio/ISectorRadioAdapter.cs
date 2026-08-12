using RadaTik.Models;

namespace RadaTik.Services.SectorRadio;

public interface ISectorRadioAdapter
{
    Task<SectorRadioMetricsResult> ReadMetricsAsync(
        Sector sector,
        MikroTikServer server,
        CancellationToken cancellationToken = default);
}
