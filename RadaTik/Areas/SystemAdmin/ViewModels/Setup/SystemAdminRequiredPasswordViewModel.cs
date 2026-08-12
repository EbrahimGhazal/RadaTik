using System.ComponentModel.DataAnnotations;

namespace RadaTik.Areas.SystemAdmin.ViewModels.Setup;

public sealed class SystemAdminRequiredPasswordViewModel
{
    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [DataType(DataType.Password)]
    [Display(Name = "تأكيد كلمة المرور الجديدة")]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين")]
    public string ConfirmNewPassword { get; set; } = "";
}
