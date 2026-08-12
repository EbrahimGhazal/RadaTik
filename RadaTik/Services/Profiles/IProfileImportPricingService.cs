namespace RadaTik.Services.Profiles;

public interface IProfileImportPricingService
{
    Task<decimal> GetProfileImportUnitPriceAsync(CancellationToken ct = default);

    Task<decimal> GetCompanyWalletBalanceAsync(int companyNetworkId, CancellationToken ct = default);

    Task<ProfileImportChargeEstimate> CalculateProfileChargeAsync(
        int companyNetworkId,
        int unitsCount,
        CancellationToken ct = default);
}

public sealed class ProfileImportChargeEstimate
{
    public decimal UnitPrice { get; init; }
    public decimal TotalCharge { get; init; }
    public decimal WalletBalance { get; init; }
    public bool HasSufficientBalance { get; init; }
}
