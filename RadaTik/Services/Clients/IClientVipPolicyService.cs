using RadaTik.Models;

namespace RadaTik.Services.Clients;

public interface IClientVipPolicyService
{
    Task<CompanyVipPolicy> GetCompanyPolicyAsync(int? networkId, CancellationToken ct = default);

    Task<decimal> ApplyPackageDiscountAsync(decimal basePrice, Client client, CancellationToken ct = default);

    Task<(decimal BasePrice, decimal VatAmount, decimal Total)> ApplyMonthlyPriceAsync(
        Client client,
        CancellationToken ct = default);

    Task<bool> IsProtectedFromAutoDisableAsync(Client client, DateTime now, CancellationToken ct = default);
}
