using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services.Clients;

public sealed class ClientFormViewDataService(
    ApplicationDbContext context,
    IUsageBasedSubscriptionChargeService usageChargeService)
    : ApplicationServiceBase(context), IClientFormViewDataService
{
    public async Task<ClientCreateFormViewData> BuildCreateFormDataAsync(int networkId, Client client, CancellationToken ct = default)
    {
        IQueryable<Receiver> receiversQuery = Db.Receivers.Where(r => r.NetworkId == networkId);
        if (client.MikroTikServerId.HasValue)
        {
            receiversQuery = receiversQuery.Where(r => r.Sector.MikroTikServerId == client.MikroTikServerId.Value);
        }

        List<Receiver> receivers = await receiversQuery
            .Include(r => r.Sector)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        List<MikroTikServer> servers = await Db.MikroTikServers
            .Where(s => s.NetworkId == networkId)
            .ToListAsync(ct);
        List<Profile> profiles = await Db.Profiles
            .Where(p => p.IsActive && p.NetworkId == networkId)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        return new ClientCreateFormViewData
        {
            ReceiverId = new SelectList(receivers, "Id", "Name", client.ReceiverId),
            MikroTikServerId = new SelectList(servers, "Id", "Name", client.MikroTikServerId),
            ProfileId = new SelectList(profiles, "Id", "Name", client.ProfileId),
            Pricing = await BuildCreatePricingAsync(networkId, ct)
        };
    }

    public async Task<ClientEditFormViewData> BuildEditFormDataAsync(int networkId, Client client, CancellationToken ct = default)
    {
        List<Receiver> receivers = await Db.Receivers
            .Where(r => r.NetworkId == networkId)
            .ToListAsync(ct);
        List<MikroTikServer> servers = await Db.MikroTikServers
            .Where(s => s.NetworkId == networkId)
            .ToListAsync(ct);
        List<Profile> profiles = await Db.Profiles
            .Where(p => p.IsActive && p.NetworkId == networkId)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        return new ClientEditFormViewData
        {
            ReceiverId = new SelectList(receivers, "Id", "Name", client.ReceiverId),
            MikroTikServerId = new SelectList(servers, "Id", "Name", client.MikroTikServerId),
            ProfileId = new SelectList(profiles, "Id", "Name", client.ProfileId)
        };
    }

    private async Task<ClientCreatePricingViewData> BuildCreatePricingAsync(int selectedNetworkId, CancellationToken ct)
    {
        try
        {
            Network? selectedNetwork = await Db.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            UsageImportChargeEstimate subscriberEstimate = await usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSubscriber,
                1);
            List<FeaturePricing> clientPricingRows = await Db.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == FeatureKeys.Clients &&
                    p.ChargeUnit == PricingChargeUnit.PerSubscriber)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync(ct);

            FeaturePricing? initialPricing = clientPricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
            FeaturePricing? renewalPricing = clientPricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);

            return new ClientCreatePricingViewData
            {
                HasPricing = subscriberEstimate.HasCharge,
                ChargeAmount = subscriberEstimate.RequiredAmountSyp,
                SubscriberChargeAmount = subscriberEstimate.RequiredAmountSyp,
                UserChargeAmount = 0m,
                ChargeWalletBalance = subscriberEstimate.WalletBalance > 0m ? subscriberEstimate.WalletBalance : 0m,
                InitialPrice = initialPricing?.AmountSYP ?? subscriberEstimate.RequiredAmountSyp,
                RenewalPrice = renewalPricing?.AmountSYP ?? 0m,
                RenewalPeriodLabel = renewalPricing != null
                    ? PricingDisplay.BillingPeriodLabel(renewalPricing.BillingPeriod)
                    : null,
                HasRenewalPricing = renewalPricing != null
            };
        }
        catch
        {
            return new ClientCreatePricingViewData();
        }
    }
}
