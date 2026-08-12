using System.ComponentModel.DataAnnotations;

namespace RadaTik.Models;

/// <summary>
/// قسم/دور الموظف داخل الشركة — يحدد قالب الصلاحيات الافتراضي وتركيز لوحة التحكم.
/// </summary>
public enum EmployeeDepartment
{
    [Display(Name = "— اختر القسم —")]
    None = 0,

    [Display(Name = "فني ميداني")]
    FieldTechnician = 1,

    [Display(Name = "خدمة عملاء")]
    CustomerService = 2,

    [Display(Name = "عمليات شبكة")]
    NetworkOperations = 3,

    [Display(Name = "موظف عام")]
    GeneralStaff = 4,

    [Display(Name = "مالية")]
    Finance = 5,

    [Display(Name = "مبيعات")]
    Sales = 6,

    [Display(Name = "مشتريات")]
    Purchases = 7,

    [Display(Name = "مستودع")]
    Warehouse = 8,

    [Display(Name = "تخصيص يدوي")]
    Custom = 99
}

/// <summary>
/// تركيز لوحة تحكم الموظف حسب دوره.
/// </summary>
public enum EmployeeDashboardFocus
{
    Balanced = 0,
    FieldTasks = 1,
    CustomerCare = 2,
    NetworkOps = 3,
    Finance = 4,
    Sales = 5,
    Purchases = 6,
    Warehouse = 7
}
