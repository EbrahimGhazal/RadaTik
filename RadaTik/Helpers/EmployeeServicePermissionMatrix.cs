using RadaTik.Models;
using RadaTik.Security;
using RadaTik.ViewModels.Admin;

namespace RadaTik.Helpers;

/// <summary>
/// مصفوفة صلاحيات الموظف: تظهر فقط الخدمات التي فعّلها مدير الشركة (اشتراك نشط).
/// لكل خدمة: عرض / إضافة / تعديل حسب الجدول.
/// ملاحظات السياسة التشغيلية:
/// - عمليات الإضافة/التعديل للمرسلات والمستقبلات والعملاء من الموظف تتطلب موافقة مدير الشركة.
/// - المخدمات (MikroTik) مجمّدة حالياً للموظفين.
/// - طلبات الصيانة تبقى كما هي، وطلبات التركيب للعرض فقط.
/// </summary>
public static class EmployeeServicePermissionMatrix
{
    private sealed record SlotDef(string Label, string[] Keys, string? Hint = null);

    private sealed record ServiceDef(string FeatureKey, string Title, int SortOrder, SlotDef[] Slots);

    private static readonly ServiceDef[] Definitions =
    [
        new(FeatureKeys.Sectors, "المرسلات", 10,
        [
            new SlotDef("عرض", ["Sectors.View"]),
            new SlotDef("إضافة", ["Sectors.Create"], "ينشئ الموظف المرسل، ويُستكمل بعد موافقة مدير الشركة."),
            new SlotDef("تعديل", ["Sectors.Edit"], "تعديل بيانات المرسل يحتاج موافقة مدير الشركة."),
        ]),
        new(FeatureKeys.Receivers, "المستقبلات", 20,
        [
            new SlotDef("عرض", ["Receivers.View"]),
            new SlotDef("إضافة", ["Receivers.Create"], "إضافة المستقبل من الموظف تتطلب موافقة مدير الشركة."),
            new SlotDef("تعديل", ["Receivers.Edit"], "تعديل بيانات المستقبل يتطلب موافقة مدير الشركة."),
        ]),
        new(FeatureKeys.Clients, "العملاء", 30,
        [
            new SlotDef("عرض", ["Clients.View"]),
            new SlotDef("إضافة", ["Clients.Create"],
                "إنشاء مشترك جديد، ويُنفّذ بعد موافقة مدير الشركة عند الحاجة."),
            new SlotDef("تعديل", ["Clients.Edit"], "تعديل بيانات/بروفايل العميل يحتاج موافقة مدير الشركة."),
            new SlotDef("استيراد من السيرفر", ["Clients.ImportFromServer"],
                "عند التفعيل يظهر زر استيراد المشتركين من MikroTik لموظف الشركة."),
        ]),
        new(FeatureKeys.Requests, "الطلبات", 50,
        [
            new SlotDef("عرض", ["Requests.View"], "عرض طلبات الصيانة وطلبات التركيب."),
            new SlotDef("تعديل (صيانة)", ["MaintenanceRequests.Manage"], "قبول ورفض وإتمام طلبات الصيانة."),
        ]),
        new(FeatureKeys.Warehouse, "المستودع", 60,
        [
            new SlotDef("عرض", ["Warehouse.View"], "عرض الأصناف والحركات والكميات."),
            new SlotDef("إدارة", ["Warehouse.Manage"], "تسجيل وارد وصادر وتصحيح المخزون."),
        ]),
        new(FeatureKeys.Warehouse, "جرد المستودع", 65,
        [
            new SlotDef("إدارة", ["WarehouseStocktake.Manage"], "تنفيذ جرد المستودع واعتماد الفروقات."),
        ]),
        new(FeatureKeys.Warehouse, "فواتير شراء المواد", 70,
        [
            new SlotDef("عرض", ["MaterialPurchase.View"]),
            new SlotDef("إدارة", ["MaterialPurchase.Manage"], "إنشاء وتعديل فواتير شراء المواد."),
        ]),
        new(FeatureKeys.Warehouse, "فواتير بيع المواد", 75,
        [
            new SlotDef("عرض", ["MaterialSales.View"]),
            new SlotDef("إدارة", ["MaterialSales.Manage"], "إنشاء وتعديل فواتير بيع المواد."),
        ]),
        new(FeatureKeys.MoneyDiary, "دفتر الإيراد والمصروف", 80,
        [
            new SlotDef("عرض", ["MoneyDiary.View"]),
            new SlotDef("إدارة", ["MoneyDiary.Manage"], "تسجيل قيود الإيراد والمصروف."),
            new SlotDef("جرد مالي", ["FinancialReconciliation.View"], "عرض الجرد المالي وملخص الدفاتر."),
        ]),
        new(FeatureKeys.Payroll, "رواتب الموظفين", 90,
        [
            new SlotDef("عرض", ["Payroll.View"]),
            new SlotDef("إدارة", ["Payroll.Manage"], "إعداد واعتماد دفعات الرواتب."),
            new SlotDef("طلب تغذية محفظة", ["Payroll.WalletTopUp.Request"], "تقديم طلب تغذية المحفظة الشخصية."),
        ]),
        new(FeatureKeys.Erp, "نظام ERP", 100,
        [
            new SlotDef("عرض", ["Erp.View"], "عرض لوحة ERP والعملاء والمهام."),
            new SlotDef("إدارة", ["Erp.Manage"], "إدارة مهام الموظفين والمكافآت والمحاسبة."),
        ]),
    ];

