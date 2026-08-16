using System.ComponentModel.DataAnnotations;

namespace RadaTik.ViewModels.MikroTikServers;

public sealed class BulkMikroTikServersCreateViewModel
{
    [Required(ErrorMessage = "يرجى تحديد الشبكة")]
    [Display(Name = "الشبكة")]
    public int? NetworkId { get; set; }

    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    [Display(Name = "اسم المستخدم (مشترك)")]
    public string User { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور (مشتركة)")]
    public string Pass { get; set; } = string.Empty;

    [Required(ErrorMessage = "المنفذ مطلوب")]
    [Range(1, 65535, ErrorMessage = "يجب أن يكون المنفذ بين 1 و 65535")]
    [Display(Name = "المنفذ الافتراضي")]
    public int Port { get; set; } = 8728;

    [Display(Name = "ملاحظات مشتركة")]
    [StringLength(500)]
    public string? Notes { get; set; }

    [Display(Name = "معرف المستخدم (اختياري)")]
    [StringLength(50)]
    public string? UserID { get; set; }

    public List<BulkMikroTikServerRowViewModel> Servers { get; set; } = CreateDefaultRows(5);

    public static List<BulkMikroTikServerRowViewModel> CreateDefaultRows(int count)
    {
        List<BulkMikroTikServerRowViewModel> rows = [];
        for (int i = 0; i < count; i++)
        {
            rows.Add(new BulkMikroTikServerRowViewModel());
        }

        return rows;
    }
}

public sealed class BulkMikroTikServerRowViewModel
{
    [Display(Name = "اسم الخادم")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "يجب أن يكون اسم الخادم بين 2 و 100 حرف")]
    public string? Name { get; set; }

    [Display(Name = "المضيف (IP)")]
    public string? Host { get; set; }

    [Display(Name = "منفذ خاص")]
    [Range(1, 65535, ErrorMessage = "يجب أن يكون المنفذ بين 1 و 65535")]
    public int? Port { get; set; }

    [Display(Name = "ملاحظات")]
    [StringLength(500)]
    public string? Notes { get; set; }

    public bool HasAnyValue =>
        !string.IsNullOrWhiteSpace(Name) ||
        !string.IsNullOrWhiteSpace(Host) ||
        Port.HasValue ||
        !string.IsNullOrWhiteSpace(Notes);
}
