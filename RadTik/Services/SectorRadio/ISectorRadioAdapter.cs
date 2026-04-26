using RadTik.Models;

namespace RadTik.Services.SectorRadio;

public interface ISectorRadioAdapter
{
    Task<SectorRadioMetricsResult> ReadMetricsAsync(
        Sector sector,
        MikroTikServer server,
        CancellationToken cancellationToken = default);
}
