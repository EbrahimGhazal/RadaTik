using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;

namespace RadTik.Services.SectorRadio;

public sealed class SectorRadioMetricsCollector
{
    private readonly ApplicationDbContext _db;
    private readonly ISectorRadioAdapter _adapter;
    private readonly ILogger<SectorRadioMetricsCollector> _logger;

    public SectorRadioMetricsCollector(
        ApplicationDbContext db,
        ISectorRadioAdapter adapter,
        ILogger<SectorRadioMetricsCollector> logger)
    {
        _db = db;
        _adapter = adapter;
        _logger = logger;
    }

    public async Task CollectForJobAsync(SectorRadioMetricsJob job, CancellationToken cancellationToken = default)
    {
        var sector = await _db.Sectors
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == job.SectorId, cancellationToken);

        var server = await _db.MikroTikServers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == job.MikroTikServerId, cancellationToken);

        if (sector == null || server == null || !server.IsActive)
        {
            return;
        }

        var result = await _adapter.ReadMetricsAsync(sector, server, cancellationToken);
        var sample = new SectorRadioMetricSample
        {
            SectorId = sector.Id,
            MikroTikServerId = server.Id,
            CapturedAt = DateTime.Now,
            FrequencyMhz = result.FrequencyMhz,
            ChannelWidthMhz = result.ChannelWidthMhz,
            NoiseFloorDbm = result.NoiseFloorDbm,
            SignalDbm = result.SignalDbm,
            SnrDb = result.SnrDb,
            CcqPercent = result.CcqPercent,
            TxRateMbps = result.TxRateMbps,
            RxRateMbps = result.RxRateMbps,
            Source = "MikroTik",
            StatusMessage = result.StatusMessage
        };

        _db.SectorRadioMetricSamples.Add(sample);
        await _db.SaveChangesAsync(cancellationToken);

        await EvaluateAndRaiseThresholdEventsAsync(sector, sample, cancellationToken);

        _logger.LogInformation(
            "Sector metrics collected. SectorId={SectorId}, Success={Success}, Frequency={Frequency}",
            sector.Id, result.Success, result.FrequencyMhz);
    }

    private async Task EvaluateAndRaiseThresholdEventsAsync(
        Sector sector,
        SectorRadioMetricSample sample,
        CancellationToken cancellationToken)
    {
        var noiseThreshold = sector.NoiseAlertThresholdDbm ?? -90;
        var snrMinThreshold = sector.SnrAlertMinDb ?? 20;
        var ccqMinThreshold = sector.CcqAlertMinPercent ?? 70;

        if (sample.NoiseFloorDbm.HasValue && sample.NoiseFloorDbm.Value > noiseThreshold)
        {
            await CreateThresholdEventIfNeededAsync(
                sector.Id,
                sample.Id,
                "Noise",
                sample.NoiseFloorDbm.Value,
                noiseThreshold,
                $"ارتفاع الضجيج في القطاع ({sample.NoiseFloorDbm.Value} dBm) فوق الحد ({noiseThreshold} dBm).",
                cancellationToken);
        }

        if (sample.SnrDb.HasValue && sample.SnrDb.Value < snrMinThreshold)
        {
            await CreateThresholdEventIfNeededAsync(
                sector.Id,
                sample.Id,
                "SNR",
                sample.SnrDb.Value,
                snrMinThreshold,
                $"انخفاض SNR في القطاع ({sample.SnrDb.Value} dB) تحت الحد الأدنى ({snrMinThreshold} dB).",
                cancellationToken);
        }

        if (sample.CcqPercent.HasValue && sample.CcqPercent.Value < ccqMinThreshold)
        {
            await CreateThresholdEventIfNeededAsync(
                sector.Id,
                sample.Id,
                "CCQ",
                sample.CcqPercent.Value,
                ccqMinThreshold,
                $"انخفاض CCQ في القطاع ({sample.CcqPercent.Value}%) تحت الحد الأدنى ({ccqMinThreshold}%).",
                cancellationToken);
        }
    }

    private async Task CreateThresholdEventIfNeededAsync(
        int sectorId,
        long sampleId,
        string metricName,
        decimal metricValue,
        decimal thresholdValue,
        string message,
        CancellationToken cancellationToken)
    {
        var cooldownFrom = DateTime.Now.AddMinutes(-15);
        var recentExists = await _db.SectorRadioEvents
            .AsNoTracking()
            .AnyAsync(e =>
                e.SectorId == sectorId &&
                e.EventType == "Threshold" &&
                e.MetricName == metricName &&
                e.CreatedAt >= cooldownFrom,
                cancellationToken);

        if (recentExists)
        {
            return;
        }

        var eventItem = new SectorRadioEvent
        {
            SectorId = sectorId,
            MetricSampleId = sampleId,
            Severity = "Warning",
            EventType = "Threshold",
            MetricName = metricName,
            MetricValue = metricValue,
            ThresholdValue = thresholdValue,
            Message = message,
            IsAcknowledged = false,
            CreatedAt = DateTime.Now
        };

        _db.SectorRadioEvents.Add(eventItem);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> EnqueueReadySectorsAsync(
        ISectorRadioMetricsQueue queue,
        int? networkId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Sectors
            .AsNoTracking()
            .Where(s => s.IsActive && s.MikroTikServerId > 0);

        if (networkId.HasValue)
        {
            query = query.Where(s => s.NetworkId == networkId.Value);
        }

        var ready = await query
            .Select(s => new { s.Id, s.MikroTikServerId })
            .ToListAsync(cancellationToken);

        foreach (var item in ready)
        {
            await queue.EnqueueAsync(new SectorRadioMetricsJob
            {
                SectorId = item.Id,
                MikroTikServerId = item.MikroTikServerId
            }, cancellationToken);
        }

        return ready.Count;
    }
}
