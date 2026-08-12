using System.Text;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services.PricingPolicies;

public enum PricingScenarioActorRole
{
    CompanyManager = 1,
    CompanyEmployee = 2,
    SystemAdministrator = 3
}

public enum PricingChargeKind
{
    None = 0,
    FixedAmount = 1,
    Percentage = 2
}

public enum FeaturePublicContentField
{
    Detail = 1,
    PricingPolicy = 2
}

public static class FeaturePublicContentTemplateMarkers
{
    public const string Auto = "[[AUTO]]";
    public const string AutoDetailPlaceholder = "{{AUTO_DETAIL}}";
    public const string AutoPricingPlaceholder = "{{AUTO_PRICING}}";
}

public sealed class ServicePricingPolicyDefinition
{
    public string FeatureKey { get; init; } = string.Empty;
    public string ServicePitch { get; init; } = string.Empty;
    public IReadOnlyList<string> UsageInstructions { get; init; } = [];
    public string PayerRoleLabel { get; init; } = "محفظة مدير الشركة";
    public bool EmployeeActionRequiresApproval { get; init; }
    public string ApproverRoleLabel { get; init; } = "مدير النظام";
    public string RenewalHint { get; init; } = "تتم عملية التجديد وفق مدة الاستحقاق والسعر المعلن من مدير النظام.";
}

public sealed class PricingSimulationDescriptor
{
    public string FeatureKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public PricingChargeKind ChargeKind { get; init; } = PricingChargeKind.None;
    public decimal FixedAmountSyp { get; init; }
    public decimal PercentValue { get; init; }
    public bool EmployeeActionRequiresApproval { get; init; }
    public string ApproverRoleLabel { get; init; } = string.Empty;
    public string PayerRoleLabel { get; init; } = string.Empty;
    public string ChargeUnitLabel { get; init; } = string.Empty;
    public string BillingPeriodLabel { get; init; } = string.Empty;
    public decimal InitialChargeSyp { get; init; }
    public string InitialChargeLabel { get; init; } = string.Empty;
    public decimal RenewalChargeSyp { get; init; }
    public string RenewalChargeLabel { get; init; } = string.Empty;
    public int RenewalTimesPerYear { get; init; }
    public string RenewalBillingPeriodLabel { get; init; } = string.Empty;
    public string RenewalSummary { get; init; } = string.Empty;
}

public sealed class FeaturePublicContentDraft
{
    public string DetailHtml { get; init; } = string.Empty;
    public string PricingPolicyHtml { get; init; } = string.Empty;
}

public sealed class PricingScenarioComputation
{
    public PricingChargeKind ChargeKind { get; init; }
    public decimal EffectiveAmountSyp { get; init; }
    public decimal PercentValue { get; init; }
    public string AmountDisplay { get; init; } = "—";
}

public interface IServicePricingPolicyCatalog
{
    ServicePricingPolicyDefinition GetPolicy(string featureKey, string displayName);
}

