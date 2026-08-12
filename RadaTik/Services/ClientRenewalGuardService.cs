using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services;

public sealed class RenewalBlockResult
{
    public bool CanRenew { get; init; }
    public int PendingInvoicesCount { get; init; }
    public decimal TotalOutstanding { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

public interface IClientRenewalGuardService
{
    Task<RenewalBlockResult> CheckBlockingInvoicesAsync(int clientId, CancellationToken ct = default);
}

public sealed class ClientRenewalGuardService : IClientRenewalGuardService
{
    private sealed record PendingInvoiceSnapshot(int Id, decimal GrossAmount);

    private readonly ApplicationDbContext _db;

    public ClientRenewalGuardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RenewalBlockResult> CheckBlockingInvoicesAsync(int clientId, CancellationToken ct = default)
    {
        List<PendingInvoiceSnapshot> pending = await _db.MaintenanceInvoices
            .AsNoTracking()
            .Where(i => i.ClientId == clientId && i.Status == MaintenanceInvoiceStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new PendingInvoiceSnapshot(i.Id, i.GrossAmount))
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return new RenewalBlockResult { CanRenew = true };
        }

        return new RenewalBlockResult
        {
            CanRenew = false,
            PendingInvoicesCount = pending.Count,
            TotalOutstanding = pending.Sum(x => x.GrossAmount),
            Reasons = pending.Select(x => $"فاتورة صيانة رقم #{x.Id}").ToList()
        };
    }
}
