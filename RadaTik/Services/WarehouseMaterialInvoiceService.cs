using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class WarehouseMaterialInvoiceService(
  ApplicationDbContext context,
  MaterialInvoiceWalletService walletService,
  MaterialInvoiceAccountingService accountingService,
  IWarehouseStockService warehouseStock) : IWarehouseMaterialInvoiceService
{
    private readonly ApplicationDbContext _context = context;
    private readonly MaterialInvoiceWalletService _walletService = walletService;
    private readonly MaterialInvoiceAccountingService _accounting = accountingService;
    private readonly IWarehouseStockService _warehouseStock = warehouseStock;

    public async Task<MaterialInvoiceResult> CreatePurchaseInvoiceAsync(
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
      CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return MaterialInvoiceResult.Fail("يجب تسجيل الدخول.");
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync(ct);
        MaterialInvoiceResult build = await BuildAndSavePurchaseAsync(
          companyNetworkId, userId, invoiceDate, supplierName, isPaid, notes, lines, currency, erpSupplierId, ct);
        if (!build.Success || build.InvoiceId is not int invoiceId)
        {
            await tx.RollbackAsync(ct);
            return build;
        }

        if (isPaid && linkWallet)
        {
            MaterialPurchaseInvoice invoice = await _context.MaterialPurchaseInvoices
              .FirstAsync(i => i.Id == invoiceId, ct);
            MaterialInvoiceWalletResult wallet = await _walletService.ApplyPurchasePaymentAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!wallet.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(wallet.ErrorMessage ?? "تعذر الخصم من المحفظة.");
            }

            if (wallet.WalletTransactionId.HasValue)
            {
                invoice.WalletTransactionId = wallet.WalletTransactionId;
                await _context.SaveChangesAsync(ct);
            }
        }

        if (isPaid)
        {
            MaterialPurchaseInvoice invoice = await _context.MaterialPurchaseInvoices
              .FirstAsync(i => i.Id == invoiceId, ct);
            MaterialInvoiceAccountingResult acc = await _accounting.SyncPurchasePaymentAsync(
              invoice, userId, isPaid, linkWallet, ct);
            if (!acc.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(acc.ErrorMessage ?? "تعذر ربط الدفتر أو الصندوق.");
            }
        }

        await tx.CommitAsync(ct);
        return build;
    }

    public async Task<MaterialInvoiceResult> CreateSalesInvoiceAsync(
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
      CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return MaterialInvoiceResult.Fail("يجب تسجيل الدخول.");
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync(ct);
        MaterialInvoiceResult build = await BuildAndSaveSalesAsync(
          companyNetworkId, userId, invoiceDate, customerName, isPaid, notes, lines, currency, erpCustomerId, ct);
        if (!build.Success || build.InvoiceId is not int invoiceId)
        {
            await tx.RollbackAsync(ct);
            return build;
        }

        if (isPaid && linkWallet)
        {
            MaterialSalesInvoice invoice = await _context.MaterialSalesInvoices
              .FirstAsync(i => i.Id == invoiceId, ct);
            MaterialInvoiceWalletResult wallet = await _walletService.ApplySaleReceiptAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!wallet.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(wallet.ErrorMessage ?? "تعذر إضافة المبلغ للمحفظة.");
            }

            if (wallet.WalletTransactionId.HasValue)
            {
                invoice.WalletTransactionId = wallet.WalletTransactionId;
                await _context.SaveChangesAsync(ct);
            }
        }

        if (isPaid)
        {
            MaterialSalesInvoice invoice = await _context.MaterialSalesInvoices
              .FirstAsync(i => i.Id == invoiceId, ct);
            MaterialInvoiceAccountingResult acc = await _accounting.SyncSalesPaymentAsync(
              invoice, userId, isPaid, linkWallet, ct);
            if (!acc.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(acc.ErrorMessage ?? "تعذر ربط الدفتر أو الصندوق.");
            }
        }

        await tx.CommitAsync(ct);
        return build;
    }

    public async Task<MaterialInvoiceResult> UpdatePurchaseInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      DateTime invoiceDate,
      string? supplierName,
      bool isPaid,
      bool linkWallet,
      string? notes,
      int? erpSupplierId = null,
      CancellationToken ct = default)
    {
        MaterialPurchaseInvoice? invoice = await _context.MaterialPurchaseInvoices
          .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyNetworkId == companyNetworkId, ct);
        if (invoice == null)
        {
            return MaterialInvoiceResult.Fail("الفاتورة غير موجودة.");
        }

        if (invoice.IsCancelled)
        {
            return MaterialInvoiceResult.Fail("لا يمكن تعديل فاتورة ملغاة.");
        }

        (int? resolvedSupplierId, string? resolvedSupplierName, string? supplierError) =
          await ResolveErpSupplierAsync(companyNetworkId, erpSupplierId, supplierName, ct);
        if (supplierError != null)
        {
            return MaterialInvoiceResult.Fail(supplierError);
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync(ct);
        bool wasPaidWithWallet = invoice.IsPaid && invoice.WalletTransactionId.HasValue;

        if (wasPaidWithWallet && (!isPaid || !linkWallet))
        {
            MaterialInvoiceWalletResult refund = await _walletService.RefundPurchasePaymentAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!refund.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(refund.ErrorMessage ?? "تعذر تنفيذ عملية الاسترجاع من المحفظة.");
            }

            invoice.WalletTransactionId = null;
        }

        invoice.InvoiceDate = invoiceDate.Date;
        invoice.ErpSupplierId = resolvedSupplierId;
        invoice.SupplierName = resolvedSupplierName;
        invoice.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        invoice.IsPaid = isPaid;
        invoice.PaidAt = isPaid ? invoiceDate.Date : null;

        if (isPaid && linkWallet && !wasPaidWithWallet)
        {
            MaterialInvoiceWalletResult pay = await _walletService.ApplyPurchasePaymentAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!pay.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(pay.ErrorMessage ?? "تعذر خصم قيمة الفاتورة من المحفظة.");
            }

            invoice.WalletTransactionId = pay.WalletTransactionId;
        }

        MaterialInvoiceAccountingResult acc = await _accounting.SyncPurchasePaymentAsync(
          invoice, userId, isPaid, linkWallet, ct);
        if (!acc.Success)
        {
            await tx.RollbackAsync(ct);
            return MaterialInvoiceResult.Fail(acc.ErrorMessage ?? "تعذر ربط الدفتر أو الصندوق.");
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MaterialInvoiceResult.Ok(invoiceId);
    }

    public async Task<MaterialInvoiceResult> CancelPurchaseInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      bool refundWallet,
      CancellationToken ct = default)
    {
        MaterialPurchaseInvoice? invoice = await _context.MaterialPurchaseInvoices
          .Include(i => i.Lines)
          .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyNetworkId == companyNetworkId, ct);
        if (invoice == null)
        {
            return MaterialInvoiceResult.Fail("الفاتورة غير موجودة.");
        }

        if (invoice.IsCancelled)
        {
            return MaterialInvoiceResult.Fail("الفاتورة ملغاة مسبقاً.");
        }

        List<int> itemIds = invoice.Lines
          .Where(l => l.WarehouseItemId.HasValue)
          .Select(l => l.WarehouseItemId!.Value)
          .Distinct()
          .ToList();

        Dictionary<int, decimal> onHand = await _warehouseStock.GetOnHandByItemIdAsync(
          companyNetworkId, itemIds, ct);

        foreach (MaterialPurchaseInvoiceLine line in invoice.Lines)
        {
            if (!line.WarehouseItemId.HasValue)
            {
                continue;
            }

            decimal available = onHand.GetValueOrDefault(line.WarehouseItemId.Value, 0m);
            if (available < line.BaseQuantity)
            {
                return MaterialInvoiceResult.Fail(
                  $"لا يمكن الإلغاء — الكمية المتبقية من «{line.ItemName}» ({available:0.###}) أقل من المشتراة ({line.BaseQuantity:0.###}).");
            }
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync(ct);

        if (refundWallet && invoice.IsPaid && invoice.WalletTransactionId.HasValue)
        {
            MaterialInvoiceWalletResult refund = await _walletService.RefundPurchasePaymentAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!refund.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(refund.ErrorMessage ?? "تعذر تنفيذ استرجاع المحفظة لفاتورة الشراء.");
            }
        }

        if (invoice.IsPaid)
        {
            MaterialInvoiceAccountingResult reverse = await _accounting.ReversePurchasePaymentAsync(
              invoice, userId, ct);
            if (!reverse.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(reverse.ErrorMessage ?? "تعذر عكس قيد دفتر/صندوق فاتورة الشراء.");
            }
        }

        foreach (MaterialPurchaseInvoiceLine line in invoice.Lines)
        {
            if (!line.WarehouseItemId.HasValue)
            {
                continue;
            }

            _context.WarehouseMovements.Add(new WarehouseMovement
            {
                CompanyNetworkId = companyNetworkId,
                WarehouseItemId = line.WarehouseItemId.Value,
                MovementType = WarehouseMovementType.Out,
                Quantity = line.BaseQuantity,
                MovementDate = DateTime.Today,
                MaterialPurchaseInvoiceId = invoiceId,
                Notes = $"إلغاء فاتورة شراء #{invoiceId}",
                CreatedByUserId = userId
            });
        }

        invoice.IsCancelled = true;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.IsPaid = false;
        invoice.PaidAt = null;
        invoice.WalletTransactionId = null;

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MaterialInvoiceResult.Ok(invoiceId);
    }

    public async Task<MaterialInvoiceResult> UpdateSalesInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      DateTime invoiceDate,
      string? customerName,
      bool isPaid,
      bool linkWallet,
      string? notes,
      int? erpCustomerId = null,
      CancellationToken ct = default)
    {
        MaterialSalesInvoice? invoice = await _context.MaterialSalesInvoices
          .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyNetworkId == companyNetworkId, ct);
        if (invoice == null)
        {
            return MaterialInvoiceResult.Fail("الفاتورة غير موجودة.");
        }

        if (invoice.IsCancelled)
        {
            return MaterialInvoiceResult.Fail("لا يمكن تعديل فاتورة ملغاة.");
        }

        (int? resolvedCustomerId, string? resolvedCustomerName, string? customerError) =
          await ResolveErpCustomerAsync(companyNetworkId, erpCustomerId, customerName, ct);
        if (customerError != null)
        {
            return MaterialInvoiceResult.Fail(customerError);
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync(ct);
        bool wasPaidWithWallet = invoice.IsPaid && invoice.WalletTransactionId.HasValue;

        if (wasPaidWithWallet && (!isPaid || !linkWallet))
        {
            MaterialInvoiceWalletResult refund = await _walletService.RefundSaleReceiptAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!refund.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(refund.ErrorMessage ?? "تعذر تنفيذ استرجاع المحفظة لفاتورة البيع.");
            }

            invoice.WalletTransactionId = null;
        }

        invoice.InvoiceDate = invoiceDate.Date;
        invoice.ErpCustomerId = resolvedCustomerId;
        invoice.CustomerName = resolvedCustomerName;
        invoice.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        invoice.IsPaid = isPaid;
        invoice.PaidAt = isPaid ? invoiceDate.Date : null;

        if (isPaid && linkWallet && !wasPaidWithWallet)
        {
            MaterialInvoiceWalletResult receipt = await _walletService.ApplySaleReceiptAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!receipt.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(receipt.ErrorMessage ?? "تعذر إضافة قيمة الفاتورة إلى المحفظة.");
            }

            invoice.WalletTransactionId = receipt.WalletTransactionId;
        }

        MaterialInvoiceAccountingResult acc = await _accounting.SyncSalesPaymentAsync(
          invoice, userId, isPaid, linkWallet, ct);
        if (!acc.Success)
        {
            await tx.RollbackAsync(ct);
            return MaterialInvoiceResult.Fail(acc.ErrorMessage ?? "تعذر ربط الدفتر أو الصندوق.");
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MaterialInvoiceResult.Ok(invoiceId);
    }

    public async Task<MaterialInvoiceResult> CancelSalesInvoiceAsync(
      int companyNetworkId,
      int invoiceId,
      string userId,
      bool refundWallet,
      CancellationToken ct = default)
    {
        MaterialSalesInvoice? invoice = await _context.MaterialSalesInvoices
          .Include(i => i.Lines)
          .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyNetworkId == companyNetworkId, ct);
        if (invoice == null)
        {
            return MaterialInvoiceResult.Fail("الفاتورة غير موجودة.");
        }

        if (invoice.IsCancelled)
        {
            return MaterialInvoiceResult.Fail("الفاتورة ملغاة مسبقاً.");
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync(ct);

        if (refundWallet && invoice.IsPaid && invoice.WalletTransactionId.HasValue)
        {
            MaterialInvoiceWalletResult refund = await _walletService.RefundSaleReceiptAsync(
              companyNetworkId, invoiceId, invoice.TotalAmount, invoice.Currency, userId, ct);
            if (!refund.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(refund.ErrorMessage ?? "تعذر تنفيذ استرجاع المحفظة عند إلغاء فاتورة البيع.");
            }
        }

        if (invoice.IsPaid)
        {
            MaterialInvoiceAccountingResult reverse = await _accounting.ReverseSalesPaymentAsync(
              invoice, userId, ct);
            if (!reverse.Success)
            {
                await tx.RollbackAsync(ct);
                return MaterialInvoiceResult.Fail(reverse.ErrorMessage ?? "تعذر عكس قيد دفتر/صندوق فاتورة البيع.");
            }
        }

        foreach (MaterialSalesInvoiceLine line in invoice.Lines)
        {
            _context.WarehouseMovements.Add(new WarehouseMovement
            {
                CompanyNetworkId = companyNetworkId,
                WarehouseItemId = line.WarehouseItemId,
                MovementType = WarehouseMovementType.In,
                Quantity = line.Quantity,
                MovementDate = DateTime.Today,
                MaterialSalesInvoiceId = invoiceId,
                Notes = $"إلغاء فاتورة بيع #{invoiceId}",
                CreatedByUserId = userId
            });
        }

        invoice.IsCancelled = true;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.IsPaid = false;
        invoice.PaidAt = null;
        invoice.WalletTransactionId = null;

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MaterialInvoiceResult.Ok(invoiceId);
    }

    public async Task<MaterialInvoiceResult> ApplyStocktakeAsync(
      int companyNetworkId,
      string? userId,
      DateTime stocktakeDate,
      DateTime? periodFrom,
      DateTime? periodTo,
      int? warehouseItemId,
      string? notes,
      IReadOnlyList<StocktakeLineInput> lines,
      CancellationToken ct = default)
    {
        if (lines == null || lines.Count == 0)
        {
            return MaterialInvoiceResult.Fail("أدخل كميات الجرد.");
        }

        List<int> itemIds = lines.Select(l => l.WarehouseItemId).Distinct().ToList();
        List<WarehouseMovement> movements = await _context.WarehouseMovements
          .AsNoTracking()
          .Where(m => m.CompanyNetworkId == companyNetworkId && itemIds.Contains(m.WarehouseItemId))
          .ToListAsync(ct);

        Dictionary<int, List<WarehouseMovement>> byItem = movements
          .GroupBy(m => m.WarehouseItemId)
          .ToDictionary(g => g.Key, g => g.ToList());

        WarehouseStocktake stocktake = new()
        {
            CompanyNetworkId = companyNetworkId,
            StocktakeDate = stocktakeDate.Date,
            PeriodFrom = periodFrom?.Date,
            PeriodTo = periodTo?.Date,
            WarehouseItemId = warehouseItemId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = userId
        };

        _context.WarehouseStocktakes.Add(stocktake);
        await _context.SaveChangesAsync(ct);

        foreach (StocktakeLineInput input in lines)
        {
            byItem.TryGetValue(input.WarehouseItemId, out List<WarehouseMovement>? itemMovements);
            decimal systemQty = _warehouseStock.ComputeOnHand(itemMovements ?? []);
            decimal diff = input.CountedQuantity - systemQty;
            if (diff == 0m)
            {
                continue;
            }

            stocktake.Lines.Add(new WarehouseStocktakeLine
            {
                WarehouseItemId = input.WarehouseItemId,
                SystemQuantity = systemQty,
                CountedQuantity = input.CountedQuantity,
                Difference = diff
            });

            _context.WarehouseMovements.Add(new WarehouseMovement
            {
                CompanyNetworkId = companyNetworkId,
                WarehouseItemId = input.WarehouseItemId,
                MovementType = WarehouseMovementType.Adjustment,
                Quantity = diff,
                MovementDate = stocktake.StocktakeDate,
                WarehouseStocktakeId = stocktake.Id,
                Notes = $"جرد #{stocktake.Id}",
                CreatedByUserId = userId
            });
        }

        await _context.SaveChangesAsync(ct);
        return MaterialInvoiceResult.Ok(stocktake.Id);
    }

    private async Task<MaterialInvoiceResult> BuildAndSavePurchaseAsync(
      int companyNetworkId,
      string userId,
      DateTime invoiceDate,
      string? supplierName,
      bool isPaid,
      string? notes,
      IReadOnlyList<MaterialInvoiceLineInput> lines,
      PricingCurrency? currency,
      int? erpSupplierId,
      CancellationToken ct)
    {
        if (lines == null || lines.Count == 0)
        {
            return MaterialInvoiceResult.Fail("أضف بنداً واحداً على الأقل.");
        }

        (int? resolvedSupplierId, string? resolvedSupplierName, string? supplierError) =
          await ResolveErpSupplierAsync(companyNetworkId, erpSupplierId, supplierName, ct);
        if (supplierError != null)
        {
            return MaterialInvoiceResult.Fail(supplierError);
        }

        Network? company = await _context.Networks.AsNoTracking()
          .FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        PricingCurrency invoiceCurrency = currency ?? company?.DefaultMaterialInvoiceCurrency ?? PricingCurrency.SYP_New;

        List<WarehouseItem> existingItems = await _context.WarehouseItems
          .Where(i => i.CompanyNetworkId == companyNetworkId)
          .ToListAsync(ct);

        Dictionary<string, WarehouseItem> byKey = existingItems.ToDictionary(
          i => WarehouseMaterialQuantityHelper.BuildItemMatchKey(i.Name, i.ModelNumber),
          StringComparer.OrdinalIgnoreCase);

        Dictionary<int, WarehouseItem> byId = existingItems.ToDictionary(i => i.Id);

        MaterialPurchaseInvoice invoice = new()
        {
            CompanyNetworkId = companyNetworkId,
            InvoiceDate = invoiceDate.Date,
            ErpSupplierId = resolvedSupplierId,
            SupplierName = resolvedSupplierName,
            Currency = invoiceCurrency,
            IsPaid = isPaid,
            PaidAt = isPaid ? invoiceDate.Date : null,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = userId
        };

        decimal total = 0m;

        foreach (MaterialInvoiceLineInput input in lines)
        {
            string name = (input.ItemName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return MaterialInvoiceResult.Fail("اسم المادة مطلوب في كل بند.");
            }

            decimal baseQty = WarehouseMaterialQuantityHelper.ComputeBaseQuantity(
              input.PackageUnit, input.PackageQuantity, input.UnitsPerPackage);
            if (baseQty <= 0m)
            {
                return MaterialInvoiceResult.Fail($"الكمية غير صالحة للمادة «{name}».");
            }

            if (input.UnitPrice < 0m)
            {
                return MaterialInvoiceResult.Fail($"سعر الشراء غير صالح للمادة «{name}».");
            }

            decimal lineTotal = input.PackageQuantity * input.UnitPrice;
            if (lineTotal <= 0m)
            {
                return MaterialInvoiceResult.Fail($"إجمالي البند يجب أن يكون أكبر من صفر للمادة «{name}».");
            }

            string? model = string.IsNullOrWhiteSpace(input.ModelNumber) ? null : input.ModelNumber.Trim();
            WarehouseItem item;

            if (input.WarehouseItemId is > 0 && byId.TryGetValue(input.WarehouseItemId.Value, out WarehouseItem? selected))
            {
                item = selected;
                name = item.Name;
                model = item.ModelNumber;
            }
            else
            {
                string key = WarehouseMaterialQuantityHelper.BuildItemMatchKey(name, model);
                if (!byKey.TryGetValue(key, out item!))
                {
                    item = new WarehouseItem
                    {
                        CompanyNetworkId = companyNetworkId,
                        Name = name,
                        ModelNumber = model,
                        Unit = "قطعة",
                        IsActive = true
                    };
                    _context.WarehouseItems.Add(item);
                    byKey[key] = item;
                }
            }

            decimal? purchasePerPiece = baseQty > 0m ? Math.Round(lineTotal / baseQty, 2) : null;
            if (purchasePerPiece.HasValue)
            {
                item.PurchasePrice = purchasePerPiece;
                item.PurchaseCurrency = invoiceCurrency;
            }

            if (input.WholesalePrice is > 0m)
            {
                item.WholesalePrice = input.WholesalePrice;
            }

            if (input.RetailPrice is > 0m)
            {
                item.RetailPrice = input.RetailPrice;
            }

            invoice.Lines.Add(new MaterialPurchaseInvoiceLine
            {
                WarehouseItem = item,
                ItemName = name,
                ModelNumber = model,
                PackageUnit = input.PackageUnit,
                UnitsPerPackage = WarehouseMaterialQuantityHelper.NormalizeUnitsPerPackage(input.PackageUnit, input.UnitsPerPackage),
                PackageQuantity = input.PackageQuantity,
                BaseQuantity = baseQty,
                UnitPrice = input.UnitPrice,
                LineTotal = lineTotal,
                WholesalePrice = input.WholesalePrice,
                RetailPrice = input.RetailPrice
            });

            total += lineTotal;
        }

        invoice.TotalAmount = total;
        _context.MaterialPurchaseInvoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        foreach (MaterialPurchaseInvoiceLine line in invoice.Lines)
        {
            if (!line.WarehouseItemId.HasValue)
            {
                continue;
            }

            _context.WarehouseMovements.Add(new WarehouseMovement
            {
                CompanyNetworkId = companyNetworkId,
                WarehouseItemId = line.WarehouseItemId.Value,
                MovementType = WarehouseMovementType.In,
                Quantity = line.BaseQuantity,
                MovementDate = invoice.InvoiceDate,
                MaterialPurchaseInvoiceId = invoice.Id,
                Notes = $"شراء مواد — فاتورة #{invoice.Id}",
                CreatedByUserId = userId
            });
        }

        await _context.SaveChangesAsync(ct);
        return MaterialInvoiceResult.Ok(invoice.Id);
    }

    private async Task<MaterialInvoiceResult> BuildAndSaveSalesAsync(
      int companyNetworkId,
      string userId,
      DateTime invoiceDate,
      string? customerName,
      bool isPaid,
      string? notes,
      IReadOnlyList<MaterialSalesLineInput> lines,
      PricingCurrency? currency,
      int? erpCustomerId,
      CancellationToken ct)
    {
        if (lines == null || lines.Count == 0)
        {
            return MaterialInvoiceResult.Fail("أضف بنداً واحداً على الأقل.");
        }

        (int? resolvedCustomerId, string? resolvedCustomerName, string? customerError) =
          await ResolveErpCustomerAsync(companyNetworkId, erpCustomerId, customerName, ct);
        if (customerError != null)
        {
            return MaterialInvoiceResult.Fail(customerError);
        }

        Network? company = await _context.Networks.AsNoTracking()
          .FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        PricingCurrency invoiceCurrency = currency ?? company?.DefaultMaterialInvoiceCurrency ?? PricingCurrency.SYP_New;

        List<int> lineItemIds = lines.Select(l => l.WarehouseItemId).Distinct().ToList();
        Dictionary<int, decimal> onHand = await _warehouseStock.GetOnHandByItemIdAsync(
          companyNetworkId, lineItemIds, ct);

        List<WarehouseItem> items = await _context.WarehouseItems
          .Where(i => i.CompanyNetworkId == companyNetworkId && i.IsActive && lineItemIds.Contains(i.Id))
          .ToListAsync(ct);
        Dictionary<int, WarehouseItem> itemMap = items.ToDictionary(i => i.Id);

        MaterialSalesInvoice invoice = new()
        {
            CompanyNetworkId = companyNetworkId,
            InvoiceDate = invoiceDate.Date,
            ErpCustomerId = resolvedCustomerId,
            CustomerName = resolvedCustomerName,
            Currency = invoiceCurrency,
            IsPaid = isPaid,
            PaidAt = isPaid ? invoiceDate.Date : null,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = userId
        };

        decimal total = 0m;
        Dictionary<int, decimal> pendingOut = new();

        foreach (MaterialSalesLineInput input in lines)
        {
            if (!itemMap.TryGetValue(input.WarehouseItemId, out WarehouseItem? item))
            {
                return MaterialInvoiceResult.Fail("صنف غير موجود أو غير نشط.");
            }

            if (input.Quantity <= 0m || input.Quantity != decimal.Truncate(input.Quantity))
            {
                return MaterialInvoiceResult.Fail($"الكمية يجب أن تكون عدداً صحيحاً (قطعة واحدة على الأقل) للصنف «{item.Name}».");
            }

            MaterialInvoiceResult? priceError = ValidateSalesLinePrice(item, input, invoiceCurrency);
            if (priceError != null)
            {
                return priceError;
            }

            decimal unitPrice = ResolveSaleUnitPrice(item, input);
            if (unitPrice <= 0m)
            {
                return MaterialInvoiceResult.Fail($"حدّد سعر البيع للصنف «{item.Name}» (جملة/مفرق أو سعر مخصص).");
            }

            decimal already = pendingOut.GetValueOrDefault(item.Id, 0m);
            decimal available = onHand.GetValueOrDefault(item.Id, 0m) - already;
            if (available < input.Quantity)
            {
                return MaterialInvoiceResult.Fail(
                  $"الكمية غير كافية للصنف «{item.Name}» (المتاح: {available:0.###} قطعة).");
            }

            decimal lineTotal = Math.Round(input.Quantity * unitPrice, 2);
            invoice.Lines.Add(new MaterialSalesInvoiceLine
            {
                WarehouseItemId = item.Id,
                PriceMode = input.PriceMode,
                Quantity = input.Quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });

            pendingOut[item.Id] = already + input.Quantity;
            total += lineTotal;
        }

        invoice.TotalAmount = total;
        _context.MaterialSalesInvoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        foreach (MaterialSalesInvoiceLine line in invoice.Lines)
        {
            _context.WarehouseMovements.Add(new WarehouseMovement
            {
                CompanyNetworkId = companyNetworkId,
                WarehouseItemId = line.WarehouseItemId,
                MovementType = WarehouseMovementType.Out,
                Quantity = line.Quantity,
                MovementDate = invoice.InvoiceDate,
                MaterialSalesInvoiceId = invoice.Id,
                Notes = $"بيع مواد — فاتورة #{invoice.Id}",
                CreatedByUserId = userId
            });
        }

        await _context.SaveChangesAsync(ct);
        return MaterialInvoiceResult.Ok(invoice.Id);
    }

    private static MaterialInvoiceResult? ValidateSalesLinePrice(
      WarehouseItem item,
      MaterialSalesLineInput input,
      PricingCurrency invoiceCurrency)
    {
        if (input.UnitPrice is not > 0m)
        {
            return MaterialInvoiceResult.Fail($"أدخل سعر القطعة (الافرادي) للصنف «{item.Name}».");
        }

        if (input.PriceMode == MaterialSalePriceMode.Custom
            && item.PurchaseCurrency.HasValue
            && invoiceCurrency != item.PurchaseCurrency.Value)
        {
            return MaterialInvoiceResult.Fail(
              $"السعر المخصص للصنف «{item.Name}» يجب أن يُسجَّل بعملة الشراء ({CurrencyHelper.GetSymbol(item.PurchaseCurrency.Value)}). غيّر عملة الفاتورة أو اختر جملة/مفرق.");
        }

        return null;
    }

    private static decimal ResolveSaleUnitPrice(WarehouseItem item, MaterialSalesLineInput input)
    {
        if (input.UnitPrice > 0m)
        {
            return input.UnitPrice;
        }

        if (input.CustomUnitPrice is > 0m)
        {
            return input.CustomUnitPrice.Value;
        }

        return input.PriceMode switch
        {
            MaterialSalePriceMode.Wholesale => item.WholesalePrice ?? 0m,
            MaterialSalePriceMode.Retail => item.RetailPrice ?? 0m,
            _ => 0m
        };
    }

    private async Task<(int? Id, string? Name, string? Error)> ResolveErpCustomerAsync(
      int companyNetworkId,
      int? erpCustomerId,
      string? customerName,
      CancellationToken ct)
    {
        if (erpCustomerId is > 0)
        {
            ErpCustomer? customer = await _context.ErpCustomers.AsNoTracking()
              .FirstOrDefaultAsync(c => c.Id == erpCustomerId && c.CompanyNetworkId == companyNetworkId && c.IsActive, ct);
            if (customer == null)
            {
                return (null, null, "عميل ERP غير موجود أو غير نشط.");
            }

            return (customer.Id, customer.Name, null);
        }

        return (null, string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(), null);
    }

    private async Task<(int? Id, string? Name, string? Error)> ResolveErpSupplierAsync(
      int companyNetworkId,
      int? erpSupplierId,
      string? supplierName,
      CancellationToken ct)
    {
        if (erpSupplierId is > 0)
        {
            ErpSupplier? supplier = await _context.ErpSuppliers.AsNoTracking()
              .FirstOrDefaultAsync(s => s.Id == erpSupplierId && s.CompanyNetworkId == companyNetworkId && s.IsActive, ct);
            if (supplier == null)
            {
                return (null, null, "مورد ERP غير موجود أو غير نشط.");
            }

            return (supplier.Id, supplier.Name, null);
        }

        return (null, string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim(), null);
    }
}
