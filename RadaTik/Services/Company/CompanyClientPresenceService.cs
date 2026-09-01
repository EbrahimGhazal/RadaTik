using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services.Company;

public sealed class CompanyClientPresenceService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager)
    : ApplicationServiceBase(context), ICompanyClientPresenceService
{
    private const int MaxItemsPerCompany = 20;

    public async Task<CompanyClientPresenceSnapshot> GetForCurrentClientAsync(
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return new CompanyClientPresenceSnapshot();
        }

        ApplicationUser? appUser = await userManager.GetUserAsync(user);
        if (appUser?.ClientId is not int clientId)
        {
            return new CompanyClientPresenceSnapshot();
        }

        var client = await Db.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new { c.NetworkId, ServerNetworkId = c.MikroTikServer != null ? (int?)c.MikroTikServer.NetworkId : null })
            .FirstOrDefaultAsync(ct);

        int? networkId = client?.NetworkId ?? client?.ServerNetworkId;
        if (!networkId.HasValue)
        {
            return new CompanyClientPresenceSnapshot();
        }

        int companyId = await CompanyFinancialHelper.ResolveCompanyNetworkIdAsync(Db, networkId.Value, ct);
        string companyName = await Db.Networks.AsNoTracking()
            .Where(n => n.Id == companyId)
            .Select(n => n.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        List<CompanySocialLink> social = await Db.CompanySocialLinks.AsNoTracking()
            .Where(x => x.CompanyNetworkId == companyId && x.IsVisibleToClients)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        List<CompanyComplaintContact> complaints = await Db.CompanyComplaintContacts.AsNoTracking()
            .Where(x => x.CompanyNetworkId == companyId && x.IsVisibleToClients)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        return new CompanyClientPresenceSnapshot
        {
            CompanyName = companyName,
            VisibleSocialLinks = social,
            VisibleComplaintContacts = complaints
        };
    }

    public async Task<CompanyClientPresenceAdminPage?> GetAdminPageAsync(
        int selectedNetworkId,
        string? tab,
        CancellationToken ct = default)
    {
        int? companyId = await ResolveCompanyIdAsync(selectedNetworkId, ct);
        if (!companyId.HasValue)
        {
            return null;
        }

        string companyName = await Db.Networks.AsNoTracking()
            .Where(n => n.Id == companyId.Value)
            .Select(n => n.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return new CompanyClientPresenceAdminPage
        {
            CompanyNetworkId = companyId.Value,
            CompanyName = companyName,
            Tab = tab == "complaints" ? "complaints" : "social",
            SocialLinks = await Db.CompanySocialLinks.AsNoTracking()
                .Where(x => x.CompanyNetworkId == companyId.Value)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(ct),
            ComplaintContacts = await Db.CompanyComplaintContacts.AsNoTracking()
                .Where(x => x.CompanyNetworkId == companyId.Value)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(ct)
        };
    }

    public async Task<(bool Ok, string Message)> AddSocialAsync(
        int selectedNetworkId,
        CompanySocialLinkSaveCommand command,
        CancellationToken ct = default)
    {
        int? companyId = await ResolveCompanyIdAsync(selectedNetworkId, ct);
        if (!companyId.HasValue)
        {
            return (false, "تعذر تحديد الشركة.");
        }

        if (!TryBuildSocial(command, out CompanySocialLink? row, out string? error) || row == null)
        {
            return (false, error ?? "بيانات غير صالحة.");
        }

        int count = await Db.CompanySocialLinks.CountAsync(x => x.CompanyNetworkId == companyId.Value, ct);
        if (count >= MaxItemsPerCompany)
        {
            return (false, "بلغ الحد الأقصى لصفحات السوشال ميديا.");
        }

        int nextOrder = count == 0
            ? 1
            : await Db.CompanySocialLinks.Where(x => x.CompanyNetworkId == companyId.Value).MaxAsync(x => x.SortOrder, ct) + 1;

        row.CompanyNetworkId = companyId.Value;
        row.SortOrder = nextOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        Db.CompanySocialLinks.Add(row);
        await Db.SaveChangesAsync(ct);
        return (true, "تمت إضافة صفحة السوشال ميديا.");
    }

    public async Task<(bool Ok, string Message)> UpdateSocialAsync(
        int selectedNetworkId,
        int id,
        CompanySocialLinkSaveCommand command,
        CancellationToken ct = default)
    {
        CompanySocialLink? row = await FindSocialAsync(selectedNetworkId, id, ct);
        if (row == null)
        {
            return (false, "الرابط غير موجود.");
        }

        if (!TryBuildSocial(command, out CompanySocialLink? parsed, out string? error) || parsed == null)
        {
            return (false, error ?? "بيانات غير صالحة.");
        }

        row.Platform = parsed.Platform;
        row.DisplayName = parsed.DisplayName;
        row.Url = parsed.Url;
        row.IsVisibleToClients = parsed.IsVisibleToClients;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return (true, "تم حفظ رابط السوشال ميديا.");
    }

    public async Task<(bool Ok, string Message)> ToggleSocialAsync(
        int selectedNetworkId,
        int id,
        CancellationToken ct = default)
    {
        CompanySocialLink? row = await FindSocialAsync(selectedNetworkId, id, ct);
        if (row == null)
        {
            return (false, "الرابط غير موجود.");
        }

        row.IsVisibleToClients = !row.IsVisibleToClients;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return (true, row.IsVisibleToClients ? "سيظهر الرابط للمشترك." : "تم إخفاء الرابط عن المشترك.");
    }

    public async Task<(bool Ok, string Message)> DeleteSocialAsync(
        int selectedNetworkId,
        int id,
        CancellationToken ct = default)
    {
        CompanySocialLink? row = await FindSocialAsync(selectedNetworkId, id, ct);
        if (row == null)
        {
            return (false, "الرابط غير موجود.");
        }

        Db.CompanySocialLinks.Remove(row);
        await Db.SaveChangesAsync(ct);
        return (true, "تم حذف رابط السوشال ميديا.");
    }

    public async Task<(bool Ok, string Message)> AddComplaintAsync(
        int selectedNetworkId,
        CompanyComplaintContactSaveCommand command,
        CancellationToken ct = default)
    {
        int? companyId = await ResolveCompanyIdAsync(selectedNetworkId, ct);
        if (!companyId.HasValue)
        {
            return (false, "تعذر تحديد الشركة.");
        }

        if (!TryBuildComplaint(command, out CompanyComplaintContact? row, out string? error) || row == null)
        {
            return (false, error ?? "بيانات غير صالحة.");
        }

        int count = await Db.CompanyComplaintContacts.CountAsync(x => x.CompanyNetworkId == companyId.Value, ct);
        if (count >= MaxItemsPerCompany)
        {
            return (false, "بلغ الحد الأقصى لأرقام الشكاوى.");
        }

        int nextOrder = count == 0
            ? 1
            : await Db.CompanyComplaintContacts.Where(x => x.CompanyNetworkId == companyId.Value).MaxAsync(x => x.SortOrder, ct) + 1;

        row.CompanyNetworkId = companyId.Value;
        row.SortOrder = nextOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        Db.CompanyComplaintContacts.Add(row);
        await Db.SaveChangesAsync(ct);
        return (true, "تمت إضافة رقم الشكاوى.");
    }

    public async Task<(bool Ok, string Message)> UpdateComplaintAsync(
        int selectedNetworkId,
        int id,
        CompanyComplaintContactSaveCommand command,
        CancellationToken ct = default)
    {
        CompanyComplaintContact? row = await FindComplaintAsync(selectedNetworkId, id, ct);
        if (row == null)
        {
            return (false, "الرقم غير موجود.");
        }

        if (!TryBuildComplaint(command, out CompanyComplaintContact? parsed, out string? error) || parsed == null)
        {
            return (false, error ?? "بيانات غير صالحة.");
        }

        row.Label = parsed.Label;
        row.PhoneNumber = parsed.PhoneNumber;
        row.IsVisibleToClients = parsed.IsVisibleToClients;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return (true, "تم حفظ رقم الشكاوى.");
    }

    public async Task<(bool Ok, string Message)> ToggleComplaintAsync(
        int selectedNetworkId,
        int id,
        CancellationToken ct = default)
    {
        CompanyComplaintContact? row = await FindComplaintAsync(selectedNetworkId, id, ct);
        if (row == null)
        {
            return (false, "الرقم غير موجود.");
        }

        row.IsVisibleToClients = !row.IsVisibleToClients;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return (true, row.IsVisibleToClients ? "سيظهر الرقم للمشترك." : "تم إخفاء الرقم عن المشترك.");
    }

    public async Task<(bool Ok, string Message)> DeleteComplaintAsync(
        int selectedNetworkId,
        int id,
        CancellationToken ct = default)
    {
        CompanyComplaintContact? row = await FindComplaintAsync(selectedNetworkId, id, ct);
        if (row == null)
        {
            return (false, "الرقم غير موجود.");
        }

        Db.CompanyComplaintContacts.Remove(row);
        await Db.SaveChangesAsync(ct);
        return (true, "تم حذف رقم الشكاوى.");
    }

    private async Task<int?> ResolveCompanyIdAsync(int selectedNetworkId, CancellationToken ct)
    {
        Network? selected = await Db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
        return selected == null ? null : selected.ParentNetworkId ?? selected.Id;
    }

    private async Task<CompanySocialLink?> FindSocialAsync(int selectedNetworkId, int id, CancellationToken ct)
    {
        int? companyId = await ResolveCompanyIdAsync(selectedNetworkId, ct);
        if (!companyId.HasValue)
        {
            return null;
        }

        return await Db.CompanySocialLinks
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyNetworkId == companyId.Value, ct);
    }

    private async Task<CompanyComplaintContact?> FindComplaintAsync(int selectedNetworkId, int id, CancellationToken ct)
    {
        int? companyId = await ResolveCompanyIdAsync(selectedNetworkId, ct);
        if (!companyId.HasValue)
        {
            return null;
        }

        return await Db.CompanyComplaintContacts
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyNetworkId == companyId.Value, ct);
    }

    private static bool TryBuildSocial(
        CompanySocialLinkSaveCommand command,
        out CompanySocialLink? row,
        out string? error)
    {
        row = null;
        string candidate = SocialMediaCatalog.NormalizeSocialUrl(command.Platform, command.Url);
        if (!SocialMediaCatalog.TryNormalizeHttpUrl(candidate, out string url, out error))
        {
            return false;
        }

        string name = string.IsNullOrWhiteSpace(command.DisplayName)
            ? SocialMediaCatalog.DefaultDisplayName(command.Platform)
            : command.DisplayName.Trim();
        if (name.Length > 80)
        {
            error = "الاسم يجب ألا يتجاوز 80 حرفاً.";
            return false;
        }

        row = new CompanySocialLink
        {
            Platform = command.Platform,
            DisplayName = name,
            Url = url,
            IsVisibleToClients = command.IsVisibleToClients
        };
        return true;
    }

    private static bool TryBuildComplaint(
        CompanyComplaintContactSaveCommand command,
        out CompanyComplaintContact? row,
        out string? error)
    {
        row = null;
        if (!SocialMediaCatalog.TryNormalizePhone(command.PhoneNumber, out string phone, out error))
        {
            return false;
        }

        string label = string.IsNullOrWhiteSpace(command.Label) ? "شكاوى الشركة" : command.Label.Trim();
        if (label.Length > 80)
        {
            error = "التسمية يجب ألا تتجاوز 80 حرفاً.";
            return false;
        }

        row = new CompanyComplaintContact
        {
            Label = label,
            PhoneNumber = phone,
            IsVisibleToClients = command.IsVisibleToClients
        };
        return true;
    }
}
