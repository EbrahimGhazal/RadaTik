using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services.SystemAdminPricing;

namespace RadTik.Services.PricingPolicies;

public interface ISenderCreationWorkflowStrategy
{
    bool CanHandle(bool actorIsEmployee);

    Task<SenderCreateOutcome> ExecuteAsync(
        ApplicationDbContext db,
        Sector sector,
        int selectedNetworkId,
        string actorUserId,
        CancellationToken ct = default);
}

public sealed class ImmediateSenderCreationWorkflowStrategy : ISenderCreationWorkflowStrategy
{
    public bool CanHandle(bool actorIsEmployee) => !actorIsEmployee;

    public async Task<SenderCreateOutcome> ExecuteAsync(
        ApplicationDbContext db,
        Sector sector,
        int selectedNetworkId,
        string actorUserId,
        CancellationToken ct = default)
    {
        var company = await SenderPricingOrchestrator.ResolveCompanyAsync(db, selectedNetworkId, ct);
        if (company == null)
        {
            return new SenderCreateOutcome
            {
                Success = false,
                ErrorMessage = "تعذر العثور على حساب الشركة التي سيتم الخصم منها."
            };
        }

        var initialPricing = await SenderPricingOrchestrator.ResolveInitialSenderPricingAsync(db, ct);
        var policy = RecurringPricingPolicyCodec.ReadFromNotes(initialPricing?.Notes);
        var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(db, company.Id);
        var activeSectorsCount = await db.Sectors
            .AsNoTracking()
            .CountAsync(s => s.IsActive && s.NetworkId.HasValue && companyScope.Contains(s.NetworkId.Value), ct);
        var amountToCharge = (initialPricing == null || activeSectorsCount < policy.FreeInitialUnits)
            ? 0m
            : WalletMath.CeilSyp(initialPricing.AmountSYP);
        if (amountToCharge > 0m && company.Balance < amountToCharge)
        {
            return new SenderCreateOutcome
            {
                Success = false,
                ErrorMessage = $"رصيد محفظة مدير الشركة غير كافٍ لإضافة مرسل جديد. المطلوب {amountToCharge:N2} ل.س.ج والرصيد {company.Balance:N2} ل.س.ج."
            };
        }

        db.Sectors.Add(sector);

        if (amountToCharge > 0m)
        {
            var previousBalance = company.Balance;
            company.Balance -= amountToCharge;

            db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
            {
                NetworkId = company.Id,
                Type = NetworkWalletTransactionType.ServiceCharge,
                SignedAmount = -amountToCharge,
                PreviousBalance = previousBalance,
                NewBalance = company.Balance,
                CreatedByUserId = actorUserId,
                CreatedAt = DateTime.Now,
                Notes = $"خصم إضافة مرسل جديد (Sector: {sector.Name ?? sector.Id.ToString()}) بقيمة أولية."
            });
        }

        await db.SaveChangesAsync(ct);

        return new SenderCreateOutcome
        {
            Success = true,
            IsDeferred = false,
            Message = amountToCharge > 0m
                ? $"تم إضافة المرسل بنجاح، وتم خصم قيمة الإضافة ({amountToCharge:N2} ل.س.ج)."
                : "تم إضافة المرسل بنجاح ضمن العدد المجاني قبل بدء التسعير."
        };
    }
}

public sealed class ApprovalGatedSenderCreationWorkflowStrategy : ISenderCreationWorkflowStrategy
{
    public bool CanHandle(bool actorIsEmployee) => actorIsEmployee;

