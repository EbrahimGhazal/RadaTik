namespace RadaTik.Services.PublicStats;

public interface IPublicStatsService
{
    Task<long> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default);
}
