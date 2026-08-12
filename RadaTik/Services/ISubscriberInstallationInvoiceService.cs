using RadaTik.Models;

namespace RadaTik.Services;

public interface ISubscriberInstallationInvoiceService
{
    Task CreateInitialSetupInvoiceAsync(Client client, string createdByUserId);

    /// <summary>مسار اللاقط الخاص — فاتورة مسودة (مواد + أجور) دون خصم محفظة.</summary>
    Task<int> CreatePrivateInitialSetupInvoiceAsync(Client client, string createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>فاتورة مسودة لمعالج المشترك الجديد — حسب مسار الاتصال.</summary>
    Task<int> CreateDraftInitialSetupInvoiceAsync(
        Client client,
        NewSubscriberWizardPath path,
        string createdByUserId,
        CancellationToken cancellationToken = default);

    Task<FinalizeInvoiceResult> UpdateDraftInvoiceItemsAsync(
        int invoiceId,
        int networkId,
        IReadOnlyList<DraftInvoiceLineUpdate> lineUpdates,
        CancellationToken cancellationToken = default);

    Task CreateReceiverUpgradeInvoiceIfNeededAsync(Client client, int? previousReceiverId, string createdByUserId);

    Task<FinalizeInvoiceResult> FinalizeInvoiceAsync(int invoiceId, int networkId, string userId, CancellationToken cancellationToken = default);

    Task<RegisterInstallationPaymentResult> RegisterPaymentAsync(
        int invoiceId,
        int networkId,
        string userId,
        decimal amount,
        SubscriberInstallationPaymentMethod method,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriberInstallationMaterialPrice>> GetOrCreateMaterialPricesAsync(int networkId);

    Task SaveMaterialPricesAsync(int networkId, IEnumerable<(string MaterialKey, decimal UnitPrice, bool IsActive, int? WarehouseItemId)> rows);

    Task SaveMaterialPricesWithModelsAsync(
        int networkId,
        IEnumerable<MaterialPriceSaveRow> rows,
        CancellationToken cancellationToken = default);
}

public sealed class DraftInvoiceLineUpdate
{
    public int ItemId { get; init; }
    public decimal Quantity { get; init; }
    public int? WarehouseItemId { get; init; }
}

public sealed class MaterialPriceSaveRow
{
    public string MaterialKey { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
    public int? DefaultWarehouseItemId { get; set; }
    public List<int> WarehouseItemIds { get; set; } = [];
}

public sealed class FinalizeInvoiceResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class RegisterInstallationPaymentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public SubscriberInstallationInvoiceStatus? NewStatus { get; init; }
}
