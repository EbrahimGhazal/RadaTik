using System.Text.Json;
using RadaTik.Models;
using RadaTik.ViewModels.Admin;

namespace RadaTik.Helpers;

/// <summary>
/// قوالب أقسام الموظف: صلاحيات افتراضية + مسمى وظيفي + تركيز لوحة التحكم.
/// </summary>
public static class EmployeeDepartmentTemplates
{
    private sealed record TemplateDef(
        EmployeeDepartment Department,
        string Title,
        string Description,
        string Icon,
        string DefaultPayrollJobTitle,
        EmployeeDashboardFocus DashboardFocus,
        string[] PermissionKeys);

    private sealed record DepartmentGroupDef(
        string Key,
        string Title,
        string Icon,
        EmployeeDepartment[] Departments);

    private static readonly DepartmentGroupDef[] GroupDefinitions =
    [
        new("operations", "تشغيل وشبكة", "fa-tower-broadcast",
            [EmployeeDepartment.FieldTechnician, EmployeeDepartment.CustomerService, EmployeeDepartment.NetworkOperations]),
        new("commercial", "مالي وتجاري", "fa-briefcase",
            [EmployeeDepartment.Finance, EmployeeDepartment.Sales, EmployeeDepartment.Purchases, EmployeeDepartment.Warehouse]),
        new("general", "عام وتخصيص", "fa-sliders-h",
            [EmployeeDepartment.GeneralStaff, EmployeeDepartment.Custom])
    ];

    private static readonly TemplateDef[] Definitions =
    [
        new(
            EmployeeDepartment.FieldTechnician,
            "فني ميداني",
            "زيارات الصيانة والتركيب — إدارة طلبات الصيانة وعرض مواعيد التركيب.",
            "fa-hard-hat",
            "فني ميداني",
            EmployeeDashboardFocus.FieldTasks,
            ["Requests.View", "MaintenanceRequests.Manage"]),
        new(
            EmployeeDepartment.CustomerService,
            "خدمة عملاء",
            "متابعة العملاء والطلبات — عرض وتعديل بيانات العملاء ومعالجة الطلبات.",
            "fa-headset",
            "موظف خدمة عملاء",
            EmployeeDashboardFocus.CustomerCare,
            ["Clients.View", "Clients.Edit", "Requests.View"]),
        new(
            EmployeeDepartment.NetworkOperations,
            "عمليات شبكة",
            "إدارة البنية التحتية — المرسلات والمستقبلات (عرض/إضافة/تعديل).",
            "fa-network-wired",
            "مهندس شبكة",
            EmployeeDashboardFocus.NetworkOps,
            [
                "Sectors.View", "Sectors.Create", "Sectors.Edit",
                "Receivers.View", "Receivers.Create", "Receivers.Edit"
            ]),
        new(
            EmployeeDepartment.Finance,
            "مالية",
            "دفتر الإيراد والمصروف، الجرد المالي، وعرض رواتب الموظفين.",
            "fa-coins",
            "محاسب",
            EmployeeDashboardFocus.Finance,
            ["MoneyDiary.View", "MoneyDiary.Manage", "FinancialReconciliation.View", "Payroll.View"]),
        new(
            EmployeeDepartment.Sales,
            "مبيعات",
            "متابعة المشتركين وفواتير البيع — عرض العملاء وإنشاء فواتير المواد.",
            "fa-chart-line",
            "موظف مبيعات",
            EmployeeDashboardFocus.Sales,
            ["Clients.View", "Clients.Create", "Clients.Edit", "MaterialSales.View", "MaterialSales.Manage", "Requests.View"]),
        new(
            EmployeeDepartment.Purchases,
            "مشتريات",
            "فواتير شراء المواد ومتابعة المخزون قبل الاستلام.",
            "fa-cart-shopping",
            "موظف مشتريات",
            EmployeeDashboardFocus.Purchases,
            ["MaterialPurchase.View", "MaterialPurchase.Manage", "Warehouse.View"]),
        new(
            EmployeeDepartment.Warehouse,
            "مستودع",
            "إدارة الأصناف والحركات — جرد المستودع وتحديث الكميات.",
            "fa-warehouse",
            "أمين مستودع",
            EmployeeDashboardFocus.Warehouse,
            ["Warehouse.View", "Warehouse.Manage", "WarehouseStocktake.Manage"]),
        new(
            EmployeeDepartment.GeneralStaff,
            "موظف عام",
            "صلاحيات عرض أساسية على جميع الخدمات المفعّلة — مناسب للموظف متعدد المهام.",
            "fa-user-check",
            "موظف",
            EmployeeDashboardFocus.Balanced,
            ["Sectors.View", "Receivers.View", "Clients.View", "Requests.View"]),
        new(
            EmployeeDepartment.Custom,
            "تخصيص يدوي",
            "اختر الصلاحيات بنفسك من المصفوفة أدناه دون تطبيق قالب جاهز.",
            "fa-sliders-h",
            "",
            EmployeeDashboardFocus.Balanced,
            [])
    ];

    public static IReadOnlyList<EmployeeDepartmentTemplateUiItem> GetUiItems()
    {
        return Definitions
            .Where(d => d.Department != EmployeeDepartment.None)
            .Select(d => new EmployeeDepartmentTemplateUiItem
            {
                Department = d.Department,
                Title = d.Title,
                Description = d.Description,
                Icon = d.Icon,
                DefaultPayrollJobTitle = d.DefaultPayrollJobTitle,
                DashboardFocus = d.DashboardFocus
            })
            .ToList();
    }

