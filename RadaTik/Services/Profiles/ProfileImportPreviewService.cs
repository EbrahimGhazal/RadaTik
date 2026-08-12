using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Dtos.MikroTik;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;

namespace RadaTik.Services.Profiles;

public sealed class ProfileImportPreviewService(
    ApplicationDbContext context,
    IMikroTikProfilesService mikroTikProfiles,
    IProfileImportPricingService profileImportPricing)
    : ApplicationServiceBase(context), IProfileImportPreviewService
{
    public async Task<ImportProfilesPreviewResult> GetPreviewWithTimeoutAsync(
        int serverId,
        int networkId,
        int timeoutMs = 5000,
        CancellationToken ct = default)
    {
        Task<ImportProfilesPreviewResult> previewTask =
            mikroTikProfiles.BuildProfilesImportPreviewAsync(serverId, networkId);
        Task completed = await Task.WhenAny(previewTask, Task.Delay(timeoutMs, ct));
        if (completed == previewTask)
        {
            return await previewTask;
        }

        throw new TimeoutException($"Profile import preview timed out after {timeoutMs}ms.");
    }

    public async Task<ProfileImportPreviewJsonModel> BuildImportPreviewJsonAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default)
    {
        MikroTikServer? server = await Db.MikroTikServers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId, ct);
        if (server == null)
        {
            return new ProfileImportPreviewJsonModel
            {
                Success = false,
                Message = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه."
            };
        }

        Network? selectedNetwork = await Db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId, ct);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId;

        ImportProfilesPreviewResult preview = await GetPreviewWithTimeoutAsync(serverId, networkId, ct: ct);
        decimal unitPrice = await profileImportPricing.GetProfileImportUnitPriceAsync(ct);
        decimal totalCharge = WalletMath.CeilSyp(unitPrice * preview.ImportableProfilesCount);
        decimal walletBalance = await profileImportPricing.GetCompanyWalletBalanceAsync(companyNetworkId, ct);

        return new ProfileImportPreviewJsonModel
        {
            Success = true,
            ServerId = serverId,
            TotalProfiles = preview.TotalProfilesOnServer,
            ImportableProfiles = preview.ImportableProfilesCount,
            UnitPrice = unitPrice,
            TotalCharge = totalCharge,
            WalletBalance = walletBalance
        };
    }
}
