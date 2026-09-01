using System.ComponentModel.DataAnnotations;
using global::RadaTik.Models;

namespace RadaTik.Areas.CompanyAdmin.ViewModels;

public sealed class NewSubscriberWizardStartViewModel
{
    public NewSubscriberWizardPath? SelectedPath { get; set; }
    public int? ExistingReceiverId { get; set; }
    public List<ReceiverPickOption> Receivers { get; set; } = new();
}

public sealed class ReceiverPickOption
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public string SectorName { get; init; } = string.Empty;
    public int MikroTikServerId { get; init; }
    public int SectorId { get; init; }
    public bool IsShared { get; init; }
    public bool IsActive { get; init; }
}

public sealed class WizardSectorLookup
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int MikroTikServerId { get; init; }
}

public sealed class NewSubscriberWizardSharedReceiverViewModel
{
    public int? MikroTikServerId { get; set; }
    public int? SectorId { get; set; }
    public int? ReceiverId { get; set; }
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Servers { get; set; } = new();
    public List<WizardSectorLookup> Sectors { get; set; } = new();
    public List<ReceiverPickOption> Receivers { get; set; } = new();
}

public sealed class InvoiceWarehouseModelOptionViewModel
{
    public int WarehouseItemId { get; init; }
    public string DisplayLabel { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}

public sealed class NewSubscriberWizardInvoiceLineViewModel
{
    public int ItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? MaterialKey { get; init; }
    public bool IsStockItem { get; init; }
    public int? WarehouseItemId { get; set; }
    public List<InvoiceWarehouseModelOptionViewModel> AvailableModels { get; init; } = [];
    public decimal UnitPrice { get; init; }
    public decimal Quantity { get; set; }
    public decimal LineTotal { get; init; }
}

public sealed class NewSubscriberWizardInvoiceViewModel
{
    public int InvoiceId { get; init; }
    public int ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public NewSubscriberWizardPath Path { get; init; }
    public bool RequiresManagerApproval { get; init; }
    public decimal TotalAmount { get; init; }
    public bool WarehousePricingReady { get; init; }
    public int UnlinkedStockLineCount { get; init; }
    public List<NewSubscriberWizardInvoiceLineViewModel> Lines { get; set; } = new();
}

public sealed class NewSubscriberWizardCollectPaymentViewModel
{
    public int InvoiceId { get; init; }
    public int ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public decimal ClientWalletBalance { get; init; }
}

public sealed class NewSubscriberWizardCompleteViewModel
{
    public int ClientId { get; init; }
    public int InvoiceId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public bool RequiresManagerApproval { get; init; }
    public bool InvoiceFinalized { get; init; }
    public bool PaymentRecorded { get; init; }
}

public sealed class NewSubscriberWizardSubscriberFormModel
{
    public NewSubscriberWizardPath Path { get; set; }

    [Required(ErrorMessage = "الاسم الثلاثي مطلوب")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "الرقم الوطني مطلوب")]
    public string? SID { get; set; }

    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    public string? UserName { get; set; }

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "البروفايل مطلوب")]
    [Range(1, int.MaxValue, ErrorMessage = "البروفايل مطلوب")]
    public int? ProfileId { get; set; }

    [Required(ErrorMessage = "رقم الهاتف مطلوب")]
    public string? PhoneNumber { get; set; }

    public string? ResidenceAddress { get; set; }

    [Display(Name = "العمل الوظيفي")]
    [StringLength(100, ErrorMessage = "العمل الوظيفي يجب أن لا يتجاوز 100 حرف")]
    public string? Occupation { get; set; }

    [Display(Name = "مكان العمل")]
    [StringLength(200, ErrorMessage = "مكان العمل يجب أن لا يتجاوز 200 حرف")]
    public string? Workplace { get; set; }

    public int? ReceiverId { get; set; }
    public int? MikroTikServerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? AccountExpirationDate { get; set; }
    public string? DbUserName { get; set; }
    public string? DbPassword { get; set; }

    [Display(Name = "مشترك مميز (VIP)")]
    public bool IsVip { get; set; }

    [Display(Name = "ملاحظة التمييز")]
    [StringLength(200, ErrorMessage = "ملاحظة التمييز يجب أن لا تتجاوز 200 حرف")]
    public string? VipNote { get; set; }

    [Display(Name = "ميزة المميز")]
    public ClientVipBenefitKind VipBenefitKind { get; set; } = ClientVipBenefitKind.Discount;

    [Display(Name = "نسبة الحسم (%)")]
    [Range(0, 100, ErrorMessage = "نسبة الحسم يجب أن تكون بين 0 و 100")]
    public decimal VipDiscountPercent { get; set; }
}
