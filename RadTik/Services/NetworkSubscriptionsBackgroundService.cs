using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;

namespace RadTik.Services
{
    /// <summary>
    /// Background job to mark expired service subscriptions.
    /// Access control is enforced in FeatureAccessService by ExpiresAt, but this keeps DB status consistent.
    /// </summary>
    public sealed class NetworkSubscriptionsBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NetworkSubscriptionsBackgroundService> _logger;

        public NetworkSubscriptionsBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NetworkSubscriptionsBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Small initial delay to allow app startup/migrations to settle.
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var now = DateTime.Now;
                    var expired = await db.NetworkServiceSubscriptions
                        .Where(s => s.Status == NetworkServiceSubscriptionStatus.Active && s.ExpiresAt <= now)
                        .ToListAsync(stoppingToken);

                    if (expired.Count > 0)
                    {
                        foreach (var s in expired)
                        {
                            s.Status = NetworkServiceSubscriptionStatus.Expired;
                            s.UpdatedAt = now;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Marked {Count} subscriptions as expired.", expired.Count);
                    }
                }
                catch (Exception ex)
                {
                    // Keep service alive; DB might be unavailable temporarily.
                    _logger.LogWarning(ex, "Failed to process subscription expiry.");
                }

                // Run periodically (every 6 hours).
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
    }
}

