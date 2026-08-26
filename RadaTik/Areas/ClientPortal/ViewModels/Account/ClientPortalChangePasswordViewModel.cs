using System.ComponentModel.DataAnnotations;
using RadaTik.Security;

namespace RadaTik.Areas.ClientPortal.ViewModels.Account;

public sealed class ClientPortalChangePasswordViewModel
{
    [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة مرور النظام الجديدة")]
    [MinLength(ClientPasswordRules.MinimumLength, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [DataType(DataType.Password)]
    [Display(Name = "تأكيد كلمة المرور الجديدة")]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
    public string ConfirmNewPassword { get; set; } = "";
}
