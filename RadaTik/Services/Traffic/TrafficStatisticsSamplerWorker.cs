using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services.Traffic;

/// <summary>
/// Samples per-server RX/TX rates periodically and stores them for historical analytics.
/// </summary>
public sealed class TrafficStatisticsSamplerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrafficStatisticsSamplerWorker> _logger;
    private readonly TimeSpan _sampleInterval;
    private readonly TimeSpan _retention;

    public TrafficStatisticsSamplerWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TrafficStatisticsSamplerWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var sampleMinutes = configuration.GetValue("Traffic:StatsSampleIntervalMinutes", 1);
        sampleMinutes = Math.Clamp(sampleMinutes, 1, 60);
        _sampleInterval = TimeSpan.FromMinutes(sampleMinutes);

        var retentionDays = configuration.GetValue("Traffic:StatsRetentionDays", 120);
        retentionDays = Math.Clamp(retentionDays, 7, 3650);
        _retention = TimeSpan.FromDays(retentionDays);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Traffic statistics sampler started. Interval={Interval}, Retention={RetentionDays} days",
            _sampleInterval,
            _retention.TotalDays);

        using var timer = new PeriodicTimer(_sampleInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await SampleAndPersistAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Traffic statistics sampling cycle failed.");
            }
        }
    }

    private async Task SampleAndPersistAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reader = scope.ServiceProvider.GetRequiredService<MikroTikTrafficSnapshotReader>();
        var utcNow = DateTime.UtcNow;

        var servers = await db.MikroTikServers.AsNoTracking()
            .Where(s => s.IsActive && s.NetworkId.HasValue)
            .Select(s => new MikroTikServer
            {
                Id = s.Id,
                Name = s.Name,
                Host = s.Host,
                Port = s.Port,
                User = s.User,
                Pass = s.Pass,
                NetworkId = s.NetworkId,
                IsActive = s.IsActive,
            })
            .ToListAsync(cancellationToken);

        if (servers.Count == 0)
        {
            return;
        }

        var toInsert = new List<MikroTikServerTrafficSample>(servers.Count);
        foreach (var server in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (server.NetworkId is not int networkId)
            {
                continue;
            }

            try
            {
                var snapshot = reader.BuildSnapshot(server, networkId, streamKey: "stats");
                var rxBps = snapshot.Interfaces.Sum(i => Math.Max(0d, i.RxBps));
                var txBps = snapshot.Interfaces.Sum(i => Math.Max(0d, i.TxBps));

                toInsert.Add(new MikroTikServerTrafficSample
                {
                    NetworkId = networkId,
                    MikroTikServerId = server.Id,
                    CapturedAtUtc = utcNow,
                    InterfaceCount = snapshot.Interfaces.Count,
                    RxBps = rxBps,
                    TxBps = txBps,
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Traffic statistics sample failed for server {ServerId} ({Host})",
                    server.Id,
                    server.Host);
            }
        }

        if (toInsert.Count == 0)
        {
            return;
        }

        db.MikroTikServerTrafficSamples.AddRange(toInsert);

        var cutoff = utcNow - _retention;
        await db.MikroTikServerTrafficSamples
            .Where(s => s.CapturedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
