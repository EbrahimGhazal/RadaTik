namespace RadaTik.Services.Profiles;

public sealed class ProfileBulkPriceUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int UpdatedCount { get; init; }
    public int RequestedCount { get; init; }
    public int SkippedCount { get; init; }

    public static ProfileBulkPriceUpdateResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static ProfileBulkPriceUpdateResult Ok(
        int updated,
        int requested,
        int skipped,
        string message) =>
        new()
        {
            Success = true,
            UpdatedCount = updated,
            RequestedCount = requested,
            SkippedCount = skipped,
            Message = message
        };
}

public interface IProfileBulkPricingService
{
    Task<ProfileBulkPriceUpdateResult> BulkSetPriceAsync(
        int networkId,
        IReadOnlyList<int> profileIds,
        decimal newPrice,
        string changedBy,
        string? reason,
        CancellationToken ct = default);
}