public sealed class ServicePricingPolicyCatalog : IServicePricingPolicyCatalog
{
    public ServicePricingPolicyDefinition GetPolicy(string featureKey, string displayName)
    {
        if (string.Equals(featureKey, FeatureKeys.Sectors, StringComparison.OrdinalIgnoreCase))
        {
            return new ServicePricingPolicyDefinition
            {
                FeatureKey = featureKey,
                ServicePitch = "خدمة المرسلات تنظّم تغطية الشبكة وتساعد على توسيع الخدمة بسرعة مع ضبط مالي واضح لكل مرسل.",
                UsageInstructions =
                [
                    "أضف المرسل من شاشة إدارة المرسلات بعد اختيار المخدم المناسب.",
                    "إذا كانت الإضافة من موظف مفوض، يبقى الطلب بانتظار موافقة مدير النظام قبل التفعيل.",
                    "بعد الموافقة يصبح المرسل جاهزاً للاستخدام ويُسجل الخصم تلقائياً."
                ],
                PayerRoleLabel = "محفظة مدير الشركة",
                EmployeeActionRequiresApproval = true,
                ApproverRoleLabel = "مدير النظام",
                RenewalHint = "يتم تجديد كل مرسل وفق المدة والقيمة التي يحددها مدير النظام، ويمكن تعديلهما لاحقاً وتطبيق التعديل تلقائياً."
            };
        }

        var fallbackInstructions = new List<string>
        {
            "يمكن تفعيل الخدمة حسب الصلاحيات الممنوحة داخل النظام.",
            "تُطبّق الفوترة تلقائياً بالاعتماد على إعدادات التسعير الحالية في لوحة مدير النظام."
        };

        return new ServicePricingPolicyDefinition
        {
            FeatureKey = featureKey,
            ServicePitch = $"تساعد خدمة {displayName} في تحسين العمليات اليومية مع فوترة مرنة مرتبطة بسياسات التسعير الفعلية.",
            UsageInstructions = fallbackInstructions,
            PayerRoleLabel = "محفظة مدير الشركة",
            EmployeeActionRequiresApproval = false,
            ApproverRoleLabel = "مدير النظام",
            RenewalHint = "التجديد يعتمد على دورية الاستحقاق المعتمدة لكل خيار تسعير."
        };
    }
}

public interface IPricingScenarioStrategy
{
    bool CanHandle(FeaturePricing pricing);
    PricingScenarioComputation Compute(FeaturePricing pricing, decimal baseAmountSyp);
}

public sealed class FixedAmountPricingScenarioStrategy : IPricingScenarioStrategy
{
    public bool CanHandle(FeaturePricing pricing) =>
        pricing.ChargeUnit != PricingChargeUnit.PercentOfCollectedAmount;

    public PricingScenarioComputation Compute(FeaturePricing pricing, decimal baseAmountSyp)
    {
        var effective = WalletMath.CeilSyp(pricing.AmountSYP);
        return new PricingScenarioComputation
        {
            ChargeKind = PricingChargeKind.FixedAmount,
            EffectiveAmountSyp = effective,
            PercentValue = 0m,
            AmountDisplay = $"{effective:N2} ل.س.ج"
        };
    }
}

public sealed class PercentagePricingScenarioStrategy : IPricingScenarioStrategy
{
    public bool CanHandle(FeaturePricing pricing) =>
        pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount;

    public PricingScenarioComputation Compute(FeaturePricing pricing, decimal baseAmountSyp)
    {
        var percent = Math.Clamp(pricing.AmountSYP, 0m, 100m);
        var estimated = baseAmountSyp > 0m
            ? WalletMath.CeilSyp((baseAmountSyp * percent) / 100m)
            : 0m;

        var amountDisplay = estimated > 0m
            ? $"{percent:N2}% (تقديرياً {estimated:N2} ل.س.ج)"
            : $"{percent:N2}%";

        return new PricingScenarioComputation
        {
            ChargeKind = PricingChargeKind.Percentage,
            EffectiveAmountSyp = estimated,
            PercentValue = percent,
            AmountDisplay = amountDisplay
        };
    }
}

public interface IFeaturePublicContentComposer
{
    FeaturePublicContentDraft Compose(string featureKey, string displayName, IReadOnlyList<FeaturePricing> pricings);
    PricingSimulationDescriptor BuildSimulationDescriptor(string featureKey, string displayName, IReadOnlyList<FeaturePricing> pricings);
    string ResolveTemplate(string? storedValue, string generatedValue, FeaturePublicContentField field);
}

public sealed class FeaturePublicContentComposer : IFeaturePublicContentComposer
{
    private const decimal OldSypFactor = 100m;
    private readonly IServicePricingPolicyCatalog _policyCatalog;
    private readonly IReadOnlyList<IPricingScenarioStrategy> _strategies;

    public FeaturePublicContentComposer(
        IServicePricingPolicyCatalog policyCatalog,
        IEnumerable<IPricingScenarioStrategy> strategies)
    {
        _policyCatalog = policyCatalog;
        _strategies = strategies.ToList();
    }

