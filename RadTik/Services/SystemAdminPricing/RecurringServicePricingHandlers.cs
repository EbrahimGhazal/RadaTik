using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Services.SystemAdminPricing;

public sealed class RecurringPricingUpdateInput
{
    public decimal InitialPriceSyp { get; init; }
    public PricingBillingPeriod RenewalBillingPeriod { get; init; }
    public decimal RenewalPricePerUnitSyp { get; init; }
    public int FreeInitialUnits { get; init; }
    public int FreeRenewalUnits { get; init; }
}

public sealed class RecurringPricingUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public static class RecurringPricingHandlerKeys
{
    public const string Networks = "Networks";
    public const string Servers = "Servers";
    public const string Sectors = "Sectors";
    public const string Receivers = "Receivers";
    public const string Clients = "Clients";
    public const string Users = "Users";
    public const string SpeedProfiles = "SpeedProfiles";
}

public interface IRecurringServicePricingHandler
{
    string HandlerKey { get; }
    Task<RecurringPricingUpdateResult> UpdateAsync(RecurringPricingUpdateInput input, CancellationToken ct = default);
}

public interface IRecurringServicePricingHandlerResolver
{
    bool TryResolve(string handlerKey, out IRecurringServicePricingHandler? handler);
}

public sealed class RecurringServicePricingHandlerResolver : IRecurringServicePricingHandlerResolver
{
    private readonly IReadOnlyDictionary<string, IRecurringServicePricingHandler> _handlers;

