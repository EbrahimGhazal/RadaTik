using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services
{
    /// <summary>
    /// Creates daily in-app notifications for subscriptions expiring within 30 days.
    /// </summary>
    public sealed class SubscriptionExpiryNotificationsBackgroundService : BackgroundService
    {
        private sealed record NetworkManagerNotifyRow(int Id, string? Name, string? ManagerUserId);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionExpiryNotificationsBackgroundService> _logger;

        public SubscriptionExpiryNotificationsBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionExpiryNotificationsBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
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
                    ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    DateTime now = DateTime.Now;
                    DateTime billingDueDate = now.Date.AddDays(3);

                    List<NetworkServiceSubscription> dueForBillingSoon = await db.NetworkServiceSubscriptions
                        .AsNoTracking()
                        .Where(s =>
                            s.Status == NetworkServiceSubscriptionStatus.Active &&
                            s.BillingPeriod != PricingBillingPeriod.OneTime &&
                            s.ExpiresAt.Date == billingDueDate)
                        .ToListAsync(stoppingToken);

                    if (dueForBillingSoon.Count > 0)
                    {
                        List<int> networkIds = dueForBillingSoon.Select(s => s.NetworkId).Distinct().ToList();
                        List<NetworkManagerNotifyRow> networks = await db.Networks.AsNoTracking()
                            .Where(n => networkIds.Contains(n.Id))
                            .Select(n => new NetworkManagerNotifyRow(n.Id, n.Name, n.ManagerUserId))
                            .ToListAsync(stoppingToken);

                        Dictionary<int, string> mgrByNetworkId = networks
                            .Where(n => !string.IsNullOrWhiteSpace(n.ManagerUserId))
                            .ToDictionary(n => n.Id, n => n.ManagerUserId!, EqualityComparer<int>.Default);
                        HashSet<string> keys = dueForBillingSoon
                            .Select(s => $"SubBillDue3d:{s.Id}:{billingDueDate:yyyyMMdd}")
                            .ToHashSet(StringComparer.Ordinal);
                        HashSet<string> existingKeys = await db.UserNotifications
                            .AsNoTracking()
                            .Where(n => keys.Contains(n.Key))
                            .Select(n => n.Key)
                            .ToHashSetAsync(stoppingToken);

                        foreach (NetworkServiceSubscription? s in dueForBillingSoon)
                        {
                            if (!mgrByNetworkId.TryGetValue(s.NetworkId, out string? managerUserId))
                            {
                                continue;
                            }

                            string title = "تنبيه: قرب استحقاق رسم الخدمة";
                            string msg =
                                $"الخدمة ({s.FeatureKey}) ستُخصم دورياً خلال 3 أيام. تاريخ الاستحقاق: {s.ExpiresAt:yyyy/MM/dd HH:mm}. تأكد من رصيد المحفظة.";

                            string key = $"SubBillDue3d:{s.Id}:{billingDueDate:yyyyMMdd}";

                            if (!existingKeys.Contains(key))
                            {
                                db.UserNotifications.Add(new UserNotification
                                {
                                    Key = key,
                                    UserId = managerUserId,
                                    NetworkId = s.NetworkId,
                                    Type = NotificationType.SubscriptionExpiring,
                                    Title = title,
                                    Message = msg,
                                    NetworkServiceSubscriptionId = s.Id,
                                    CreatedAt = now,
                                    IsRead = false
                                });
                                existingKeys.Add(key);
                            }
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate expiry notifications.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}

