using RadTik.Models;
using RadTik.Helpers;

namespace RadTik.ViewModels.CompanyAdmin
{
    public sealed class CompanyServicesIndexViewModel
    {
        public int SelectedNetworkId { get; set; }
        public string SelectedNetworkName { get; set; } = "";

        public int EffectiveCompanyNetworkId { get; set; }
        public string EffectiveCompanyNetworkName { get; set; } = "";

        public decimal CompanyBalance { get; set; }

        public List<CompanyServiceItemViewModel> Services { get; set; } = [];
    }

    public sealed class CompanyServiceItemViewModel
    {
        public string FeatureKey { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>شرح تفصيلي من مدير النظام (HTML) لنافذة «عرض التفاصيل».</summary>
        public string? DetailHtml { get; set; }

        /// <summary>سياسة التسعير من مدير النظام (HTML).</summary>
        public string? PricingPolicyHtml { get; set; }

        public bool HasActiveSubscription { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? StartAt { get; set; }

        public bool HasPendingRequest { get; set; }
        public int? PendingRequestId { get; set; }

        public List<CompanyServicePricingOptionViewModel> PricingOptions { get; set; } = [];
    }

    public sealed class CompanyServicePricingOptionViewModel
    {
        public int PricingId { get; set; }
        public PricingBillingPeriod BillingPeriod { get; set; }
        public PricingChargeUnit ChargeUnit { get; set; } = PricingChargeUnit.Flat;
        public decimal AmountSYP { get; set; }
        public decimal AmountUSD { get; set; }
        public PricingCurrency Currency { get; set; }
        public bool IsActive { get; set; }

        /// <summary>المبلغ المطلوب تقديرياً (ل.س.ج) بعد المضاعف والتقريب لأعلى — مطابق لتحقق الخادم.</summary>
        public decimal EstimatedChargeSyp { get; set; }

        public string DisplayText =>
            $"{BillingPeriodDisplay} — {AmountSYP:N2} ل.س.ج لكل {ChargeUnitDisplay}";

        public string BillingPeriodDisplay => PricingDisplay.BillingPeriodSubjectLabel(BillingPeriod);

        public string ChargeUnitDisplay => PricingDisplay.ChargeUnitSubjectLabel(ChargeUnit);
    }
}

