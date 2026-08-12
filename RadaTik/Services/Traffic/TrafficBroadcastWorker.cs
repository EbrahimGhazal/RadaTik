using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Hubs;
using RadaTik.Models;

namespace RadaTik.Services.Traffic;

/// <summary>Polls MikroTik for active SignalR groups on a fixed interval (default 500ms, configurable).</summary>
public sealed class TrafficBroadcastWorker : BackgroundService
{
    private readonly TimeSpan _tick;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITrafficMonitoringCoordinator _coordinator;
    private readonly IHubContext<TrafficHub> _hub;
    private readonly ILogger<TrafficBroadcastWorker> _logger;

    public TrafficBroadcastWorker(
        IServiceScopeFactory scopeFactory,
        ITrafficMonitoringCoordinator coordinator,
        IHubContext<TrafficHub> hub,
        ILogger<TrafficBroadcastWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _hub = hub;
        _logger = logger;
        var ms = configuration.GetValue("Traffic:PollIntervalMilliseconds", 500);
        ms = Math.Clamp(ms, 50, 60_000);
        _tick = TimeSpan.FromMilliseconds(ms);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_tick);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var targets = _coordinator.GetActiveTargets();
            if (targets.Count == 0)
            {
                continue;
            }

            foreach (var (networkId, serverId) in targets)
            {
                await BroadcastOneAsync(networkId, serverId, stoppingToken);
            }
        }
    }

    private async Task BroadcastOneAsync(int networkId, int serverId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reader = scope.ServiceProvider.GetRequiredService<MikroTikTrafficSnapshotReader>();

            var server = await db.MikroTikServers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId && s.IsActive, ct);

            if (server == null)
            {
                return;
            }

            var snapshot = reader.BuildSnapshot(server, networkId);
            var group = TrafficHub.GroupName(networkId, serverId);
            await _hub.Clients.Group(group).SendAsync(TrafficHub.TrafficUpdateMethod, snapshot, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Traffic broadcast failed for network {NetworkId} server {ServerId}", networkId, serverId);
            try
            {
                var group = TrafficHub.GroupName(networkId, serverId);
                await _hub.Clients.Group(group).SendAsync(
                    TrafficHub.TrafficErrorMethod,
                    new { message = ex.Message },
                    ct);
            }
            catch (Exception sendEx)
            {
                _logger.LogDebug(sendEx, "Failed to push traffic error to clients");
            }
        }
    }
}
