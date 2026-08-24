using Microsoft.AspNetCore.Http;
using RadaTik.Models;
using System.ComponentModel.DataAnnotations;

namespace RadaTik.ViewModels.Network
{
    public class NetworkViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الشبكة مطلوب")]
        [Display(Name = "اسم الشبكة")]
        [StringLength(100, ErrorMessage = "اسم الشبكة يجب أن لا يتجاوز 100 حرف")]
        public string Name { get; set; } = null!;

        [Display(Name = "المحافظات")]
        [StringLength(500, ErrorMessage = "المحافظات يجب أن لا تتجاوز 500 حرف")]
        [DataType(DataType.MultilineText)]
        public string? Governorates { get; set; }

        [Display(Name = "شعار الشبكة")]
        [DataType(DataType.Upload)]
        public IFormFile? LogoFile { get; set; }

        public string? LogoPath { get; set; }

        [Required]
        [Display(Name = "حالة الشبكة")]
        public NetworkStatus Status { get; set; } = NetworkStatus.Active;

        [Display(Name = "ملاحظات")]
        [DataType(DataType.MultilineText)]
        [StringLength(1000, ErrorMessage = "الملاحظات يجب أن لا تتجاوز 1000 حرف")]
        public string? Notes { get; set; }

        public bool IsMainCompanyNetwork { get; set; }

        [Display(Name = "سعر صرف الدولار الافتراضي (1$ = ل.س.ج)")]
        [Range(0.0001, 999999999, ErrorMessage = "سعر الصرف غير صالح")]
        public decimal? DefaultUsdToSypExchangeRate { get; set; }

        [Display(Name = "عملة فواتير المواد الافتراضية")]
        public PricingCurrency DefaultMaterialInvoiceCurrency { get; set; } = PricingCurrency.SYP_New;

        [Display(Name = "خصم باقات المميزين (%)")]
        [Range(0, 100, ErrorMessage = "نسبة الخصم يجب أن تكون بين 0 و 100")]
        public decimal VipDiscountPercent { get; set; }

        [Display(Name = "مهلة السماح بعد الانتهاء (أيام)")]
        [Range(0, 365, ErrorMessage = "مهلة السماح يجب أن تكون بين 0 و 365 يوماً")]
        public int VipGraceDays { get; set; }

        [Display(Name = "عدم فصل المميزين تلقائياً")]
        public bool VipSkipAutoDisable { get; set; }
    }
}

