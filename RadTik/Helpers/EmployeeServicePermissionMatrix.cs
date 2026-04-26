using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.Admin;

namespace RadTik.Helpers;

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
            new SlotDef("إضافة", ["Clients.Create", "Clients.ImportFromServer"],
                "يشمل إنشاء العميل/الاستيراد، ويُنفّذ بعد موافقة مدير الشركة."),
            new SlotDef("تعديل", ["Clients.Edit"], "تعديل بيانات/بروفايل العميل يحتاج موافقة مدير الشركة."),
        ]),
        new(FeatureKeys.Requests, "الطلبات", 50,
        [
            new SlotDef("عرض", ["Requests.View"], "عرض طلبات الصيانة وطلبات التركيب."),
            new SlotDef("تعديل (صيانة)", ["MaintenanceRequests.Manage"], "قبول ورفض وإتمام طلبات الصيانة."),
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
        var keyToId = allPermissions
            .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var rows = new List<EmployeeServicePermissionUiRow>();

        foreach (var def in Definitions.OrderBy(d => d.SortOrder))
        {
            if (!enabledFeatureKeys.Contains(def.FeatureKey))
            {
                continue;
            }

            var slots = new List<EmployeeServicePermissionUiSlot>();
            foreach (var slot in def.Slots)
            {
                var ids = new List<int>();
                foreach (var k in slot.Keys)
                {
                    if (keyToId.TryGetValue(k, out var id))
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
    /// جميع معرّفات الصلاحيات المسموح اختيارها للموظف ضمن شبكة معيّنة (خدمات مفعّلة + موجودة في الجدول).
    /// </summary>
    public static HashSet<int> GetAllowedPermissionIds(
        HashSet<string> enabledFeatureKeys,
        List<Permission> allPermissions)
    {
        var rows = BuildRows(enabledFeatureKeys, allPermissions);
        var set = new HashSet<int>();
        foreach (var row in rows)
        {
            foreach (var slot in row.Slots)
            {
                foreach (var id in slot.PermissionIds)
                {
                    set.Add(id);
                }
            }
        }

        return set;
    }
}
