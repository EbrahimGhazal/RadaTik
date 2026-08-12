using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models.Business;

namespace RadaTik.Helpers;

public static class ErpLookupHelper
{
    public static async Task<List<SelectListItem>> GetActiveCustomersAsync(
        ApplicationDbContext context,
        int companyNetworkId,
        CancellationToken ct = default)
    {
        return await context.ErpCustomers.AsNoTracking()
            .Where(c => c.CompanyNetworkId == companyNetworkId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync(ct);
    }

    public static async Task<List<SelectListItem>> GetActiveSuppliersAsync(
        ApplicationDbContext context,
        int companyNetworkId,
        CancellationToken ct = default)
    {
        return await context.ErpSuppliers.AsNoTracking()
            .Where(s => s.CompanyNetworkId == companyNetworkId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
            .ToListAsync(ct);
    }
}
