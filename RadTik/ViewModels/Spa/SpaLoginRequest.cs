namespace RadTik.ViewModels.Spa;

/// <summary>طلب تسجيل دخول واجهة React (/app) — يطابق حقول قاعدة البيانات (اسم المستخدم / كلمة المرور).</summary>
public class SpaLoginRequest
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}
