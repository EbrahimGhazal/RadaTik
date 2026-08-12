namespace RadaTik.ViewModels.Admin;

/// <summary>
/// صف واحد في واجهة صلاحيات الموظف: خدمة مفعّلة + خانات (عرض/إضافة/تعديل/حذف).
/// </summary>
public class EmployeeServicePermissionUiRow
{
    public string FeatureKey { get; set; } = "";

    /// <summary>عنوان الخدمة بالعربية.</summary>
    public string Title { get; set; } = "";

    public List<EmployeeServicePermissionUiSlot> Slots { get; set; } = [];
}

/// <summary>خانة واحدة (مثلاً «عرض») وقد ترتبط بعدة مفاتيح صلاحية (مثل إنشاء + استيراد للعملاء).</summary>
public class EmployeeServicePermissionUiSlot
{
    public string Label { get; set; } = "";

    /// <summary>معرّفات الصلاحيات في جدول Permissions لهذه الخانة (فارغ = غير متاح بعد).</summary>
    public List<int> PermissionIds { get; set; } = [];

    public string? Hint { get; set; }
}