    public static IReadOnlyList<EmployeeDepartmentGroupUiItem> GetUiGroups()
    {
        Dictionary<EmployeeDepartment, EmployeeDepartmentTemplateUiItem> byDept = GetUiItems()
            .ToDictionary(i => i.Department);

        return GroupDefinitions
            .Select(g => new EmployeeDepartmentGroupUiItem
            {
                Key = g.Key,
                Title = g.Title,
                Icon = g.Icon,
                Departments = g.Departments
                    .Where(byDept.ContainsKey)
                    .Select(d => byDept[d])
                    .ToList()
            })
            .Where(g => g.Departments.Count > 0)
            .ToList();
    }

    public static string GetDisplayName(EmployeeDepartment department)
    {
        if (department == EmployeeDepartment.None)
        {
            return "غير محدد";
        }

        TemplateDef? def = Definitions.FirstOrDefault(d => d.Department == department);
        return def?.Title ?? "غير محدد";
    }

    public static EmployeeDashboardFocus GetDashboardFocus(EmployeeDepartment department)
    {
        TemplateDef? def = Definitions.FirstOrDefault(d => d.Department == department);
        return def?.DashboardFocus ?? EmployeeDashboardFocus.Balanced;
    }

    public static string? GetDefaultPayrollJobTitle(EmployeeDepartment department)
    {
        TemplateDef? def = Definitions.FirstOrDefault(d => d.Department == department);
        return string.IsNullOrWhiteSpace(def?.DefaultPayrollJobTitle) ? null : def.DefaultPayrollJobTitle;
    }

    public static List<int> ResolvePermissionIds(
        EmployeeDepartment department,
        HashSet<string> enabledFeatureKeys,
        List<Permission> allPermissions)
    {
        if (department is EmployeeDepartment.None or EmployeeDepartment.Custom)
        {
            return [];
        }

        TemplateDef? def = Definitions.FirstOrDefault(d => d.Department == department);
        if (def == null || def.PermissionKeys.Length == 0)
        {
            return [];
        }

        HashSet<int> allowed = EmployeeServicePermissionMatrix.GetAllowedPermissionIds(enabledFeatureKeys, allPermissions);
        Dictionary<string, int> keyToId = allPermissions
            .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        List<int> ids = new List<int>();
        foreach (string key in def.PermissionKeys)
        {
            if (keyToId.TryGetValue(key, out int id) && allowed.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids.Distinct().ToList();
    }

    public static EmployeeDepartment DetectDepartment(
        IReadOnlyCollection<int> selectedPermissionIds,
        HashSet<string> enabledFeatureKeys,
        List<Permission> allPermissions)
    {
        if (selectedPermissionIds.Count == 0)
        {
            return EmployeeDepartment.None;
        }

        List<int> sortedSelected = selectedPermissionIds.OrderBy(x => x).ToList();
        foreach (TemplateDef def in Definitions.Where(d => d.Department is not (EmployeeDepartment.None or EmployeeDepartment.Custom)))
        {
            List<int> templateIds = ResolvePermissionIds(def.Department, enabledFeatureKeys, allPermissions)
                .OrderBy(x => x)
                .ToList();
            if (templateIds.Count > 0 && templateIds.SequenceEqual(sortedSelected))
            {
                return def.Department;
            }
        }

        return EmployeeDepartment.Custom;
    }

    public static EmployeeDashboardFocus ResolveDashboardFocus(
        EmployeeDepartment department,
        IEnumerable<string> permissionKeys)
    {
        if (department is not (EmployeeDepartment.None or EmployeeDepartment.Custom))
        {
            return GetDashboardFocus(department);
        }

        HashSet<string> keys = permissionKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (keys.Contains("MaintenanceRequests.Manage"))
        {
            return EmployeeDashboardFocus.FieldTasks;
        }

        if (keys.Contains("MoneyDiary.View") || keys.Contains("MoneyDiary.Manage") || keys.Contains("Payroll.View"))
        {
            return EmployeeDashboardFocus.Finance;
        }

        if (keys.Contains("MaterialSales.View") || keys.Contains("MaterialSales.Manage"))
        {
            return EmployeeDashboardFocus.Sales;
        }

        if (keys.Contains("MaterialPurchase.View") || keys.Contains("MaterialPurchase.Manage"))
        {
            return EmployeeDashboardFocus.Purchases;
        }

        if (keys.Contains("Warehouse.View") || keys.Contains("Warehouse.Manage") || keys.Contains("WarehouseStocktake.Manage"))
        {
            return EmployeeDashboardFocus.Warehouse;
        }

        if (keys.Contains("Clients.View") || keys.Contains("Clients.Edit") || keys.Contains("Clients.Create"))
        {
            return EmployeeDashboardFocus.CustomerCare;
        }

        if (keys.Contains("Sectors.View") || keys.Contains("Receivers.View"))
        {
            return EmployeeDashboardFocus.NetworkOps;
        }

        return EmployeeDashboardFocus.Balanced;
    }

    public static string BuildTemplatesJson(
        HashSet<string> enabledFeatureKeys,
        List<Permission> allPermissions)
    {
        List<object> payload = new List<object>();
        foreach (TemplateDef def in Definitions.Where(d => d.Department != EmployeeDepartment.None))
        {
            payload.Add(new
            {
                department = (int)def.Department,
                title = def.Title,
                description = def.Description,
                icon = def.Icon,
                payrollJobTitle = def.DefaultPayrollJobTitle,
                dashboardFocus = (int)def.DashboardFocus,
                permissionIds = ResolvePermissionIds(def.Department, enabledFeatureKeys, allPermissions)
            });
        }

        return JsonSerializer.Serialize(payload);
    }
}
