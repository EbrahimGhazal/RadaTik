using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services.Clients;

public sealed record CompanyVipPolicy(
    decimal DiscountPercent,
    int GraceDays,
    bool SkipAutoDisable)
{
    public static CompanyVipPolicy None { get; } = new(0m, 0, false);
}

public static class ClientVipPricing
{
    public static decimal ApplyPackageDiscount(decimal basePrice, bool isVip, CompanyVipPolicy policy)
    {
        if (!isVip || basePrice <= 0m || policy.DiscountPercent <= 0m)
        {
            return basePrice;
        }

        decimal percent = Math.Clamp(policy.DiscountPercent, 0m, 100m);
        return Math.Round(basePrice * (1m - percent / 100m), 2, MidpointRounding.AwayFromZero);
    }

    public static (decimal BasePrice, decimal VatAmount, decimal Total) ApplyMonthlyPrice(
        decimal profilePrice,
        decimal vatPercent,
        bool isVip,
        CompanyVipPolicy policy)
    {
        decimal basePrice = ApplyPackageDiscount(profilePrice, isVip, policy);
        decimal vatAmount = Math.Round(basePrice * (vatPercent / 100m), 2, MidpointRounding.AwayFromZero);
        return (basePrice, vatAmount, basePrice + vatAmount);
    }

    public static bool IsProtectedFromAutoDisable(
        bool isVip,
        DateTime? expiration,
        CompanyVipPolicy policy,
        DateTime now)
    {
        if (!isVip)
        {
            return false;
        }

        if (policy.SkipAutoDisable)
        {
            return true;
        }

        if (policy.GraceDays <= 0 || !expiration.HasValue)
        {
            return false;
        }

        return now < expiration.Value.AddDays(policy.GraceDays);
    }
}

public static class ClientVipAssignment
{
    public static void Apply(Client target, bool isVip, string? note, DateTime now)
    {
        string? trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (isVip)
        {
            if (!target.IsVip)
            {
                target.VipSince = now;
            }

            target.IsVip = true;
            target.VipNote = trimmed;
            return;
        }

        target.IsVip = false;
        target.VipNote = null;
        target.VipSince = null;
    }

    public static void NormalizeNew(Client client, DateTime now)
    {
        Apply(client, client.IsVip, client.VipNote, now);
        if (client.IsVip)
        {
            client.VipSince ??= now;
        }
    }
}

public static class CurrentClientVipLookup
{
    public static async Task<(bool IsVip, string? Note)> ResolveAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ClaimsPrincipal principal,
        CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true
            || !principal.IsInRole(RoleNames.Client))
        {
            return (false, null);
        }

        ApplicationUser? user = await userManager.GetUserAsync(principal);
        if (user?.ClientId == null)
        {
            return (false, null);
        }

        var row = await db.Clients.AsNoTracking()
            .Where(c => c.Id == user.ClientId.Value)
            .Select(c => new { c.IsVip, c.VipNote })
            .FirstOrDefaultAsync(ct);

        return row == null ? (false, null) : (row.IsVip, row.VipNote);
    }
}
