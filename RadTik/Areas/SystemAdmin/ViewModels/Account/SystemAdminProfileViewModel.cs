using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RadTik.Areas.SystemAdmin.ViewModels.Account;

public class SystemAdminProfileViewModel
{
    [Display(Name = "اسم المستخدم")]
    public string? UserName { get; set; }

    [Display(Name = "البريد الإلكتروني")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [StringLength(256)]
    public string? Email { get; set; }

    [Display(Name = "الاسم الكامل")]
    [StringLength(100, ErrorMessage = "الاسم الكامل طويل جداً")]
    public string? FullName { get; set; }

    [Display(Name = "رقم الهاتف")]
    [StringLength(20, ErrorMessage = "رقم الهاتف طويل جداً")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "العنوان")]
    [StringLength(500, ErrorMessage = "العنوان طويل جداً")]
    public string? Address { get; set; }

    [Display(Name = "QR شام كاش الحالي")]
    [StringLength(500)]
    public string? ShamCashQrCodePath { get; set; }

    [Display(Name = "صورة QR شام كاش")]
    public IFormFile? ShamCashQrCodeFile { get; set; }

    [Display(Name = "حذف QR الحالي")]
    public bool RemoveShamCashQrCode { get; set; }

    [Display(Name = "تاريخ إنشاء الحساب")]
    public DateTime CreatedDate { get; set; }
}

