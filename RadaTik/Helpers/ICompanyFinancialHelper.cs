using RadaTik.Data;

namespace RadaTik.Helpers;

public interface ICompanyFinancialHelper
{
    Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken ct = default);

    Task<CompanyFinancialSnapshot> GetSnapshotAsync(int networkId, CancellationToken ct = default);
}

public sealed class CompanyFinancialService(ApplicationDbContext db) : ICompanyFinancialHelper
{
    public Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken ct = default) =>
        CompanyFinancialHelper.ResolveCompanyNetworkIdAsync(db, networkId, ct);

    public Task<CompanyFinancialSnapshot> GetSnapshotAsync(int networkId, CancellationToken ct = default) =>
        CompanyFinancialHelper.GetSnapshotAsync(db, networkId, ct);
}
