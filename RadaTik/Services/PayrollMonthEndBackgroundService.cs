namespace RadaTik.Services;

/// <summary>في الساعة 23:00 من آخر يوم في الشهر — تجهيز سجلات الرواتب (أساسي مُناسَب + مكافآت/حسومات الشهر).</summary>
public sealed class PayrollMonthEndBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PayrollMonthEndBackgroundService> _logger;

    public PayrollMonthEndBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PayrollMonthEndBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (PayrollMonthEndAccrualService.IsMonthEndAccrualWindow(DateTime.Now))
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    PayrollMonthEndAccrualService accrual = scope.ServiceProvider
                        .GetRequiredService<PayrollMonthEndAccrualService>();
                    await accrual.RunAllCompaniesIfDueAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Payroll month-end accrual failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
