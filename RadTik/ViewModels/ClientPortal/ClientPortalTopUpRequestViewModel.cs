using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RadTik.Models;

namespace RadTik.ViewModels.ClientPortal;

public class ClientPortalPaymentMethodOption
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsCash { get; set; }
}

public class ClientPortalCollectionPointOption
{
    public int CollectionPointAccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ShamCashQrCodePath { get; set; }
}

public class ClientPortalTopUpRequestViewModel
{
    public int ClientId { get; set; }
    public decimal WalletBalance { get; set; }

    [Required(ErrorMessage = "يرجى تحديد الجهة المستهدفة")]
    public ClientWalletTopUpRecipientTarget RecipientTarget { get; set; } = ClientWalletTopUpRecipientTarget.CompanyManager;

    [Display(Name = "نقطة التحصيل")]
    public int? TargetCollectionPointAccountId { get; set; }

    [Required(ErrorMessage = "المبلغ مطلوب")]
    [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
    [Display(Name = "المبلغ المطلوب (ل.س)")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "يرجى اختيار طريقة الدفع")]
    [Display(Name = "طريقة الدفع")]
    public int PaymentMethodId { get; set; }

    [Display(Name = "رقم الإشعار / المرجع")]
    [StringLength(200)]
    public string? ReferenceNumber { get; set; }

    [Display(Name = "صورة الإيصال")]
    public IFormFile? ReceiptImage { get; set; }

    [Display(Name = "ملاحظات")]
    [StringLength(1000)]
    public string? Notes { get; set; }

    public int? ShamCashPaymentMethodId { get; set; }
    public string? CompanyManagerShamCashQrCodePath { get; set; }

    public List<ClientPortalPaymentMethodOption> PaymentMethodOptions { get; set; } = new();
    public List<ClientPortalCollectionPointOption> CollectionPointOptions { get; set; } = new();
}
