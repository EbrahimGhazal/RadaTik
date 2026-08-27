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
    public static decimal ResolveDiscountPercent(
        bool isVip,
        ClientVipBenefitKind benefitKind,
        decimal clientDiscountPercent,
        CompanyVipPolicy policy)
    {
        if (!isVip)
        {
            return 0m;
        }

        if (benefitKind == ClientVipBenefitKind.PermanentlyFree)
        {
            return 100m;
        }

        decimal percent = clientDiscountPercent > 0m ? clientDiscountPercent : policy.DiscountPercent;
        return Math.Clamp(percent, 0m, 100m);
    }

    public static decimal ApplyPackageDiscount(
        decimal basePrice,
        bool isVip,
        CompanyVipPolicy policy,
        ClientVipBenefitKind benefitKind = ClientVipBenefitKind.None,
        decimal clientDiscountPercent = 0m)
    {
        if (!isVip || basePrice <= 0m)
        {
            return basePrice;
        }

        decimal percent = ResolveDiscountPercent(isVip, benefitKind, clientDiscountPercent, policy);
        if (percent <= 0m)
        {
            return basePrice;
        }

        return Math.Round(basePrice * (1m - percent / 100m), 2, MidpointRounding.AwayFromZero);
    }

    public static (decimal BasePrice, decimal VatAmount, decimal Total) ApplyMonthlyPrice(
        decimal profilePrice,
        decimal vatPercent,
        bool isVip,
        CompanyVipPolicy policy,
        ClientVipBenefitKind benefitKind = ClientVipBenefitKind.None,
        decimal clientDiscountPercent = 0m)
    {
        decimal basePrice = ApplyPackageDiscount(profilePrice, isVip, policy, benefitKind, clientDiscountPercent);
        decimal vatAmount = Math.Round(basePrice * (vatPercent / 100m), 2, MidpointRounding.AwayFromZero);
        return (basePrice, vatAmount, basePrice + vatAmount);
    }

    public static bool IsProtectedFromAutoDisable(
        bool isVip,
        DateTime? expiration,
        CompanyVipPolicy policy,
        DateTime now,
        ClientVipBenefitKind benefitKind = ClientVipBenefitKind.None)
    {
        if (!isVip)
        {
            return false;
        }

        if (benefitKind == ClientVipBenefitKind.PermanentlyFree || policy.SkipAutoDisable)
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
    public static void Apply(
        Client target,
        bool isVip,
        string? note,
        DateTime now,
        ClientVipBenefitKind? benefitKind = null,
        decimal? discountPercent = null)
    {
        string? trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (!isVip)
        {
            target.IsVip = false;
            target.VipNote = null;
            target.VipSince = null;
            target.VipBenefitKind = ClientVipBenefitKind.None;
            target.VipDiscountPercent = 0m;
            return;
        }

        if (!target.IsVip)
        {
            target.VipSince = now;
        }

        target.IsVip = true;
        target.VipNote = trimmed;

        if (benefitKind.HasValue)
        {
            target.VipBenefitKind = benefitKind.Value == ClientVipBenefitKind.None
                ? ClientVipBenefitKind.Discount
                : benefitKind.Value;
        }
        else if (target.VipBenefitKind == ClientVipBenefitKind.None)
        {
            target.VipBenefitKind = ClientVipBenefitKind.Discount;
        }

        if (discountPercent.HasValue)
        {
            target.VipDiscountPercent = Math.Clamp(discountPercent.Value, 0m, 100m);
        }

        if (target.VipBenefitKind == ClientVipBenefitKind.PermanentlyFree)
        {
            target.VipDiscountPercent = 0m;
        }
    }

    public static void NormalizeNew(Client client, DateTime now)
    {
        Apply(
            client,
            client.IsVip,
            client.VipNote,
            now,
            client.VipBenefitKind,
            client.VipDiscountPercent);
        if (client.IsVip)
        {
            client.VipSince ??= now;
        }
    }
}

public static class ClientVipBenefitDisplay
{
    public static string BadgeText(Client client)
    {
        if (!client.IsVip)
        {
            return string.Empty;
        }

        if (client.VipBenefitKind == ClientVipBenefitKind.PermanentlyFree)
        {
            return "VIP · مجاني";
        }

        if (client.VipDiscountPercent > 0m)
        {
            return $"VIP · حسم {client.VipDiscountPercent:0.##}%";
        }

        return "VIP";
    }

    public static string DetailsText(Client client, decimal companyDefaultPercent)
    {
        if (!client.IsVip)
        {
            return "عادي";
        }

        if (client.VipBenefitKind == ClientVipBenefitKind.PermanentlyFree)
        {
            return "مجاني دائم";
        }

        decimal percent = client.VipDiscountPercent > 0m ? client.VipDiscountPercent : companyDefaultPercent;
        return percent > 0m
            ? $"حسم {percent:0.##}%"
            : "حسم (حسب سياسة الشركة)";
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
