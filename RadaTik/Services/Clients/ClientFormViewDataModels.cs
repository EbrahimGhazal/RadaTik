using Microsoft.AspNetCore.Mvc.Rendering;

namespace RadaTik.Services.Clients;

public sealed class ClientCreatePricingViewData
{
    public bool HasPricing { get; init; }
    public decimal ChargeAmount { get; init; }
    public decimal SubscriberChargeAmount { get; init; }
    public decimal UserChargeAmount { get; init; }
    public decimal ChargeWalletBalance { get; init; }
    public decimal InitialPrice { get; init; }
    public decimal RenewalPrice { get; init; }
    public string? RenewalPeriodLabel { get; init; }
    public bool HasRenewalPricing { get; init; }
}

public sealed class ClientCreateFormViewData
{
    public required SelectList ReceiverId { get; init; }
    public required SelectList MikroTikServerId { get; init; }
    public required SelectList ProfileId { get; init; }
    public required ClientCreatePricingViewData Pricing { get; init; }
}

public sealed class ClientEditFormViewData
{
    public required SelectList ReceiverId { get; init; }
    public required SelectList MikroTikServerId { get; init; }
    public required SelectList ProfileId { get; init; }
}