    public FeaturePublicContentDraft Compose(string featureKey, string displayName, IReadOnlyList<FeaturePricing> pricings)
    {
        var policy = _policyCatalog.GetPolicy(featureKey, displayName);
        var activePricings = pricings
            .Where(p => p.IsActive)
            .OrderBy(p => p.BillingPeriod)
            .ThenBy(p => p.Id)
            .ToList();

        var detailBuilder = new StringBuilder();
        detailBuilder.Append("<p>");
        detailBuilder.Append(policy.ServicePitch);
        detailBuilder.Append("</p>");
        detailBuilder.Append("<ul>");
        foreach (var line in policy.UsageInstructions)
        {
            detailBuilder.Append("<li>");
            detailBuilder.Append(line);
            detailBuilder.Append("</li>");
        }
        detailBuilder.Append("</ul>");

        var pricingBuilder = new StringBuilder();
        pricingBuilder.Append("<p><strong>سياسة التسعير الحالية (ديناميكية):</strong></p>");

        if (activePricings.Count == 0)
        {
            pricingBuilder.Append("<p>لا يوجد تسعير نشط حالياً لهذه الخدمة. سيظهر السعر تلقائياً بعد تعريفه من مدير النظام.</p>");
        }
        else
        {
            pricingBuilder.Append("<ul>");
            foreach (var pricing in activePricings)
            {
                var strategy = ResolveStrategy(pricing);
                var computed = strategy.Compute(pricing, 0m);
                var periodLabel = PricingDisplay.BillingPeriodLabel(pricing.BillingPeriod);
                var unitLabel = PricingDisplay.ChargeUnitLabel(pricing.ChargeUnit);
                pricingBuilder.Append("<li>");
                pricingBuilder.Append($"الاستحقاق: <strong>{periodLabel}</strong> — ");
                pricingBuilder.Append($"طريقة الاحتساب: <strong>{unitLabel}</strong> — ");
                pricingBuilder.Append($"القيمة: <strong>{computed.AmountDisplay}</strong>.");
                if (computed.ChargeKind == PricingChargeKind.FixedAmount && computed.EffectiveAmountSyp > 0m)
                {
                    pricingBuilder.Append($" (الليرة القديمة تقريباً: {(computed.EffectiveAmountSyp * OldSypFactor):N0})");
                }
                pricingBuilder.Append("</li>");
            }
            pricingBuilder.Append("</ul>");
        }

        pricingBuilder.Append("<p><strong>سيناريو الفوترة حسب الدور:</strong></p><ul>");
        pricingBuilder.Append($"<li>الخصم يتم من: <strong>{policy.PayerRoleLabel}</strong>.</li>");
        pricingBuilder.Append("<li>إضافة مدير الشركة: يتم الخصم مباشرة بعد نجاح الإضافة.</li>");
        if (policy.EmployeeActionRequiresApproval)
        {
            pricingBuilder.Append($"<li>إضافة الموظف المفوض: تُنشأ العملية أولاً ثم تبقى معلّقة حتى موافقة <strong>{policy.ApproverRoleLabel}</strong>، وبعد الموافقة يتم الخصم.</li>");
        }
        else
        {
            pricingBuilder.Append("<li>إضافة الموظف المفوض: يتم تطبيق نفس سياسة الخصم الفوري المعتمدة للخدمة.</li>");
        }
        pricingBuilder.Append("</ul>");

        var initialPricing = SelectInitialPricing(activePricings);
        var renewalPricing = SelectRenewalPricing(activePricings);
        pricingBuilder.Append("<p><strong>القيم المالية المعتمدة حالياً:</strong></p><ul>");
        pricingBuilder.Append($"<li>قيمة الإضافة الأولى: {BuildPricingAmountLabel(initialPricing)}.</li>");
        pricingBuilder.Append($"<li>قيمة التجديد: {BuildPricingAmountLabel(renewalPricing)}{(renewalPricing == null ? string.Empty : $" ({PricingDisplay.BillingPeriodLabel(renewalPricing.BillingPeriod)})")}.</li>");
        pricingBuilder.Append("</ul>");

        pricingBuilder.Append("<p><strong>سياسة التجديد:</strong></p>");
        pricingBuilder.Append($"<p>{BuildRenewalSummary(activePricings, policy)}</p>");
        pricingBuilder.Append("<p class=\"small text-muted\">الخدمات التي لا تحتوي على تسعير تجديد دوري لا يتم تجديدها تلقائياً.</p>");
        pricingBuilder.Append("<p class=\"text-muted small\">ملاحظة: جميع الأسعار الحالية تُعرض بالليرة السورية الجديدة، والتحويل التقريبي لليرة القديمة = القيمة × 100.</p>");

        return new FeaturePublicContentDraft
        {
            DetailHtml = detailBuilder.ToString(),
            PricingPolicyHtml = pricingBuilder.ToString()
        };
    }

