namespace RadaTik.Services.SectorRadio;

public sealed class SectorRadioMetricsSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISectorRadioMetricsQueue _queue;
    private readonly ILogger<SectorRadioMetricsSchedulerService> _logger;

    public SectorRadioMetricsSchedulerService(
        IServiceScopeFactory scopeFactory,
        ISectorRadioMetricsQueue queue,
        ILogger<SectorRadioMetricsSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PoC polling interval (can move to configuration later).
        TimeSpan interval = TimeSpan.FromMinutes(3);
        _logger.LogInformation("Sector radio metrics scheduler started. Interval={Interval}", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                SectorRadioMetricsCollector collector = scope.ServiceProvider.GetRequiredService<SectorRadioMetricsCollector>();
                int count = await collector.EnqueueReadySectorsAsync(_queue, null, stoppingToken);
                _logger.LogInformation("Sector radio polling cycle enqueued {Count} jobs.", count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sector radio polling scheduler failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
