using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;

namespace RadTik.Services;

/// <summary>
/// خصم وتجديد تلقائي لاشتراكات الخدمات بحسب مدة الاستحقاق ووحدة التسعير (لكل مستخدم/قطاع/..).
/// الفكرة: عند انتهاء الاشتراك (ExpiresAt) يتم خصم قيمة الدورة التالية من رصيد الشركة وتمديد ExpiresAt.
/// في حال عدم كفاية الرصيد يتم تعليق الاشتراك.
/// يشمل الاشتراكات المعلّقة المستحقة (Suspended) ليعاد المحاولة بعد شحن المحفظة دون انتظار يدوي.
/// </summary>
public sealed class NetworkSubscriptionBillingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NetworkSubscriptionBillingBackgroundService> _logger;

    public NetworkSubscriptionBillingBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NetworkSubscriptionBillingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var usageChargeService = scope.ServiceProvider.GetRequiredService<IUsageBasedSubscriptionChargeService>();
                var renewalProcessor = scope.ServiceProvider.GetRequiredService<NetworkSubscriptionRenewalProcessor>();

                var now = DateTime.Now;

                var due = await db.NetworkServiceSubscriptions
                    .Where(s =>
                        s.BillingPeriod != PricingBillingPeriod.OneTime &&
                        (s.Status == NetworkServiceSubscriptionStatus.Active ||
                         s.Status == NetworkServiceSubscriptionStatus.Expired ||
                         s.Status == NetworkServiceSubscriptionStatus.Suspended) &&
                        s.ExpiresAt <= now)
                    .OrderBy(s => s.ExpiresAt)
                    .Take(200)
                    .ToListAsync(stoppingToken);

                if (due.Count > 0)
                {
                    foreach (var sub in due)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        await renewalProcessor.ProcessSubscriptionRenewalAsync(db, sub.Id, now, stoppingToken);
                    }
                }

                var activeCompanyIds = await db.NetworkServiceSubscriptions
                    .Where(s => s.Status == NetworkServiceSubscriptionStatus.Active)
                    .Select(s => s.NetworkId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                foreach (var companyId in activeCompanyIds)
                {
                    var actorId = await usageChargeService.ResolveActorUserIdAsync(companyId, stoppingToken);
                    await usageChargeService.ChargeUsageIncreaseAsync(companyId, actorId, null, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process subscription billing.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}
