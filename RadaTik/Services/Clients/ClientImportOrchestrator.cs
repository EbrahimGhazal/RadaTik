using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;
using RadaTik.Services.MikroTik;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services.Clients;

public sealed class ClientImportOrchestrator(
    ApplicationDbContext context,
    IMikroTikUserImportService mikroTikImport,
    IUsageBasedSubscriptionChargeService usageChargeService)
    : ApplicationServiceBase(context), IClientImportOrchestrator
{
    public async Task<ClientImportFromServerViewModel> BuildImportFromServerViewAsync(int networkId, CancellationToken ct = default)
    {
        List<MikroTikServer> servers = await Db.MikroTikServers
            .Where(s => s.NetworkId == networkId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        ClientImportPageModel importPage = await BuildImportPageAsync(networkId, ct);
        return new ClientImportFromServerViewModel
        {
            Servers = servers,
            ImportPage = importPage
        };
    }

    public async Task<ClientImportPageModel> BuildImportPageAsync(int networkId, CancellationToken ct = default)
    {
        List<int> serverIds = await Db.MikroTikServers
            .Where(s => s.NetworkId == networkId)
            .OrderBy(s => s.Name)
            .Select(s => s.Id)
            .ToListAsync(ct);

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId, ct);
        Dictionary<int, ImportUsersPreviewResult> previewByServer = new();
        Dictionary<int, UsageImportChargeEstimate> chargeByServer = new();

        foreach (int serverId in serverIds)
        {
            ImportUsersPreviewResult preview = await mikroTikImport.BuildUsersImportPreviewAsync(serverId, networkId);
            previewByServer[serverId] = preview;
            chargeByServer[serverId] = await BuildChargeEstimateAsync(companyNetworkId, preview.ImportableUsersCount, ct);
        }

        UsageImportChargeEstimate unitEstimate = await usageChargeService.EstimateImportChargeAsync(
            companyNetworkId,
            PricingChargeUnit.PerSubscriber,
            1);

        return new ClientImportPageModel
        {
            PreviewByServer = previewByServer,
            ChargeByServer = chargeByServer,
            SubscriberUnitPrice = unitEstimate.UnitPriceSyp
        };
    }

    public async Task<MikroTikServerUsersImportContext> BuildServerUsersImportContextAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default)
    {
        int companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId, ct);
        ImportUsersPreviewResult preview = await mikroTikImport.BuildUsersImportPreviewAsync(serverId, networkId);
        UsageImportChargeEstimate estimate = await BuildChargeEstimateAsync(companyNetworkId, preview.ImportableUsersCount, ct);
        UsageImportChargeEstimate unitEstimate = await usageChargeService.EstimateImportChargeAsync(
            companyNetworkId,
            PricingChargeUnit.PerSubscriber,
            1);

        return new MikroTikServerUsersImportContext
        {
            Preview = preview,
            Estimate = estimate,
            SubscriberUnitPrice = unitEstimate.UnitPriceSyp
        };
    }

    public async Task<ClientImportOutcome> ExecuteImportAsync(
        int serverId,
        int networkId,
        string actorUserId,
        bool rejectWhenProfilesMissing,
        CancellationToken ct = default)
    {
        MikroTikServer? server = await Db.MikroTikServers
            .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId, ct);
        if (server == null)
        {
            return ClientImportOutcome.Failed("السيرفر غير موجود أو لا يتبع الشبكة الحالية");
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId, ct);
        ImportUsersPreviewResult preview = await mikroTikImport.BuildUsersImportPreviewAsync(serverId, networkId);

        if (rejectWhenProfilesMissing && preview.MissingProfileCount > 0)
        {
            return ClientImportOutcome.Failed(
                $"لا يمكن استيراد المشتركين قبل استيراد البروفايلات. يوجد {preview.MissingProfileCount} مشترك مرتبط ببروفايلات غير مستوردة.");
        }

        if (preview.ImportableUsersCount <= 0 &&
            preview.RelinkableUsersCount <= 0 &&
            preview.UpdatableUsersCount <= 0)
        {
            return ClientImportOutcome.Failed("لا توجد إضافات أو تعديلات من السيرفر لمزامنتها حالياً.");
        }

        int billableCount = preview.ImportableUsersCount;
        UsageImportChargeEstimate charge = await BuildChargeEstimateAsync(companyNetworkId, billableCount, ct);
        if (charge.RequiredAmountSyp > 0m && charge.WalletBalance < charge.RequiredAmountSyp)
        {
            return ClientImportOutcome.Failed(
                $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({charge.WalletBalance:N2}) أقل من المبلغ المطلوب ({charge.RequiredAmountSyp:N2}) ل.س.ج.");
        }

        ImportUsersResult result = await mikroTikImport.ImportAllUsersToDatabase(serverId, networkId);
        if (!result.Success)
        {
            return ClientImportOutcome.Failed(result.Message);
        }

        if (result.AddedCount > 0)
        {
            for (int i = 0; i < result.AddedCount; i++)
            {
                await usageChargeService.ChargeUsageIncreaseAsync(
                    companyNetworkId,
                    actorUserId,
                    PricingChargeUnit.PerSubscriber);
            }
        }

        string? warnings = null;
        if (result.FailedCount > 0 && result.Errors.Any())
        {
            warnings = string.Join(" | ", result.Errors.Take(5));
        }

        string? failedUsersJson = null;
        if (result.UsersFailedCount > 0 && result.Errors.Any())
        {
            List<string> failedUserDetails = result.Errors
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Take(15)
                .ToList();
            failedUsersJson = JsonSerializer.Serialize(failedUserDetails);
        }

        return ClientImportOutcome.Succeeded(result.Message, warnings, failedUsersJson, result.DuplicateCount);
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken ct)
    {
        Network? selectedNetwork = await Db.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId, ct);
        return selectedNetwork?.ParentNetworkId ?? networkId;
    }

    private async Task<UsageImportChargeEstimate> BuildChargeEstimateAsync(
        int companyNetworkId,
        int importableCount,
        CancellationToken ct)
    {
        UsageImportChargeEstimate subscriberEstimate = await usageChargeService.EstimateImportChargeAsync(
            companyNetworkId,
            PricingChargeUnit.PerSubscriber,
            importableCount);

        return new UsageImportChargeEstimate
        {
            ImportableCount = importableCount,
            MatchedPricingsCount = subscriberEstimate.MatchedPricingsCount,
            UnitPriceSyp = subscriberEstimate.UnitPriceSyp,
            RequiredAmountSyp = subscriberEstimate.RequiredAmountSyp,
            WalletBalance = subscriberEstimate.WalletBalance
        };
    }
}