    public RecurringServicePricingHandlerResolver(IEnumerable<IRecurringServicePricingHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.HandlerKey, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryResolve(string handlerKey, out IRecurringServicePricingHandler? handler) =>
        _handlers.TryGetValue(handlerKey, out handler);
}

internal static class RecurringPricingRowMutator
{
    public static void MarkInactive(IEnumerable<FeaturePricing> rows, DateTime now)
    {
        foreach (var row in rows)
        {
            row.IsActive = false;
            row.UpdatedAt = now;
        }
    }
}

public abstract class RecurringServicePricingHandlerBase : IRecurringServicePricingHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    protected RecurringServicePricingHandlerBase(ApplicationDbContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public abstract string HandlerKey { get; }
    protected abstract string FeatureKey { get; }
    protected abstract PricingChargeUnit ChargeUnit { get; }
    protected abstract string ServiceDisplayName { get; }
    protected abstract string InitialNote { get; }
    protected abstract string RenewalNote { get; }
    protected abstract string LogName { get; }

    public async Task<RecurringPricingUpdateResult> UpdateAsync(RecurringPricingUpdateInput input, CancellationToken ct = default)
    {
        if (input.FreeInitialUnits < 0 || input.FreeRenewalUnits < 0)
        {
            return new RecurringPricingUpdateResult
            {
                Success = false,
                Message = RecurringPricingPolicyCodec.NonNegativeFreeUnitsMessage
            };
        }

        if (input.FreeInitialUnits > RecurringPricingPolicyCodec.MaxFreeUnitsLimit || input.FreeRenewalUnits > RecurringPricingPolicyCodec.MaxFreeUnitsLimit)
        {
            return new RecurringPricingUpdateResult
            {
                Success = false,
                Message = RecurringPricingPolicyCodec.BuildMaxFreeUnitsExceededMessage()
            };
        }

        RecurringPricingPolicy policy = input.FreeInitialUnits > 0 || input.FreeRenewalUnits > 0
            ? new PartiallyFreeRecurringPolicy(input.FreeInitialUnits, input.FreeRenewalUnits)
            : new FullyPaidRecurringPolicy();

        if (input.RenewalBillingPeriod == PricingBillingPeriod.OneTime)
        {
            return new RecurringPricingUpdateResult
            {
                Success = false,
                Message = RecurringPricingPolicyCodec.BuildRenewalPeriodicRequiredMessage(ServiceDisplayName)
            };
        }

        if (input.InitialPriceSyp < 0m || input.RenewalPricePerUnitSyp < 0m)
        {
            return new RecurringPricingUpdateResult
            {
                Success = false,
                Message = RecurringPricingPolicyCodec.BuildNonNegativePriceMessage(ServiceDisplayName)
            };
        }

        try
        {
            var now = DateTime.UtcNow;

            var initial = await _db.FeaturePricings
                .FirstOrDefaultAsync(p =>
                    p.FeatureKey == FeatureKey &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime, ct);

            if (initial == null)
            {
                initial = new FeaturePricing
                {
                    FeatureKey = FeatureKey,
                    BillingPeriod = PricingBillingPeriod.OneTime,
                    ChargeUnit = ChargeUnit,
                    Currency = PricingCurrency.SYP_New,
                    IsActive = true,
                    CreatedAt = now
                };
                _db.FeaturePricings.Add(initial);
            }

            initial.ChargeUnit = ChargeUnit;
            var initialPrice = input.InitialPriceSyp;
            var renewalPrice = input.RenewalPricePerUnitSyp;

            initial.AmountSYP = initialPrice;
            initial.AmountUSD = 0m;
            initial.Currency = PricingCurrency.SYP_New;
            initial.IsActive = true;
            initial.Notes = RecurringPricingPolicyCodec.WriteNotes(InitialNote, policy);
            initial.UpdatedAt = now;

            var renewal = await _db.FeaturePricings
                .FirstOrDefaultAsync(p =>
                    p.FeatureKey == FeatureKey &&
                    p.BillingPeriod == input.RenewalBillingPeriod, ct);

            if (renewal == null)
            {
                renewal = new FeaturePricing
                {
                    FeatureKey = FeatureKey,
                    BillingPeriod = input.RenewalBillingPeriod,
                    ChargeUnit = ChargeUnit,
                    Currency = PricingCurrency.SYP_New,
                    IsActive = true,
                    CreatedAt = now
                };
                _db.FeaturePricings.Add(renewal);
            }

            renewal.ChargeUnit = ChargeUnit;
            renewal.AmountSYP = renewalPrice;
            renewal.AmountUSD = 0m;
            renewal.Currency = PricingCurrency.SYP_New;
            renewal.IsActive = true;
            renewal.Notes = RecurringPricingPolicyCodec.WriteNotes(RenewalNote, policy);
            renewal.UpdatedAt = now;

            var oldRenewalRows = await _db.FeaturePricings
                .Where(p =>
                    p.FeatureKey == FeatureKey &&
                    p.ChargeUnit == ChargeUnit &&
                    p.BillingPeriod != PricingBillingPeriod.OneTime &&
                    p.BillingPeriod != input.RenewalBillingPeriod)
                .ToListAsync(ct);

            RecurringPricingRowMutator.MarkInactive(oldRenewalRows, now);

            await _db.SaveChangesAsync(ct);

            return new RecurringPricingUpdateResult
            {
                Success = true,
                Message = RecurringPricingPolicyCodec.BuildRecurringPricingSavedMessage(ServiceDisplayName)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update recurring pricing for {LogName}.", LogName);
            return new RecurringPricingUpdateResult
            {
                Success = false,
                Message = RecurringPricingPolicyCodec.BuildRecurringPricingSaveFailedMessage(ServiceDisplayName)
            };
        }
    }
}

public sealed class NetworkRecurringPricingHandler : RecurringServicePricingHandlerBase
{
    public NetworkRecurringPricingHandler(ApplicationDbContext db, ILogger<NetworkRecurringPricingHandler> logger) : base(db, logger) { }
    public override string HandlerKey => RecurringPricingHandlerKeys.Networks;
    protected override string FeatureKey => FeatureKeys.Networks;
    protected override PricingChargeUnit ChargeUnit => PricingChargeUnit.PerNetwork;
    protected override string ServiceDisplayName => "الشبكات";
    protected override string InitialNote => "تسعير إنشاء شبكة إضافية مع إمكانية تحديد عدد مجاني قبل بدء التسعير.";
    protected override string RenewalNote => "تجديد دوري لخدمة الشبكات مع إمكانية تحديد عدد مجاني قبل بدء رسوم التجديد.";
    protected override string LogName => "networks";
}

public sealed class ServerRecurringPricingHandler : RecurringServicePricingHandlerBase
{
    public ServerRecurringPricingHandler(ApplicationDbContext db, ILogger<ServerRecurringPricingHandler> logger) : base(db, logger) { }
    public override string HandlerKey => RecurringPricingHandlerKeys.Servers;
    protected override string FeatureKey => FeatureKeys.MikroTikServers;
    protected override PricingChargeUnit ChargeUnit => PricingChargeUnit.PerServer;
    protected override string ServiceDisplayName => "السيرفرات";
    protected override string InitialNote => "تسعير إنشاء سيرفر إضافي مع إمكانية تحديد عدد مجاني قبل بدء التسعير.";
    protected override string RenewalNote => "تجديد دوري لخدمة السيرفرات مع إمكانية تحديد عدد مجاني قبل بدء رسوم التجديد.";
    protected override string LogName => "MikroTik servers";
}

public sealed class SectorRecurringPricingHandler : RecurringServicePricingHandlerBase
{
    public SectorRecurringPricingHandler(ApplicationDbContext db, ILogger<SectorRecurringPricingHandler> logger) : base(db, logger) { }
    public override string HandlerKey => RecurringPricingHandlerKeys.Sectors;
    protected override string FeatureKey => FeatureKeys.Sectors;
    protected override PricingChargeUnit ChargeUnit => PricingChargeUnit.PerSector;
    protected override string ServiceDisplayName => "القطاعات";
    protected override string InitialNote => "تسعير إنشاء قطاع/مرسل جديد.";
    protected override string RenewalNote => "تجديد دوري لخدمة القطاعات/المرسلات لكل وحدة.";
    protected override string LogName => "sectors";
}

public sealed class ReceiverRecurringPricingHandler : RecurringServicePricingHandlerBase
{
    public ReceiverRecurringPricingHandler(ApplicationDbContext db, ILogger<ReceiverRecurringPricingHandler> logger) : base(db, logger) { }
    public override string HandlerKey => RecurringPricingHandlerKeys.Receivers;
    protected override string FeatureKey => FeatureKeys.Receivers;
    protected override PricingChargeUnit ChargeUnit => PricingChargeUnit.PerReceiver;
    protected override string ServiceDisplayName => "المستقبلات";
    protected override string InitialNote => "تسعير إنشاء مستقبل جديد.";
    protected override string RenewalNote => "تجديد دوري لخدمة المستقبلات لكل وحدة.";
    protected override string LogName => "receivers";
}

public sealed class ClientRecurringPricingHandler : RecurringServicePricingHandlerBase
{
    public ClientRecurringPricingHandler(ApplicationDbContext db, ILogger<ClientRecurringPricingHandler> logger) : base(db, logger) { }
    public override string HandlerKey => RecurringPricingHandlerKeys.Clients;
    protected override string FeatureKey => FeatureKeys.Clients;
    protected override PricingChargeUnit ChargeUnit => PricingChargeUnit.PerSubscriber;
    protected override string ServiceDisplayName => "المشتركين/العملاء";
    protected override string InitialNote => "تسعير إضافة مشترك/عميل جديد.";
    protected override string RenewalNote => "تجديد دوري لخدمة المشتركين/العملاء لكل وحدة.";
    protected override string LogName => "clients";
}

public sealed class UserRecurringPricingHandler : RecurringServicePricingHandlerBase
{
    public UserRecurringPricingHandler(ApplicationDbContext db, ILogger<UserRecurringPricingHandler> logger) : base(db, logger) { }
    public override string HandlerKey => RecurringPricingHandlerKeys.Users;
    protected override string FeatureKey => FeatureKeys.Users;
    protected override PricingChargeUnit ChargeUnit => PricingChargeUnit.PerUser;
    protected override string ServiceDisplayName => "الموظفين/المستخدمين";
    protected override string InitialNote => "تسعير إضافة موظف/مستخدم جديد.";
    protected override string RenewalNote => "تجديد دوري لخدمة الموظفين/المستخدمين لكل وحدة.";
    protected override string LogName => "users";
}

public sealed class SpeedProfileRecurringPricingHandler : RecurringServicePricingHandlerBase
{
    public SpeedProfileRecurringPricingHandler(ApplicationDbContext db, ILogger<SpeedProfileRecurringPricingHandler> logger) : base(db, logger) { }
    public override string HandlerKey => RecurringPricingHandlerKeys.SpeedProfiles;
    protected override string FeatureKey => FeatureKeys.Profiles;
    protected override PricingChargeUnit ChargeUnit => PricingChargeUnit.PerSpeedProfile;
    protected override string ServiceDisplayName => "السرعة/البروفايل";
    protected override string InitialNote => "تسعير إضافة بروفايل/سرعة جديدة.";
    protected override string RenewalNote => "تجديد دوري لخدمة السرعات/البروفايلات لكل وحدة.";
    protected override string LogName => "speed profiles";
}
