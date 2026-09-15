namespace RadaTik.Services.Clients;

/// <summary>
/// يجمع جلسات /ppp/active في الخلفية حتى لا تنتظر صفحة العملاء أجهزة MikroTik.
/// </summary>
public sealed class ClientLiveConnectionBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ClientLiveConnectionBackgroundService> logger) : BackgroundService
{
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ClientLiveConnectionBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("بدء خدمة تحديث حالة اتصال المشتركين من MikroTik");

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IClientLiveConnectionRefreshService refresh =
                    scope.ServiceProvider.GetRequiredService<IClientLiveConnectionRefreshService>();
                await refresh.RefreshAllActiveServersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "فشل تحديث حالة الاتصال من MikroTik");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