    public PricingSimulationDescriptor BuildSimulationDescriptor(string featureKey, string displayName, IReadOnlyList<FeaturePricing> pricings)
    {
        var policy = _policyCatalog.GetPolicy(featureKey, displayName);
        var pricing = pricings
            .Where(p => p.IsActive)
            .OrderBy(p => p.BillingPeriod)
            .ThenBy(p => p.Id)
            .FirstOrDefault();

        if (pricing == null)
        {
            return new PricingSimulationDescriptor
            {
                FeatureKey = featureKey,
                DisplayName = displayName,
                ChargeKind = PricingChargeKind.None,
                EmployeeActionRequiresApproval = policy.EmployeeActionRequiresApproval,
                ApproverRoleLabel = policy.ApproverRoleLabel,
                PayerRoleLabel = policy.PayerRoleLabel,
                RenewalSummary = policy.RenewalHint
            };
        }

        var strategy = ResolveStrategy(pricing);
        var computed = strategy.Compute(pricing, 100000m);
        var activePricings = pricings.Where(p => p.IsActive).ToList();
        var initialPricing = SelectInitialPricing(activePricings);
        var renewalPricing = SelectRenewalPricing(activePricings);

        return new PricingSimulationDescriptor
        {
            FeatureKey = featureKey,
            DisplayName = displayName,
            ChargeKind = computed.ChargeKind,
            FixedAmountSyp = computed.ChargeKind == PricingChargeKind.FixedAmount ? computed.EffectiveAmountSyp : 0m,
            PercentValue = computed.ChargeKind == PricingChargeKind.Percentage ? computed.PercentValue : 0m,
            EmployeeActionRequiresApproval = policy.EmployeeActionRequiresApproval,
            ApproverRoleLabel = policy.ApproverRoleLabel,
            PayerRoleLabel = policy.PayerRoleLabel,
            ChargeUnitLabel = PricingDisplay.ChargeUnitLabel(pricing.ChargeUnit),
            BillingPeriodLabel = PricingDisplay.BillingPeriodLabel(pricing.BillingPeriod),
            InitialChargeSyp = initialPricing != null ? WalletMath.CeilSyp(initialPricing.AmountSYP) : 0m,
            InitialChargeLabel = BuildPricingAmountLabel(initialPricing),
            RenewalChargeSyp = renewalPricing != null ? WalletMath.CeilSyp(renewalPricing.AmountSYP) : 0m,
            RenewalChargeLabel = BuildPricingAmountLabel(renewalPricing),
            RenewalTimesPerYear = renewalPricing != null ? GetRenewalsPerYear(renewalPricing.BillingPeriod) : 0,
            RenewalBillingPeriodLabel = renewalPricing != null ? PricingDisplay.BillingPeriodLabel(renewalPricing.BillingPeriod) : string.Empty,
            RenewalSummary = BuildRenewalSummary(activePricings, policy)
        };
    }

