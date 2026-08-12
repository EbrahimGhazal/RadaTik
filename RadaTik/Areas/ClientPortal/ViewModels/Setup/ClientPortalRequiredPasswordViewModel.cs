using System.ComponentModel.DataAnnotations;
using global::RadaTik.Security;

namespace RadaTik.Areas.ClientPortal.ViewModels.Setup;

public sealed class ClientPortalRequiredPasswordViewModel
{
    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة مرور البوابة الجديدة")]
    [MinLength(ClientPasswordRules.MinimumLength, ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [DataType(DataType.Password)]
    [Display(Name = "تأكيد كلمة المرور")]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
    public string ConfirmNewPassword { get; set; } = "";
}
