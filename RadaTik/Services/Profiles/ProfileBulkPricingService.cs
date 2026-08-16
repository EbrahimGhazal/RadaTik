using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;

namespace RadaTik.Services.Profiles;

public sealed class ProfileBulkPricingService(
    ApplicationDbContext context,
    IProfileCompanyWalletService profileCompanyWallet)
    : ApplicationServiceBase(context), IProfileBulkPricingService
{
    public async Task<ProfileBulkPriceUpdateResult> BulkSetPriceAsync(
        int networkId,
        IReadOnlyList<int> profileIds,
        decimal newPrice,
        string changedBy,
        string? reason,
        CancellationToken ct = default)
    {
        if (newPrice < 0)
        {
            return ProfileBulkPriceUpdateResult.Fail("السعر يجب أن يكون صفر أو أكبر.");
        }

        int[] ids = (profileIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return ProfileBulkPriceUpdateResult.Fail("لم يتم تحديد أي بروفايل.");
        }

        List<Profile> profiles = await Db.Profiles
            .Where(p => p.NetworkId == networkId && ids.Contains(p.Id))
            .ToListAsync(ct);

        if (profiles.Count == 0)
        {
            return ProfileBulkPriceUpdateResult.Fail("لا توجد بروفايلات مطابقة ضمن الشبكة الحالية.");
        }

        decimal systemVat = await profileCompanyWallet.ResolveSystemProfileVatPercentageAsync(ct);
        string changeReason = string.IsNullOrWhiteSpace(reason)
            ? "تعيين سعر موحّد لمجموعة سرعات"
            : reason.Trim();
        if (changeReason.Length > 200)
        {
            changeReason = changeReason[..200];
        }

        DateTime now = DateTime.Now;
        int updated = 0;
        int skipped = 0;

        foreach (Profile profile in profiles)
        {
            bool priceChanged = profile.Price != newPrice;
            bool vatChanged = profile.VATPercentage != systemVat;
            if (!priceChanged && !vatChanged)
            {
                skipped++;
                continue;
            }

            if (priceChanged)
            {
                Db.ProfilePriceHistories.Add(new ProfilePriceHistory
                {
                    ProfileId = profile.Id,
                    OldPrice = profile.Price,
                    NewPrice = newPrice,
                    OldVATPercentage = profile.VATPercentage,
                    NewVATPercentage = systemVat,
                    ChangeReason = changeReason,
                    ChangeDate = now,
                    ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "System" : changedBy
                });
            }

            profile.Price = newPrice;
            profile.VATPercentage = systemVat;
            profile.UpdatedDate = now;
            updated++;
        }

        if (updated > 0)
        {
            await Db.SaveChangesAsync(ct);
        }

        int notFound = ids.Length - profiles.Count;
        string message =
            $"تم تحديث سعر {updated} بروفايل إلى {newPrice:N2} ل.س" +
            (skipped > 0 ? $"، تخطي {skipped} بدون تغيير" : "") +
            (notFound > 0 ? $"، {notFound} خارج الشبكة/غير موجود" : "") +
            ".";

        return ProfileBulkPriceUpdateResult.Ok(updated, ids.Length, skipped, message);
    }
}