    public async Task<SenderCreateOutcome> ExecuteAsync(
        ApplicationDbContext db,
        Sector sector,
        int selectedNetworkId,
        string actorUserId,
        CancellationToken ct = default)
    {
        var selectedNetwork = await db.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
        var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

        sector.IsActive = false;
        db.Sectors.Add(sector);
        await db.SaveChangesAsync(ct);

        var pricing = await SenderPricingOrchestrator.ResolveInitialSenderPricingAsync(db, ct);

        var estimatedSyp = pricing != null ? WalletMath.CeilSyp(pricing.AmountSYP) : 0m;
        var estimatedUsd = 0m;
        var billingPeriod = pricing?.BillingPeriod ?? PricingBillingPeriod.OneTime;

        var request = new NetworkServiceRequest
        {
            NetworkId = companyNetworkId,
            FeatureKey = FeatureKeys.Sectors,
            FeaturePricingId = pricing?.Id,
            BillingPeriod = billingPeriod,
            AmountSYP = estimatedSyp,
            AmountUSD = estimatedUsd,
            Currency = PricingCurrency.SYP_New,
            Status = NetworkServiceRequestStatus.Pending,
            RequestedByUserId = actorUserId,
            RequestedAt = DateTime.Now,
            Notes = BuildPendingSectorMeta(sector.Id, selectedNetworkId)
        };

        db.NetworkServiceRequests.Add(request);
        await db.SaveChangesAsync(ct);

        await SenderPricingOrchestrator.CreateManagerApprovalNotificationAsync(db, companyNetworkId, actorUserId, sector, ct);

        return new SenderCreateOutcome
        {
            Success = true,
            IsDeferred = true,
            Message = $"تم إرسال طلب إضافة المرسل إلى مدير الشركة للموافقة (رقم الطلب: {request.Id}). سيتم التفعيل واحتساب الرسوم فقط بعد اعتماد الطلب."
        };
    }

    public static string BuildPendingSectorMeta(int sectorId, int selectedNetworkId) =>
        $"SECTOR_CREATE_PENDING:{sectorId};Network:{selectedNetworkId}";
}

