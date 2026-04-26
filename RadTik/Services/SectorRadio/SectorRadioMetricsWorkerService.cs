using RadTik.Data;

namespace RadTik.Services.SectorRadio;

public sealed class SectorRadioMetricsWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISectorRadioMetricsQueue _queue;
    private readonly ILogger<SectorRadioMetricsWorkerService> _logger;

    public SectorRadioMetricsWorkerService(
        IServiceScopeFactory scopeFactory,
        ISectorRadioMetricsQueue queue,
        ILogger<SectorRadioMetricsWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sector radio metrics worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var collector = scope.ServiceProvider.GetRequiredService<SectorRadioMetricsCollector>();
                await collector.CollectForJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sector radio worker failed to process job.");
            }
        }
    }
}
