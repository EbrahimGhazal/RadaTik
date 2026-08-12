using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed class ClientExpirationQueryService(ApplicationDbContext context)
    : ApplicationServiceBase(context), IClientExpirationQueryService
{
    public async Task<ClientExpiredAccountsPageModel> BuildExpiredAccountsPageAsync(int networkId, CancellationToken ct = default)
    {
        DateTime today = DateTime.Now.Date;
        List<Client> expiredAccounts = await Db.Clients
            .Where(c => c.NetworkId == networkId
                && c.AccountExpirationDate.HasValue
                && c.AccountExpirationDate.Value.Date < today)
            .Include(c => c.Profile)
            .Include(c => c.MikroTikServer)
            .Include(c => c.Receiver)
            .OrderBy(c => c.AccountExpirationDate)
            .ToListAsync(ct);

        return new ClientExpiredAccountsPageModel
        {
            Accounts = expiredAccounts,
            TotalExpired = expiredAccounts.Count,
            ActiveExpired = expiredAccounts.Count(c => c.IsActive),
            DisabledExpired = expiredAccounts.Count(c => !c.IsActive)
        };
    }

    public async Task<ClientExpiringSoonPageModel> BuildExpiringIn3DaysPageAsync(int networkId, CancellationToken ct = default)
    {
        DateTime today = DateTime.Now.Date;
        DateTime in3Days = today.AddDays(3);

        List<Client> expiringAccounts = await Db.Clients
            .Where(c => c.NetworkId == networkId
                && c.AccountExpirationDate.HasValue
                && c.AccountExpirationDate.Value.Date >= today
                && c.AccountExpirationDate.Value.Date <= in3Days
                && c.IsActive)
            .Include(c => c.Profile)
            .Include(c => c.MikroTikServer)
            .Include(c => c.Receiver)
            .OrderBy(c => c.AccountExpirationDate)
            .ToListAsync(ct);

        return new ClientExpiringSoonPageModel
        {
            Accounts = expiringAccounts,
            TotalExpiring = expiringAccounts.Count,
            ExpiringToday = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today),
            ExpiringTomorrow = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today.AddDays(1)),
            ExpiringIn2Days = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today.AddDays(2)),
            ExpiringIn3Days = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today.AddDays(3))
        };
    }
}