public sealed class SenderCreateOutcome
{
    public bool Success { get; init; }
    public bool IsDeferred { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

public enum SenderApprovalOutcomeType
{
    NotApplicable = 0,
    ApprovedAndCharged = 1,
    InsufficientBalance = 2,
    SectorNotFound = 3,
    CompanyNotFound = 4
}

public sealed class SenderApprovalOutcome
{
    public SenderApprovalOutcomeType OutcomeType { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool Handled => OutcomeType != SenderApprovalOutcomeType.NotApplicable;
}

public interface ISenderPricingOrchestrator
{
    Task<SenderCreateOutcome> HandleSectorCreationAsync(
        Sector sector,
        int selectedNetworkId,
        string actorUserId,
        bool actorIsEmployee,
        CancellationToken ct = default);

    Task<SenderApprovalOutcome> TryHandlePendingApprovalAsync(
        NetworkServiceRequest request,
        string adminUserId,
        string? notes,
        CancellationToken ct = default);

    Task TryHandlePendingRejectionAsync(NetworkServiceRequest request, CancellationToken ct = default);
}

public sealed class SenderPricingOrchestrator : ISenderPricingOrchestrator
{
    private readonly ApplicationDbContext _db;
    private readonly IReadOnlyList<ISenderCreationWorkflowStrategy> _createStrategies;

    public SenderPricingOrchestrator(
        ApplicationDbContext db,
        IEnumerable<ISenderCreationWorkflowStrategy> createStrategies)
    {
        _db = db;
        _createStrategies = createStrategies.ToList();
    }

    public async Task<SenderCreateOutcome> HandleSectorCreationAsync(
        Sector sector,
        int selectedNetworkId,
        string actorUserId,
        bool actorIsEmployee,
        CancellationToken ct = default)
    {
        var strategy = _createStrategies.FirstOrDefault(s => s.CanHandle(actorIsEmployee))
                       ?? throw new InvalidOperationException("No sender creation workflow strategy is configured.");

        return await strategy.ExecuteAsync(_db, sector, selectedNetworkId, actorUserId, ct);
    }

    public async Task<SenderApprovalOutcome> TryHandlePendingApprovalAsync(
        NetworkServiceRequest request,
        string adminUserId,
        string? notes,
        CancellationToken ct = default)
    {
        if (!TryParsePendingSectorMeta(request.Notes, out var pendingSectorId))
        {
            return new SenderApprovalOutcome
            {
                OutcomeType = SenderApprovalOutcomeType.NotApplicable
            };
        }

        var company = await ResolveCompanyAsync(_db, request.NetworkId, ct);
        if (company == null)
        {
            return new SenderApprovalOutcome
            {
                OutcomeType = SenderApprovalOutcomeType.CompanyNotFound,
                Message = "تعذر العثور على شركة الشبكة المطلوبة."
            };
        }

        var scopeNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, request.NetworkId);
        var sector = await _db.Sectors.FirstOrDefaultAsync(s =>
            s.Id == pendingSectorId &&
            s.NetworkId.HasValue &&
            scopeNetworkIds.Contains(s.NetworkId.Value), ct);
        if (sector == null)
        {
            return new SenderApprovalOutcome
            {
                OutcomeType = SenderApprovalOutcomeType.SectorNotFound,
                Message = "تعذر العثور على المرسل المرتبط بهذا الطلب."
            };
        }

        var currentPricing = await ResolveInitialSenderPricingAsync(_db, ct);
        var policy = RecurringPricingPolicyCodec.ReadFromNotes(currentPricing?.Notes);
        var activeSectorsCount = await _db.Sectors
            .AsNoTracking()
            .CountAsync(s =>
                s.IsActive &&
                s.Id != sector.Id &&
                s.NetworkId.HasValue &&
                scopeNetworkIds.Contains(s.NetworkId.Value), ct);

        var amountToCharge = currentPricing != null
            ? (activeSectorsCount < policy.FreeInitialUnits ? 0m : WalletMath.CeilSyp(currentPricing.AmountSYP))
            : WalletMath.CeilSyp(request.AmountSYP);

        if (amountToCharge > 0m && company.Balance < amountToCharge)
        {
            return new SenderApprovalOutcome
            {
                OutcomeType = SenderApprovalOutcomeType.InsufficientBalance,
                Message = $"لا يمكن الموافقة حالياً: رصيد الشركة غير كافٍ. المطلوب {amountToCharge:N2} ل.س.ج والرصيد {company.Balance:N2} ل.س.ج."
            };
        }

        NetworkWalletTransaction? walletTx = null;
        var now = DateTime.Now;
        if (amountToCharge > 0m)
        {
            var previousBalance = company.Balance;
            company.Balance -= amountToCharge;

            walletTx = new NetworkWalletTransaction
            {
                NetworkId = company.Id,
                Type = NetworkWalletTransactionType.ServiceCharge,
                SignedAmount = -amountToCharge,
                PreviousBalance = previousBalance,
                NewBalance = company.Balance,
                NetworkServiceRequestId = request.Id,
                CreatedByUserId = adminUserId,
                CreatedAt = now,
                Notes = $"خصم تفعيل مرسل معلق (Sector #{sector.Id}) بعد الموافقة."
            };
            _db.NetworkWalletTransactions.Add(walletTx);
        }

        sector.IsActive = true;
        request.Status = NetworkServiceRequestStatus.Approved;
        request.DecidedByUserId = adminUserId;
        request.DecidedAt = now;
        request.Notes = string.IsNullOrWhiteSpace(notes)
            ? $"{request.Notes ?? ""}\nApproved sector activation."
            : $"{request.Notes ?? ""}\n{notes.Trim()}";

        await _db.SaveChangesAsync(ct);

        if (walletTx != null)
        {
            request.ChargeWalletTransactionId = walletTx.Id;
            await _db.SaveChangesAsync(ct);
        }

        return new SenderApprovalOutcome
        {
            OutcomeType = SenderApprovalOutcomeType.ApprovedAndCharged,
            Message = "تمت الموافقة على إضافة المرسل وتفعيله مع خصم الرسوم من محفظة مدير الشركة."
        };
    }

    public async Task TryHandlePendingRejectionAsync(NetworkServiceRequest request, CancellationToken ct = default)
    {
        if (!TryParsePendingSectorMeta(request.Notes, out var pendingSectorId))
        {
            return;
        }

        var scopeNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, request.NetworkId);
        var sector = await _db.Sectors.FirstOrDefaultAsync(s =>
            s.Id == pendingSectorId &&
            s.NetworkId.HasValue &&
            scopeNetworkIds.Contains(s.NetworkId.Value), ct);
        if (sector != null)
        {
            sector.IsActive = false;
        }
    }

    public static bool TryParsePendingSectorMeta(string? notes, out int sectorId)
    {
        sectorId = 0;
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        const string marker = "SECTOR_CREATE_PENDING:";
        var idx = notes.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        var start = idx + marker.Length;
        var tail = notes[start..];
        var endIdx = tail.IndexOfAny([';', '\n', '\r', ' ']);
        var token = endIdx >= 0 ? tail[..endIdx] : tail;
        return int.TryParse(token, out sectorId) && sectorId > 0;
    }

    internal static async Task CreateManagerApprovalNotificationAsync(
        ApplicationDbContext db,
        int companyNetworkId,
        string actorUserId,
        Sector sector,
        CancellationToken ct = default)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(db, companyNetworkId);

        var managerUserId = await db.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId)
            .Select(n => n.ManagerUserId)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(managerUserId))
        {
            recipients.Add(managerUserId);
        }

        var roleUserIds = await db.Users
            .AsNoTracking()
            .Where(u => u.NetworkId.HasValue && companyScope.Contains(u.NetworkId.Value))
            .Join(db.UserRoles.AsNoTracking(), u => u.Id, ur => ur.UserId, (u, ur) => new { u.Id, ur.RoleId })
            .Join(db.Roles.AsNoTracking().Where(r => r.Name == RoleNames.NetworkAdministrator),
                x => x.RoleId,
                r => r.Id,
                (x, _) => x.Id)
            .Distinct()
            .ToListAsync(ct);
        foreach (var uid in roleUserIds)
        {
            recipients.Add(uid);
        }

        if (recipients.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        var keyPrefix = $"EmployeeSectorCreatePending:{sector.Id}";
        var rows = recipients.Select(uid => new UserNotification
        {
            Key = $"{keyPrefix}:{uid}:{Guid.NewGuid():N}",
            UserId = uid,
            NetworkId = companyNetworkId,
            Type = NotificationType.SubscriptionExpiring,
            Title = "طلب إضافة مرسل من موظف",
            Message = $"قدّم الموظف طلب إضافة مرسل جديد ({sector.Name ?? $"#{sector.Id}"}). يرجى مراجعة الطلب واعتماده.",
            CreatedAt = now,
            IsRead = false
        });

        db.UserNotifications.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<FeaturePricing?> ResolveInitialSenderPricingAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        return await db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == FeatureKeys.Sectors &&
                p.ChargeUnit == PricingChargeUnit.PerSector)
            .OrderBy(p => p.BillingPeriod == PricingBillingPeriod.OneTime ? 0 : 1)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }

    internal static async Task<Network?> ResolveCompanyAsync(ApplicationDbContext db, int anyNetworkId, CancellationToken ct = default)
    {
        var selected = await db.Networks.FirstOrDefaultAsync(n => n.Id == anyNetworkId, ct);
        if (selected == null)
        {
            return null;
        }

        var companyNetworkId = selected.ParentNetworkId ?? selected.Id;
        return await db.Networks.FirstOrDefaultAsync(n =>
            n.Id == companyNetworkId &&
            n.ParentNetworkId == null, ct);
    }
}
