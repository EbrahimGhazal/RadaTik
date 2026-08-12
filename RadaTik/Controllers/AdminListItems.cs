namespace RadaTik.Controllers;

using RadaTik.Models;

/// <summary>
/// عناصر عرض (View Models) لصفحات إدارة المستخدمين الخاصة بمدير الشركة.
/// ملاحظة: تم إبقاء الـ namespace كما كان سابقاً لتجنب كسر الـ Razor Views القديمة.
/// </summary>
public class AdminUserListItem
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Roles { get; set; } = string.Empty;
    public EmployeeDepartment EmployeeDepartment { get; set; } = EmployeeDepartment.None;
    public string EmployeeDepartmentName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class AdminClientListItem
{
    public int Id { get; set; }
    public string NetworkName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime LastUpdated { get; set; }
    public string ServerName { get; set; } = string.Empty;
}

