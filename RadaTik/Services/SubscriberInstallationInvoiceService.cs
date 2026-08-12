using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class SubscriberInstallationInvoiceService : ISubscriberInstallationInvoiceService
{
    private const string ReceiverKey = "receiver";
    private const string CableKey = "cable";
    private const string RgKey = "rg";
    private const string SwitchKey = "switch";
    private const string RouterKey = "router";
    private const string LaborKey = "labor";
    private const string TransportKey = "transport";
    private const string AccountSetupKey = "account_setup";

    private static readonly string[] DefaultServiceLineKeys = [LaborKey, TransportKey, AccountSetupKey];

    private readonly ApplicationDbContext _context;
    private readonly SubscriberInstallationWarehouseLinkService _warehouseLinkService;
    private readonly IWarehouseStockService _warehouseStock;
    private readonly ILogger<SubscriberInstallationInvoiceService> _logger;

    public SubscriberInstallationInvoiceService(
        ApplicationDbContext context,
        SubscriberInstallationWarehouseLinkService warehouseLinkService,
        IWarehouseStockService warehouseStock,
        ILogger<SubscriberInstallationInvoiceService> logger)
    {
        _context = context;
        _warehouseLinkService = warehouseLinkService;
        _warehouseStock = warehouseStock;
        _logger = logger;
    }

    public async Task CreateInitialSetupInvoiceAsync(Client client, string createdByUserId)
    {
        bool exists = await _context.SubscriberInstallationInvoices
            .AnyAsync(i => i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup);
        if (exists)
        {
            return;
        }

        SubscriberReceiverMode receiverMode = await ResolveReceiverModeAsync(client);
        SubscriberInstallationInvoice invoice = await BuildLegacyInvoiceAsync(client, createdByUserId, receiverMode, SubscriberInstallationInvoiceKind.InitialSetup);
        _context.SubscriberInstallationInvoices.Add(invoice);
        client.Balance -= invoice.TotalAmount;
    }

    public async Task<int> CreateDraftInitialSetupInvoiceAsync(
        Client client,
        NewSubscriberWizardPath path,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        bool exists = await _context.SubscriberInstallationInvoices
            .AnyAsync(i => i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup, cancellationToken);
        if (exists)
        {
            return await _context.SubscriberInstallationInvoices
                .Where(i => i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup)
                .Select(i => i.Id)
                .FirstAsync(cancellationToken);
        }

        SubscriberInstallationInvoice invoice = await BuildWizardDraftInvoiceAsync(client, path, createdByUserId, cancellationToken);
        _context.SubscriberInstallationInvoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }

    public async Task<FinalizeInvoiceResult> UpdateDraftInvoiceItemsAsync(
        int invoiceId,
        int networkId,
        IReadOnlyList<DraftInvoiceLineUpdate> lineUpdates,
        CancellationToken cancellationToken = default)
    {
        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.NetworkId == networkId, cancellationToken);
        if (invoice == null)
        {
            return new FinalizeInvoiceResult { Success = false, ErrorMessage = "الفاتورة غير موجودة." };
        }

        if (invoice.Status != SubscriberInstallationInvoiceStatus.Draft)
        {
            return new FinalizeInvoiceResult { Success = false, ErrorMessage = "يمكن تعديل بنود الفاتورة في حالة «مسودة» فقط." };
        }

        Dictionary<string, SubscriberInstallationMaterialPrice> prices =
            await GetOrSeedMaterialPricesAsync(networkId);

        Dictionary<int, DraftInvoiceLineUpdate> updates = lineUpdates.ToDictionary(x => x.ItemId);
        foreach (SubscriberInstallationInvoiceItem item in invoice.Items)
        {
            if (!updates.TryGetValue(item.Id, out DraftInvoiceLineUpdate? lineUpdate))
            {
                continue;
            }

            item.Quantity = Math.Max(0m, lineUpdate.Quantity);
            item.LineTotal = Math.Round(item.UnitPrice * item.Quantity, 2);

            if (!item.IsStockItem || string.IsNullOrWhiteSpace(item.MaterialKey))
            {
                continue;
            }

            if (!prices.TryGetValue(item.MaterialKey, out SubscriberInstallationMaterialPrice? material))
            {
                continue;
            }

            HashSet<int> allowedWarehouseIds = material.WarehouseLinks
                .Select(l => l.WarehouseItemId)
                .ToHashSet();
            if (material.WarehouseItemId is > 0)
            {
                allowedWarehouseIds.Add(material.WarehouseItemId.Value);
            }

            if (lineUpdate.WarehouseItemId is > 0)
            {
                if (allowedWarehouseIds.Count > 0 && !allowedWarehouseIds.Contains(lineUpdate.WarehouseItemId.Value))
                {
                    return new FinalizeInvoiceResult
                    {
                        Success = false,
                        ErrorMessage = $"الموديل المختار غير مرتبط بمادة «{item.ItemName}» في تسعير التركيب."
                    };
                }

                item.WarehouseItemId = lineUpdate.WarehouseItemId;
            }
            else if (!item.WarehouseItemId.HasValue)
            {
                item.WarehouseItemId = ResolveDefaultWarehouseItemId(material);
            }
        }

        invoice.TotalAmount = invoice.Items.Sum(i => i.LineTotal);
        invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
        invoice.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return new FinalizeInvoiceResult { Success = true };
    }

    public async Task<int> CreatePrivateInitialSetupInvoiceAsync(Client client, string createdByUserId, CancellationToken cancellationToken = default)
    {
        bool exists = await _context.SubscriberInstallationInvoices
            .AnyAsync(i => i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup, cancellationToken);
        if (exists)
        {
            int existingId = await _context.SubscriberInstallationInvoices
                .Where(i => i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup)
                .Select(i => i.Id)
                .FirstAsync(cancellationToken);
            return existingId;
        }

        SubscriberInstallationInvoice invoice = await BuildPrivateInvoiceAsync(client, createdByUserId, cancellationToken);
        _context.SubscriberInstallationInvoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }

    public async Task CreateReceiverUpgradeInvoiceIfNeededAsync(Client client, int? previousReceiverId, string createdByUserId)
    {
        if (previousReceiverId == client.ReceiverId || !client.ReceiverId.HasValue)
        {
            return;
        }

        bool isShared = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.ReceiverId == client.ReceiverId && c.Id != client.Id);
        if (!isShared)
        {
            return;
        }

        bool exists = await _context.SubscriberInstallationInvoices
            .AnyAsync(i => i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.ReceiverUpgradeToShared);
        if (exists)
        {
            return;
        }

        string networkName = await ResolveNetworkNameAsync(client.NetworkId);
        Dictionary<string, SubscriberInstallationMaterialPrice> prices = await GetOrSeedMaterialPricesAsync(client.NetworkId ?? 0);
        decimal switchPrice = prices[SwitchKey].UnitPrice;
        decimal amount = switchPrice;
        SubscriberInstallationInvoice invoice = new SubscriberInstallationInvoice
        {
            ClientId = client.Id,
            NetworkId = client.NetworkId ?? 0,
            CompanyName = networkName,
            ClientName = client.Name ?? client.UserName ?? $"Client-{client.Id}",
            ReceiverMode = SubscriberReceiverMode.Shared,
            Kind = SubscriberInstallationInvoiceKind.ReceiverUpgradeToShared,
            Status = SubscriberInstallationInvoiceStatus.PendingWalletPayment,
            TotalAmount = amount,
            PaidAmount = 0m,
            RemainingAmount = amount,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = new List<SubscriberInstallationInvoiceItem>
            {
                BuildItem(prices[SwitchKey], isStock: true)
            }
        };

        _context.SubscriberInstallationInvoices.Add(invoice);
        client.Balance -= amount;
        _logger.LogInformation("Created shared-receiver upgrade invoice for client {ClientId} with amount {Amount}", client.Id, amount);
    }

    public async Task<FinalizeInvoiceResult> FinalizeInvoiceAsync(
        int invoiceId,
        int networkId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.NetworkId == networkId, cancellationToken);
        if (invoice == null)
        {
            return new FinalizeInvoiceResult { Success = false, ErrorMessage = "الفاتورة غير موجودة." };
        }

        if (invoice.Status != SubscriberInstallationInvoiceStatus.Draft)
        {
            return new FinalizeInvoiceResult { Success = false, ErrorMessage = "يمكن تثبيت الفاتورة من حالة «مسودة» فقط." };
        }

        if (invoice.Kind != SubscriberInstallationInvoiceKind.InitialSetup)
        {
            return new FinalizeInvoiceResult { Success = false, ErrorMessage = "التثبيت النهائي متاح لفواتير التركيب الأولي فقط." };
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(invoice.NetworkId, cancellationToken);
        Dictionary<int, decimal> onHand = await _warehouseStock.GetOnHandByItemIdAsync(companyNetworkId, cancellationToken);

        foreach (SubscriberInstallationInvoiceItem item in invoice.Items.Where(i => i.IsStockItem && i.WarehouseItemId.HasValue))
        {
            int whId = item.WarehouseItemId!.Value;
            decimal qty = item.Quantity;
            decimal available = onHand.GetValueOrDefault(whId, 0m);
            if (available < qty)
            {
                return new FinalizeInvoiceResult
                {
                    Success = false,
                    ErrorMessage = $"الكمية غير كافية في المستودع للصنف «{item.ItemName}» (المتاح: {available:0.##})."
                };
            }

            _context.WarehouseMovements.Add(new WarehouseMovement
            {
                CompanyNetworkId = companyNetworkId,
                WarehouseItemId = whId,
                MovementType = WarehouseMovementType.Out,
                Quantity = qty,
                MovementDate = DateTime.Now,
                Notes = $"تركيب مشترك — فاتورة #{invoice.Id} / {invoice.ClientName}",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            onHand[whId] = available - qty;
        }

        Client? billingClient = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == invoice.ClientId && c.NetworkId == networkId, cancellationToken);
        if (billingClient != null && invoice.TotalAmount > 0m)
        {
            billingClient.Balance -= invoice.TotalAmount;
            billingClient.LastUpdated = DateTime.Now;
        }

        invoice.Status = SubscriberInstallationInvoiceStatus.Finalized;
        invoice.FinalizedAt = DateTime.UtcNow;
        invoice.FinalizedByUserId = userId;
        invoice.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Finalized installation invoice {InvoiceId} for client {ClientId}", invoice.Id, invoice.ClientId);

        return new FinalizeInvoiceResult { Success = true };
    }

    public async Task<RegisterInstallationPaymentResult> RegisterPaymentAsync(
        int invoiceId,
        int networkId,
        string userId,
        decimal amount,
        SubscriberInstallationPaymentMethod method,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            return new RegisterInstallationPaymentResult { Success = false, ErrorMessage = "قيمة الدفعة يجب أن تكون أكبر من صفر." };
        }

        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.NetworkId == networkId, cancellationToken);
        if (invoice == null)
        {
            return new RegisterInstallationPaymentResult { Success = false, ErrorMessage = "الفاتورة غير موجودة." };
        }

        if (invoice.Status is SubscriberInstallationInvoiceStatus.Cancelled or SubscriberInstallationInvoiceStatus.Paid)
        {
            return new RegisterInstallationPaymentResult { Success = false, ErrorMessage = "لا يمكن تسجيل دفعة على فاتورة ملغاة أو مسددة." };
        }

        bool canPay = invoice.Status is SubscriberInstallationInvoiceStatus.Finalized
            or SubscriberInstallationInvoiceStatus.PendingWalletPayment
            or SubscriberInstallationInvoiceStatus.PartiallyPaid;
        if (!canPay)
        {
            return new RegisterInstallationPaymentResult { Success = false, ErrorMessage = "يجب تثبيت الفاتورة نهائياً قبل التحصيل." };
        }

        Client? client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == invoice.ClientId && c.NetworkId == networkId, cancellationToken);
        if (client == null)
        {
            return new RegisterInstallationPaymentResult { Success = false, ErrorMessage = "المشترك غير موجود." };
        }

        decimal appliedAmount = Math.Min(amount, invoice.RemainingAmount);
        if (appliedAmount <= 0m)
        {
            return new RegisterInstallationPaymentResult { Success = false, ErrorMessage = "لا يوجد مبلغ متبقٍ." };
        }

        int? paymentTxId = null;
        if (method == SubscriberInstallationPaymentMethod.Wallet)
        {
            decimal previousBalance = client.Balance;
            client.Balance += appliedAmount;
            client.LastUpdated = DateTime.Now;

            PaymentTransaction paymentTx = new PaymentTransaction
            {
                ClientId = client.Id,
                NetworkId = client.NetworkId,
                PaymentDate = DateTime.Now,
                ReceivedByUserId = userId,
                Notes = string.IsNullOrWhiteSpace(notes) ? $"تسديد فاتورة تجهيز #{invoice.Id}" : notes.Trim(),
                OperationType = "SubscriberInstallationInvoicePayment",
                ReferenceNumber = $"SII-{invoice.Id}-{DateTime.Now:yyyyMMddHHmmss}",
                PreviousClientBalance = previousBalance,
                NewClientBalance = client.Balance,
                PreviousPointBalance = 0m,
                NewPointBalance = 0m
            };
            PaymentTransactionHelper.ApplySingleCurrencySyp(paymentTx, appliedAmount, client.AccountCurrency);
            _context.PaymentTransactions.Add(paymentTx);
            await _context.SaveChangesAsync(cancellationToken);
            paymentTxId = paymentTx.Id;
        }
        else
        {
            int companyNetworkId = await ResolveCompanyNetworkIdAsync(invoice.NetworkId, cancellationToken);
            _context.MoneyDiaryEntries.Add(new MoneyDiaryEntry
            {
                CompanyNetworkId = companyNetworkId,
                EntryType = MoneyDiaryEntryType.Income,
                CategoryKey = "income_installation",
                Amount = appliedAmount,
                Currency = PricingCurrency.SYP_New,
                EntryDate = DateTime.Today,
                Description = $"تحصيل نقدي — فاتورة تركيب #{invoice.Id} — {invoice.ClientName}",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        invoice.PaidAmount += appliedAmount;
        invoice.RemainingAmount = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);
        invoice.Status = invoice.RemainingAmount <= 0m
            ? SubscriberInstallationInvoiceStatus.Paid
            : SubscriberInstallationInvoiceStatus.PartiallyPaid;
        invoice.UpdatedAt = DateTime.Now;

        _context.SubscriberInstallationInvoicePayments.Add(new SubscriberInstallationInvoicePayment
        {
            SubscriberInstallationInvoiceId = invoice.Id,
            PaymentTransactionId = paymentTxId,
            Amount = appliedAmount,
            ReceivedByUserId = userId,
            PaymentMethod = method,
            Notes = notes,
            CreatedAt = DateTime.Now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new RegisterInstallationPaymentResult { Success = true, NewStatus = invoice.Status };
    }

    public async Task<IReadOnlyList<SubscriberInstallationMaterialPrice>> GetOrCreateMaterialPricesAsync(int networkId)
    {
        Dictionary<string, SubscriberInstallationMaterialPrice> materials = await GetOrSeedMaterialPricesAsync(networkId);
        return materials.Values
            .OrderBy(m => MaterialOrder(m.MaterialKey))
            .ToList();
    }

    public async Task SaveMaterialPricesAsync(int networkId, IEnumerable<(string MaterialKey, decimal UnitPrice, bool IsActive, int? WarehouseItemId)> rows)
    {
        await SaveMaterialPricesWithModelsAsync(
            networkId,
            rows.Select(r => new MaterialPriceSaveRow
            {
                MaterialKey = r.MaterialKey,
                UnitPrice = r.UnitPrice,
                IsActive = r.IsActive,
                DefaultWarehouseItemId = r.WarehouseItemId,
                WarehouseItemIds = r.WarehouseItemId is > 0 ? [r.WarehouseItemId.Value] : []
            }));
    }

    public async Task SaveMaterialPricesWithModelsAsync(
        int networkId,
        IEnumerable<MaterialPriceSaveRow> rows,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, SubscriberInstallationMaterialPrice> materials = await _context.SubscriberInstallationMaterialPrices
            .Include(m => m.WarehouseLinks)
            .Where(m => m.NetworkId == networkId)
            .ToDictionaryAsync(m => m.MaterialKey, m => m, cancellationToken);

        foreach (MaterialPriceSaveRow row in rows)
        {
            if (!materials.TryGetValue(row.MaterialKey, out SubscriberInstallationMaterialPrice? material))
            {
                continue;
            }

            material.UnitPrice = Math.Max(0m, row.UnitPrice);
            material.IsActive = row.IsActive;

            if (SubscriberInstallationWarehouseLinkService.IsStockMaterialKey(row.MaterialKey))
            {
                await _warehouseLinkService.SyncMaterialWarehouseLinksAsync(
                    material,
                    row.WarehouseItemIds,
                    row.DefaultWarehouseItemId,
                    cancellationToken);
            }
            else
            {
                List<SubscriberInstallationMaterialWarehouseLink> toRemove = material.WarehouseLinks.ToList();
                _context.SubscriberInstallationMaterialWarehouseLinks.RemoveRange(toRemove);
                material.WarehouseItemId = null;
                material.UpdatedAt = DateTime.Now;
            }
        }
    }

    private async Task<SubscriberInstallationInvoice> BuildWizardDraftInvoiceAsync(
        Client client,
        NewSubscriberWizardPath path,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        string networkName = await ResolveNetworkNameAsync(client.NetworkId);
        Dictionary<string, SubscriberInstallationMaterialPrice> prices = await GetOrSeedMaterialPricesAsync(client.NetworkId ?? 0);
        List<SubscriberInstallationInvoiceItem> items = [];

        SubscriberReceiverMode receiverMode;
        switch (path)
        {
            case NewSubscriberWizardPath.TowerDirect:
                AddMaterialLineIfActive(items, prices, CableKey, isStock: true);
                AddMaterialLineIfActive(items, prices, RgKey, isStock: true);
                AddMaterialLineIfActive(items, prices, RouterKey, isStock: true);
                AddDefaultServiceLines(items, prices);
                receiverMode = SubscriberReceiverMode.Private;
                break;
            case NewSubscriberWizardPath.SharedSelectReceiver:
                AddMaterialLineIfActive(items, prices, CableKey, isStock: true);
                AddMaterialLineIfActive(items, prices, RgKey, isStock: true);
                AddMaterialLineIfActive(items, prices, SwitchKey, isStock: true);
                AddMaterialLineIfActive(items, prices, RouterKey, isStock: true);
                AddDefaultServiceLines(items, prices);
                receiverMode = SubscriberReceiverMode.Shared;
                break;
            case NewSubscriberWizardPath.ExistingReceiverFromList:
                receiverMode = await ResolveReceiverModeAsync(client);
                if (receiverMode == SubscriberReceiverMode.Shared)
                {
                    AddMaterialLineIfActive(items, prices, CableKey, isStock: true);
                    AddMaterialLineIfActive(items, prices, RgKey, isStock: true);
                    AddMaterialLineIfActive(items, prices, SwitchKey, isStock: true);
                    AddMaterialLineIfActive(items, prices, RouterKey, isStock: true);
                    AddDefaultServiceLines(items, prices);
                }
                else
                {
                    AddMaterialLineIfActive(items, prices, ReceiverKey, isStock: true);
                    AddMaterialLineIfActive(items, prices, CableKey, isStock: true);
                    AddMaterialLineIfActive(items, prices, RgKey, isStock: true);
                    AddMaterialLineIfActive(items, prices, RouterKey, isStock: true);
                    AddDefaultServiceLines(items, prices);
                }

                break;
            case NewSubscriberWizardPath.PrivateNewReceiver:
            default:
                AddMaterialLineIfActive(items, prices, ReceiverKey, isStock: true);
                AddMaterialLineIfActive(items, prices, CableKey, isStock: true);
                AddMaterialLineIfActive(items, prices, RgKey, isStock: true);
                AddMaterialLineIfActive(items, prices, RouterKey, isStock: true);
                AddDefaultServiceLines(items, prices);
                receiverMode = SubscriberReceiverMode.Private;
                break;
        }

        decimal total = items.Sum(i => i.LineTotal);
        return new SubscriberInstallationInvoice
        {
            ClientId = client.Id,
            NetworkId = client.NetworkId ?? 0,
            CompanyName = networkName,
            ClientName = client.Name ?? client.UserName ?? $"Client-{client.Id}",
            ReceiverMode = receiverMode,
            Kind = SubscriberInstallationInvoiceKind.InitialSetup,
            Status = SubscriberInstallationInvoiceStatus.Draft,
            TotalAmount = total,
            PaidAmount = 0m,
            RemainingAmount = total,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = items
        };
    }

    private async Task<SubscriberInstallationInvoice> BuildPrivateInvoiceAsync(
        Client client,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        string networkName = await ResolveNetworkNameAsync(client.NetworkId);
        Dictionary<string, SubscriberInstallationMaterialPrice> prices = await GetOrSeedMaterialPricesAsync(client.NetworkId ?? 0);

        List<SubscriberInstallationInvoiceItem> items = [];
        AddMaterialLineIfActive(items, prices, ReceiverKey, isStock: true);
        AddMaterialLineIfActive(items, prices, CableKey, isStock: true);
        AddMaterialLineIfActive(items, prices, RgKey, isStock: true);
        AddMaterialLineIfActive(items, prices, RouterKey, isStock: true);
        AddDefaultServiceLines(items, prices);

        decimal total = items.Sum(i => i.LineTotal);
        return new SubscriberInstallationInvoice
        {
            ClientId = client.Id,
            NetworkId = client.NetworkId ?? 0,
            CompanyName = networkName,
            ClientName = client.Name ?? client.UserName ?? $"Client-{client.Id}",
            ReceiverMode = SubscriberReceiverMode.Private,
            Kind = SubscriberInstallationInvoiceKind.InitialSetup,
            Status = SubscriberInstallationInvoiceStatus.Draft,
            TotalAmount = total,
            PaidAmount = 0m,
            RemainingAmount = total,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = items
        };
    }

    private async Task<SubscriberInstallationInvoice> BuildLegacyInvoiceAsync(
        Client client,
        string createdByUserId,
        SubscriberReceiverMode receiverMode,
        SubscriberInstallationInvoiceKind kind)
    {
        string networkName = await ResolveNetworkNameAsync(client.NetworkId);
        Dictionary<string, SubscriberInstallationMaterialPrice> prices = await GetOrSeedMaterialPricesAsync(client.NetworkId ?? 0);
        List<SubscriberInstallationInvoiceItem> items = [];
        AddMaterialLineIfActive(items, prices, ReceiverKey, isStock: false);
        AddMaterialLineIfActive(items, prices, CableKey, isStock: false);
        AddMaterialLineIfActive(items, prices, RgKey, isStock: false);
        AddMaterialLineIfActive(items, prices, RouterKey, isStock: false);
        if (receiverMode == SubscriberReceiverMode.Shared)
        {
            AddMaterialLineIfActive(items, prices, SwitchKey, isStock: false);
        }

        AddDefaultServiceLines(items, prices);

        decimal total = items.Sum(i => i.LineTotal);
        return new SubscriberInstallationInvoice
        {
            ClientId = client.Id,
            NetworkId = client.NetworkId ?? 0,
            CompanyName = networkName,
            ClientName = client.Name ?? client.UserName ?? $"Client-{client.Id}",
            ReceiverMode = receiverMode,
            Kind = kind,
            Status = SubscriberInstallationInvoiceStatus.PendingWalletPayment,
            TotalAmount = total,
            PaidAmount = 0m,
            RemainingAmount = total,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = items
        };
    }

    private async Task<SubscriberReceiverMode> ResolveReceiverModeAsync(Client client)
    {
        if (!client.ReceiverId.HasValue)
        {
            return SubscriberReceiverMode.Private;
        }

        bool hasPeers = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.ReceiverId == client.ReceiverId && c.Id != client.Id);

        return hasPeers ? SubscriberReceiverMode.Shared : SubscriberReceiverMode.Private;
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken cancellationToken)
    {
        int? parentId = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == networkId)
            .Select(n => n.ParentNetworkId)
            .FirstOrDefaultAsync(cancellationToken);
        return parentId ?? networkId;
    }

    private async Task<string> ResolveNetworkNameAsync(int? networkId)
    {
        if (!networkId.HasValue)
        {
            return "شركة غير محددة";
        }

        string? networkName = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == networkId.Value)
            .Select(n => n.Name)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(networkName) ? $"شركة {networkId.Value}" : networkName;
    }

    private static void AddMaterialLineIfActive(
        List<SubscriberInstallationInvoiceItem> items,
        Dictionary<string, SubscriberInstallationMaterialPrice> prices,
        string materialKey,
        bool isStock)
    {
        if (prices.TryGetValue(materialKey, out SubscriberInstallationMaterialPrice? price) && price.IsActive)
        {
            items.Add(BuildItem(price, isStock));
        }
    }

    private static void AddDefaultServiceLines(
        List<SubscriberInstallationInvoiceItem> items,
        Dictionary<string, SubscriberInstallationMaterialPrice> prices)
    {
        foreach (string key in DefaultServiceLineKeys)
        {
            AddMaterialLineIfActive(items, prices, key, isStock: false);
        }
    }

    private static bool IsServiceLineKey(string materialKey) =>
        materialKey is LaborKey or TransportKey or AccountSetupKey;

    private static SubscriberInstallationInvoiceItem BuildItem(SubscriberInstallationMaterialPrice price, bool isStock)
    {
        bool stock = isStock && !IsServiceLineKey(price.MaterialKey);
        return new SubscriberInstallationInvoiceItem
        {
            ItemName = price.MaterialName,
            MaterialKey = price.MaterialKey,
            IsStockItem = stock,
            WarehouseItemId = stock ? ResolveDefaultWarehouseItemId(price) : null,
            UnitPrice = price.UnitPrice,
            Quantity = 1m,
            LineTotal = price.UnitPrice
        };
    }

    private static int? ResolveDefaultWarehouseItemId(SubscriberInstallationMaterialPrice price)
    {
        SubscriberInstallationMaterialWarehouseLink? defaultLink =
            price.WarehouseLinks.FirstOrDefault(l => l.IsDefault);
        if (defaultLink != null)
        {
            return defaultLink.WarehouseItemId;
        }

        return price.WarehouseItemId;
    }

    private async Task<Dictionary<string, SubscriberInstallationMaterialPrice>> GetOrSeedMaterialPricesAsync(int networkId)
    {
        Dictionary<string, SubscriberInstallationMaterialPrice> materials = await _context.SubscriberInstallationMaterialPrices
            .Include(m => m.WarehouseLinks)
            .Where(m => m.NetworkId == networkId)
            .ToDictionaryAsync(m => m.MaterialKey, m => m);

        List<SubscriberInstallationMaterialPrice> defaults =
        [
            new() { NetworkId = networkId, MaterialKey = ReceiverKey, MaterialName = "المستقبل", UnitPrice = 450000m },
            new() { NetworkId = networkId, MaterialKey = CableKey, MaterialName = "الكبل", UnitPrice = 90000m },
            new() { NetworkId = networkId, MaterialKey = RgKey, MaterialName = "RG", UnitPrice = 15000m },
            new() { NetworkId = networkId, MaterialKey = SwitchKey, MaterialName = "سويتش", UnitPrice = 175000m },
            new() { NetworkId = networkId, MaterialKey = RouterKey, MaterialName = "راوتر", UnitPrice = 180000m },
            new() { NetworkId = networkId, MaterialKey = LaborKey, MaterialName = "أجور التركيب", UnitPrice = 75000m },
            new() { NetworkId = networkId, MaterialKey = TransportKey, MaterialName = "مواصلات", UnitPrice = 25000m },
            new() { NetworkId = networkId, MaterialKey = AccountSetupKey, MaterialName = "أجور إنشاء حساب جديد", UnitPrice = 15000m }
        ];

        foreach (SubscriberInstallationMaterialPrice item in defaults)
        {
            if (!materials.ContainsKey(item.MaterialKey))
            {
                _context.SubscriberInstallationMaterialPrices.Add(item);
                materials[item.MaterialKey] = item;
            }
        }

        await _context.SaveChangesAsync();
        return materials;
    }

    private static int MaterialOrder(string key)
    {
        return key switch
        {
            ReceiverKey => 1,
            CableKey => 2,
            RgKey => 3,
            RouterKey => 4,
            SwitchKey => 5,
            LaborKey => 6,
            TransportKey => 7,
            AccountSetupKey => 8,
            _ => 100
        };
    }
}