    /// <summary>
    /// مفاتيح الخدمات التي يدعمها مصفوفة صلاحيات الموظف.
    /// </summary>
    public static HashSet<string> GetSupportedFeatureKeys()
    {
        return Definitions
            .Select(d => d.FeatureKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// بناء صفوف الواجهة للخدمات المفعّلة فقط، مع حل معرّفات الصلاحيات من قاعدة البيانات.
    /// </summary>
    public static List<EmployeeServicePermissionUiRow> BuildRows(
        HashSet<string> enabledFeatureKeys,
        List<Permission> allPermissions)
    {
        Dictionary<string, int> keyToId = allPermissions
            .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        List<EmployeeServicePermissionUiRow> rows = new List<EmployeeServicePermissionUiRow>();

        foreach (ServiceDef? def in Definitions.OrderBy(d => d.SortOrder))
        {
            if (!enabledFeatureKeys.Contains(def.FeatureKey))
            {
                continue;
            }

            List<EmployeeServicePermissionUiSlot> slots = new List<EmployeeServicePermissionUiSlot>();
            foreach (SlotDef slot in def.Slots)
            {
                List<int> ids = new List<int>();
                foreach (string k in slot.Keys)
                {
                    if (keyToId.TryGetValue(k, out int id))
                    {
                        ids.Add(id);
                    }
                }

                ids = ids.Distinct().ToList();
                if (ids.Count == 0)
                {
                    continue;
                }

                slots.Add(new EmployeeServicePermissionUiSlot
                {
                    Label = slot.Label,
                    PermissionIds = ids,
                    Hint = slot.Hint
                });
            }

            if (slots.Count == 0)
            {
                continue;
            }

            rows.Add(new EmployeeServicePermissionUiRow
            {
                FeatureKey = def.FeatureKey,
                Title = def.Title,
                Slots = slots
            });
        }

        return rows;
    }

    /// <summary>
    /// جميع مفاتيح الصلاحيات المرتبطة بخدمة معيّنة في مصفوفة الموظف.
    /// </summary>
    public static IReadOnlyList<string> GetPermissionKeysForFeature(string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return Array.Empty<string>();
        }

        return Definitions
            .Where(d => string.Equals(d.FeatureKey, featureKey, StringComparison.OrdinalIgnoreCase))
            .SelectMany(d => d.Slots)
            .SelectMany(s => s.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// جميع معرّفات الصلاحيات المسموح اختيارها للموظف ضمن شبكة معيّنة (خدمات مفعّلة + موجودة في الجدول).
    /// </summary>
    public static HashSet<int> GetAllowedPermissionIds(
        HashSet<string> enabledFeatureKeys,
        List<Permission> allPermissions)
    {
        List<EmployeeServicePermissionUiRow> rows = BuildRows(enabledFeatureKeys, allPermissions);
        HashSet<int> set = new HashSet<int>();
        foreach (EmployeeServicePermissionUiRow row in rows)
        {
            foreach (EmployeeServicePermissionUiSlot slot in row.Slots)
            {
                foreach (int id in slot.PermissionIds)
                {
                    set.Add(id);
                }
            }
        }

        return set;
    }
}
