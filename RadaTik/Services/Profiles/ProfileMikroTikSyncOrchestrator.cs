using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Domain.Profiles;
using RadaTik.Dtos.MikroTik;
using RadaTik.Models;
using RadaTik.Services;

namespace RadaTik.Services.Profiles;

public sealed class ProfileMikroTikSyncOrchestrator(
    ApplicationDbContext context,
    IMikroTikProfilesService mikroTikProfiles,
    IProfileImportPricingService profileImportPricing,
    IProfileCompanyWalletService profileCompanyWallet)
    : ApplicationServiceBase(context), IProfileMikroTikSyncOrchestrator
{
    public async Task<ProfileSyncFromMikroTikOutcome> SyncFromMikroTikAsync(
        ProfileSyncFromMikroTikCommand command,
        CancellationToken ct = default)
    {
        MikroTikServer? server = await Db.MikroTikServers
            .FirstOrDefaultAsync(s => s.Id == command.ServerId && s.NetworkId == command.NetworkId, ct);
        if (server == null)
        {
            return new ProfileSyncFromMikroTikOutcome
            {
                Status = ProfileSyncFromMikroTikStatus.ServerNotFound,
                Message = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه"
            };
        }

        Network? selectedNetwork = await Db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == command.NetworkId, ct);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? command.NetworkId;

        ImportProfilesPreviewResult preview =
            await mikroTikProfiles.BuildProfilesImportPreviewAsync(command.ServerId, command.NetworkId);
        if (preview.ImportableProfilesCount <= 0)
        {
            return new ProfileSyncFromMikroTikOutcome
            {
                Status = ProfileSyncFromMikroTikStatus.NoImportable,
                Message = "لا يوجد بروفايلات جديدة للاستيراد من هذا السيرفر. إذا أردت تحديث البروفايلات الحالية استخدم «المزامنة الثنائية»."
            };
        }

        ProfileImportChargeEstimate syncCharge =
            await profileImportPricing.CalculateProfileChargeAsync(companyNetworkId, preview.ImportableProfilesCount, ct);
        if (!syncCharge.HasSufficientBalance)
        {
            return new ProfileSyncFromMikroTikOutcome
            {
                Status = ProfileSyncFromMikroTikStatus.InsufficientBalance,
                Message =
                    $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({syncCharge.WalletBalance:N2}) أقل من المبلغ المطلوب ({syncCharge.TotalCharge:N2}) ل.س.ج."
            };
        }

        decimal defaultPrice = Math.Clamp(command.DefaultPrice, 0m, 1_000_000m);

        try
        {
            SyncResult result = await mikroTikProfiles.SyncFromMikroTikToDatabase(
                command.ServerId,
                command.ImportAsInactive,
                command.NetworkId,
                defaultPrice);

            if (!result.Success)
            {
                return new ProfileSyncFromMikroTikOutcome
                {
                    Status = ProfileSyncFromMikroTikStatus.SyncFailed,
                    Message = MikroTikProfileErrorFormatter.Sanitize(result.Message, "فشلت المزامنة"),
                    SyncResult = result
                };
            }

            decimal chargedAmount = 0m;
            if (result.AddedCount > 0)
            {
                chargedAmount = await profileCompanyWallet.ChargeCompanyForProfileUnitsAsync(
                    companyNetworkId,
                    command.ActorUserId,
                    result.AddedCount,
                    $"خصم استيراد بروفايلات من السيرفر #{command.ServerId}",
                    ct);
            }

            string message;
            ProfileSyncFromMikroTikStatus status;
            if (result.AddedCount > 0 || result.UpdatedCount > 0)
            {
                status = ProfileSyncFromMikroTikStatus.Success;
                message = chargedAmount > 0m
                    ? $"{result.Message} وتم خصم {chargedAmount:N2} ل.س.ج مقابل {result.AddedCount} بروفايل مستورد."
                    : result.Message;
            }
            else
            {
                status = ProfileSyncFromMikroTikStatus.Info;
                message = "جميع البروفايلات محدثة بالفعل";
            }

            return new ProfileSyncFromMikroTikOutcome
            {
                Status = status,
                Message = message,
                ChargedAmount = chargedAmount,
                SyncResult = result
            };
        }
        catch (Exception ex)
        {
            return new ProfileSyncFromMikroTikOutcome
            {
                Status = ProfileSyncFromMikroTikStatus.Error,
                Message = MikroTikProfileErrorFormatter.Format("فشلت المزامنة", ex)
            };
        }
    }
}
