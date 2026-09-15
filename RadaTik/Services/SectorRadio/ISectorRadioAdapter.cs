using RadaTik.Models;

namespace RadaTik.Services.SectorRadio;

public interface ISectorRadioAdapter
{
    Task<SectorRadioMetricsResult> ReadMetricsAsync(
        Sector sector,
        MikroTikServer server,
        CancellationToken cancellationToken = default);

    Task<SectorRadioStationsResult> ReadStationsAsync(
        Sector sector,
        MikroTikServer server,
        CancellationToken cancellationToken = default);
}
