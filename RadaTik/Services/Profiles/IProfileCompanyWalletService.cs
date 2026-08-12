namespace RadaTik.Services.Profiles;

public interface IProfileCompanyWalletService
{
    Task<decimal> ResolveSystemProfileVatPercentageAsync(CancellationToken ct = default);

    Task<decimal> ChargeCompanyForProfileUnitsAsync(
        int companyNetworkId,
        string actorUserId,
        int unitsCount,
        string note,
        CancellationToken ct = default);
}
