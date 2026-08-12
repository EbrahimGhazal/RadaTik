using global::RadaTik.Models;

namespace RadaTik.Areas.CompanyAdmin.ViewModels;

public sealed class SubscriberInstallationInvoiceListRowViewModel
{
    public int Id { get; init; }
    public int ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public SubscriberInstallationInvoiceKind Kind { get; init; }
    public SubscriberInstallationInvoiceStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class SubscriberInstallationInvoicesIndexViewModel
{
    public int NetworkId { get; init; }
    public string NetworkName { get; init; } = string.Empty;
    public List<SubscriberInstallationInvoiceListRowViewModel> Rows { get; init; } = new();
}

public sealed class SubscriberInstallationInvoicePaymentRowViewModel
{
    public decimal Amount { get; init; }
    public DateTime PaidAt { get; init; }
    public SubscriberInstallationPaymentMethod PaymentMethod { get; init; }
    public string PaymentMethodLabel { get; init; } = string.Empty;
    public string ReceivedByName { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class SubscriberInstallationInvoiceDetailsViewModel
{
    public int Id { get; init; }
    public int ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public SubscriberInstallationInvoiceKind Kind { get; init; }
    public SubscriberReceiverMode ReceiverMode { get; init; }
    public SubscriberInstallationInvoiceStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public decimal ClientWalletBalance { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public List<SubscriberInstallationInvoiceItem> Items { get; init; } = new();
    public List<SubscriberInstallationInvoicePaymentRowViewModel> Payments { get; init; } = new();

    public bool CanFinalize =>
        Status == SubscriberInstallationInvoiceStatus.Draft
        && Kind == SubscriberInstallationInvoiceKind.InitialSetup;

    public bool CanCollectPayment =>
        Status is SubscriberInstallationInvoiceStatus.Finalized
            or SubscriberInstallationInvoiceStatus.PendingWalletPayment
            or SubscriberInstallationInvoiceStatus.PartiallyPaid
        && RemainingAmount > 0;
}
