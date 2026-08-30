using Microsoft.EntityFrameworkCore;
using RadaTik.Data;

namespace RadaTik.Services.PublicStats;

public sealed class PublicStatsService(ApplicationDbContext db, ILogger<PublicStatsService> logger) : IPublicStatsService
{
    public async Task<long> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.PublicSiteCounters.AsNoTracking()
                .Where(item => item.Key == key)
                .Select(item => item.Count)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "تعذر قراءة العداد {Key}", key);
            return 0;
        }
    }

    public async Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            int updated = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE [dbo].[PublicSiteCounters]
                SET [Count] = [Count] + 1, [UpdatedUtc] = SYSUTCDATETIME()
                WHERE [Key] = {key};
                """,
                cancellationToken);

            if (updated == 0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[PublicSiteCounters] WHERE [Key] = {key})
                    INSERT INTO [dbo].[PublicSiteCounters] ([Key], [Count], [UpdatedUtc])
                    VALUES ({key}, 1, SYSUTCDATETIME());
                    """,
                    cancellationToken);
            }

            return await GetAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "تعذر زيادة العداد {Key}", key);
            return 0;
        }
    }
}