    public string ResolveTemplate(string? storedValue, string generatedValue, FeaturePublicContentField field)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return generatedValue;
        }

        var trimmed = storedValue.Trim();
        if (string.Equals(trimmed, FeaturePublicContentTemplateMarkers.Auto, StringComparison.OrdinalIgnoreCase))
        {
            return generatedValue;
        }

        var placeholder = field == FeaturePublicContentField.Detail
            ? FeaturePublicContentTemplateMarkers.AutoDetailPlaceholder
            : FeaturePublicContentTemplateMarkers.AutoPricingPlaceholder;

        if (trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Replace(placeholder, generatedValue, StringComparison.OrdinalIgnoreCase);
        }

        return trimmed;
    }

    private IPricingScenarioStrategy ResolveStrategy(FeaturePricing pricing)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(pricing));
        if (strategy != null)
        {
            return strategy;
        }

        return new FixedAmountPricingScenarioStrategy();
    }

    private static string BuildRenewalSummary(IReadOnlyList<FeaturePricing> activePricings, ServicePricingPolicyDefinition policy)
    {
        var renewalPricings = activePricings
            .Where(p => p.BillingPeriod != PricingBillingPeriod.OneTime)
            .OrderBy(p => p.BillingPeriod)
            .ThenBy(p => p.Id)
            .ToList();

        if (renewalPricings.Count == 0)
        {
            return policy.RenewalHint;
        }

        var rows = renewalPricings.Select(p =>
        {
            var renewalsPerYear = GetRenewalsPerYear(p.BillingPeriod);
            var frequency = renewalsPerYear > 0 ? $" ({renewalsPerYear} مرة سنوياً)" : string.Empty;
            var multiplierHint = IsPerUnitCharge(p.ChargeUnit)
                ? $"، ويُحتسب المبلغ = قيمة التجديد × عدد الوحدات الفعلية ({PricingDisplay.ChargeUnitLabel(p.ChargeUnit)}) لكل شركة"
                : string.Empty;

            if (p.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
            {
                return $"{PricingDisplay.BillingPeriodLabel(p.BillingPeriod)}{frequency} بنسبة {p.AmountSYP:N2}%";
            }

            return $"{PricingDisplay.BillingPeriodLabel(p.BillingPeriod)}{frequency} بقيمة {WalletMath.CeilSyp(p.AmountSYP):N2} ل.س.ج{multiplierHint}";
        });

        return $"خيارات التجديد المتاحة حالياً: {string.Join("، ", rows)}.";
    }

    private static FeaturePricing? SelectInitialPricing(IReadOnlyList<FeaturePricing> activePricings)
    {
        return activePricings
            .OrderBy(p => p.BillingPeriod == PricingBillingPeriod.OneTime ? 0 : 1)
            .ThenBy(p => p.Id)
            .FirstOrDefault();
    }

    private static FeaturePricing? SelectRenewalPricing(IReadOnlyList<FeaturePricing> activePricings)
    {
        return activePricings
            .Where(p => p.BillingPeriod != PricingBillingPeriod.OneTime)
            .OrderBy(p => p.BillingPeriod)
            .ThenBy(p => p.Id)
            .FirstOrDefault();
    }

    private static string BuildPricingAmountLabel(FeaturePricing? pricing)
    {
        if (pricing == null)
        {
            return "غير محددة";
        }

        if (pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
        {
            return $"{pricing.AmountSYP:N2}%";
        }

        return $"{WalletMath.CeilSyp(pricing.AmountSYP):N2} ل.س.ج";
    }

    private static int GetRenewalsPerYear(PricingBillingPeriod billingPeriod) =>
        billingPeriod switch
        {
            PricingBillingPeriod.Daily => 365,
            PricingBillingPeriod.Monthly => 12,
            PricingBillingPeriod.Every3Months => 4,
            PricingBillingPeriod.Every6Months => 2,
            PricingBillingPeriod.Every12Months => 1,
            _ => 0
        };

    private static bool IsPerUnitCharge(PricingChargeUnit chargeUnit) =>
        chargeUnit is PricingChargeUnit.PerUser
            or PricingChargeUnit.PerNetwork
            or PricingChargeUnit.PerSubscriber
            or PricingChargeUnit.PerSector
            or PricingChargeUnit.PerReceiver
            or PricingChargeUnit.PerServer
            or PricingChargeUnit.PerCollectionPoint
            or PricingChargeUnit.PerSpeedProfile;
}
