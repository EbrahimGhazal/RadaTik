using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed class EmployeeDashboardQueryService(ApplicationDbContext context)
    : ApplicationServiceBase(context), IEmployeeDashboardQueryService
{
    public Task<List<Client>> GetPendingInstallationsUntilAsync(
        int networkId,
        DateTime dateInclusive,
        CancellationToken ct = default)
    {
        DateTime endOfDay = dateInclusive.Date.AddDays(1);
        return PendingInstallations(networkId)
            .Where(client => client.CreatedDate < endOfDay)
            .OrderBy(client => client.CreatedDate)
            .ToListAsync(ct);
    }

    public Task<List<Client>> GetPendingInstallationsOnDateAsync(
        int networkId,
        DateTime date,
        CancellationToken ct = default)
    {
        DateTime dayStart = date.Date;
        DateTime dayEnd = dayStart.AddDays(1);
        return PendingInstallations(networkId)
            .Where(client => client.CreatedDate >= dayStart && client.CreatedDate < dayEnd)
            .OrderBy(client => client.CreatedDate)
            .ToListAsync(ct);
    }

    private IQueryable<Client> PendingInstallations(int networkId) =>
        Db.Clients
            .AsNoTracking()
            .Include(client => client.Profile)
            .Where(client => client.NetworkId == networkId)
            .WherePendingInstallation(Db.SubscriberInstallationInvoices);
}
