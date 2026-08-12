using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class MaterialInvoiceLineInput
{
    public int? WarehouseItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string? ModelNumber { get; set; }
    public MaterialPackageUnit PackageUnit { get; set; } = MaterialPackageUnit.Piece;
    public int UnitsPerPackage { get; set; } = 1;
    public decimal PackageQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? RetailPrice { get; set; }
}

public sealed class MaterialSalesLineInput
{
    public int WarehouseItemId { get; set; }
    public MaterialSalePriceMode PriceMode { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>سعر القطعة المُدخل من النموذج (جملة/مفرق/مخصص).</summary>
    public decimal UnitPrice { get; set; }
    public decimal? CustomUnitPrice { get; set; }
}

public sealed class StocktakeLineInput
{
    public int WarehouseItemId { get; set; }
    public decimal CountedQuantity { get; set; }
}

public sealed class MaterialInvoiceResult
{
    public bool Success { get; init; }
    public int? InvoiceId { get; init; }
    public string? ErrorMessage { get; init; }

    public static MaterialInvoiceResult Ok(int id) => new() { Success = true, InvoiceId = id };
    public static MaterialInvoiceResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public interface IWarehouseMaterialInvoiceService
{
    Task<MaterialInvoiceResult> ApplyStocktakeAsync(
      int companyNetworkId,
      string? userId,
      DateTime stocktakeDate,
      DateTime? periodFrom,
      DateTime? periodTo,
      int? warehouseItemId,
      string? notes,
      IReadOnlyList<StocktakeLineInput> lines,
      CancellationToken ct = default);

    Task<MaterialInvoiceResult> CreatePurchaseInvoiceAsync(
      int companyNetworkId,
      string? userId,
      DateTime invoiceDate,
      string? supplierName,
      bool isPaid,
      bool linkWallet,
      string? notes,
      IReadOnlyList<MaterialInvoiceLineInput> lines,
      PricingCurrency? currency = null,
      int? erpSupplierId = null,
      CancellationToken ct = default);

    Task<MaterialInvoiceResult> CreateSalesInvoiceAsync(
      int companyNetworkId,
      string? userId,
      DateTime invoiceDate,
      string? customerName,
      bool isPaid,
      bool linkWallet,
      string? notes,
      IReadOnlyList<MaterialSalesLineInput> lines,
      PricingCurrency? currency = null,
      int? erpCustomerId = null,
      CancellationToken ct = default);

    Task<MaterialInvoiceResult> UpdatePurchaseInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      DateTime invoiceDate,
      string? supplierName,
      bool isPaid,
      bool linkWallet,
      string? notes,
      int? erpSupplierId = null,
      CancellationToken ct = default);

    Task<MaterialInvoiceResult> CancelPurchaseInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      bool refundWallet,
      CancellationToken ct = default);

    Task<MaterialInvoiceResult> UpdateSalesInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      DateTime invoiceDate,
      string? customerName,
      bool isPaid,
      bool linkWallet,
      string? notes,
      int? erpCustomerId = null,
      CancellationToken ct = default);

    Task<MaterialInvoiceResult> CancelSalesInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      bool refundWallet,
      CancellationToken ct = default);
}
