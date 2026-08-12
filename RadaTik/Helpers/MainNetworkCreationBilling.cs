using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Helpers;

/// <summary>
/// رسوم إنشاء شبكة الشركة الرئيسية (تسعير OneTime) — تُؤجَّل عند رصيد 0 وتُطبَّق بعد التغذية.
/// </summary>
public static class MainNetworkCreationBilling
{
    public const string OneTimeChargeNoteMarker = "إنشاء شبكة الشركة الرئيسية";

    public static async Task<bool> TryApplyOneTimeCreationChargeAsync(
        ApplicationDbContext db,
        int companyNetworkId,
        string networkName,
        string featureKey,
        decimal amountSyp,
        string actorUserId,
        CancellationToken ct = default)
    {
        decimal amount = WalletMath.CeilSyp(amountSyp);
        if (amount <= 0)
        {
            return false;
        }

        bool alreadyCharged = await db.NetworkWalletTransactions.AsNoTracking()
            .AnyAsync(t =>
                t.NetworkId == companyNetworkId &&
                t.Type == NetworkWalletTransactionType.ServiceCharge &&
                t.Notes != null &&
                t.Notes.Contains(OneTimeChargeNoteMarker),
                ct);
        if (alreadyCharged)
        {
            return false;
        }

        Network? company = await db.Networks
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct);
        if (company == null || company.Balance < amount)
        {
            return false;
        }

        DateTime now = DateTime.Now;
        decimal previousBalance = company.Balance;
        company.Balance -= amount;

        db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
        {
            NetworkId = companyNetworkId,
            Type = NetworkWalletTransactionType.ServiceCharge,
            SignedAmount = -amount,
            PreviousBalance = previousBalance,
            NewBalance = company.Balance,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            Notes =
                $"{OneTimeChargeNoteMarker}: {networkName} ({featureKey} / {PricingBillingPeriod.OneTime} / {PricingChargeUnit.PerNetwork})"
        });

        await EnsureMainNetworkUnitLedgerAsync(db, companyNetworkId, featureKey, now, charged: true, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// يسجّل وحدة الشبكة الرئيسية في الـ Ledger حتى لا يُعاد خصمها كـ «عنصر جديد» عند مزامنة الاستخدام.
    /// </summary>
    public static async Task EnsureMainNetworkUnitLedgerAsync(
        ApplicationDbContext db,
        int companyNetworkId,
        string featureKey,
        DateTime now,
        bool charged,
        CancellationToken ct = default)
    {
        NetworkServiceSubscription? sub = await db.NetworkServiceSubscriptions
            .Where(s =>
                s.NetworkId == companyNetworkId &&
                s.FeatureKey == featureKey &&
                s.Status == NetworkServiceSubscriptionStatus.Active)
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(ct);
        if (sub == null)
        {
            return;
        }

        string unitKey = "N:" + companyNetworkId;
        bool exists = await db.ServiceUnitChargeLedgers.AnyAsync(
            l =>
                l.NetworkServiceSubscriptionId == sub.Id &&
                l.ChargeUnit == PricingChargeUnit.PerNetwork &&
                l.UnitEntityKey == unitKey,
            ct);
        if (exists)
        {
            return;
        }

        db.ServiceUnitChargeLedgers.Add(new ServiceUnitChargeLedger
        {
            NetworkServiceSubscriptionId = sub.Id,
            ChargeUnit = PricingChargeUnit.PerNetwork,
            UnitEntityKey = unitKey,
            IsActive = true,
            FirstChargedAt = charged ? now : null,
            LastChargedAt = charged ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
