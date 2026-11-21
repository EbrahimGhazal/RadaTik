using RadaTik.Models;
using RadaTik.Security;
using RadaTik.ViewModels.Admin;

namespace RadaTik.Helpers;

/// <summary>
/// مصفوفة صلاحيات الموظف: كل خدمة وصفحة يمكن لمدير الشركة منحها أو منعها.
/// لكل صف: عرض/إضافة/تعديل/حذف عند وجود العملية، وقدرات خاصة للصفحات التابعة.
/// </summary>
public static class EmployeeServicePermissionMatrix
{
    /// <summary>صفحات تشغيلية ليست اشتراكاً مستقلاً، وتظهر دائماً في المصفوفة.</summary>
    public const string CompanyPages = "CompanyPages";

    private sealed record CrudKeyDef(string[]? Keys, string? Hint = null);

    private sealed record SpecialDef(string Label, string[] Keys, string? Hint = null);

    private sealed record ServiceDef(
        string FeatureKey,
        string Group,
        string Title,
        string Icon,
        int SortOrder,
        CrudKeyDef? View,
        CrudKeyDef? Create,
        CrudKeyDef? Edit,
        CrudKeyDef? Delete,
        SpecialDef[] Specials);

    private static readonly ServiceDef[] Definitions =
    [
        new(CompanyPages, "البداية", "لوحة الشركة", "gauge-high", 10,
            new(["CompanyDashboard.View"], "لوحة تحكم مدير الشركة وملخص التشغيل."),
            null, null, null, []),
        new(CompanyPages, "البداية", "مركز العمليات", "bolt", 12,
            new(["Operations.View"], "متابعة التشغيل اليومي من مركز العمليات."),
            null, null, null, []),

        new(FeatureKeys.Clients, "المشتركون", "المشتركون", "users", 20,
            new(["Clients.View"], "قائمة المشتركين وتفاصيلهم وعقد الانضمام للمشترك."),
            new(["Clients.Create"], "إضافة مشترك، ومعالج الإضافة، وتجهيز المشترك الخاص."),
            new(["Clients.Edit"], "تعديل بيانات المشترك وباقته. يشمل أيضاً عمليات شريط الأدوات إن لم تُفصَل."),
            new(["Clients.Delete"], "حذف مشترك — عملية حساسة (يشمل الحذف الجماعي)."),
            [
                new("استيراد من السيرفر", ["Clients.ImportFromServer"], "استيراد المشتركين من MikroTik."),
                new("استيراد معلومات ملف", ["Clients.ImportInfo"], "استيراد بيانات المشتركين من ملف Excel."),
                new("شريط: تجميد/تفعيل", ["Clients.Freeze"], "أزرار التجميد والتفعيل في شريط الإجراءات الجماعية."),
                new("شريط: تجديد اشتراك", ["Clients.Renew"], "زر التجديد في شريط الإجراءات الجماعية."),
                new("شريط: تمديد 3/5 أيام", ["Clients.CourtesyExtend"], "أزرار التمديد المجاملاتي في شريط الإجراءات الجماعية."),
                new("شريط: تعديل السرعة", ["Clients.ChangeSpeed"], "أزرار البروفايل والسرعة المؤقتة/الدورية في الشريط."),
                new("شريط: تاريخ الانتهاء", ["Clients.SetExpiration"], "زر تعديل تاريخ الانتهاء في شريط الإجراءات الجماعية."),
                new("شريط: نقل/نسخ إلى برج", ["Clients.CopyToTower"], "أزرار النقل والنسخ إلى برج في الشريط."),
                new("شريط: شحن رصيد", ["Clients.WalletTopUp"], "زر شحن الرصيد في شريط الإجراءات الجماعية."),
                new("شريط: تصدير Excel/PDF", ["Clients.Export"], "أزرار تصدير Excel وPDF في شريط الإجراءات الجماعية.")
            ]),
        new(FeatureKeys.Clients, "المشتركون", "عقد الانضمام", "file-signature", 22,
            new(["SubscriberContract.View"], "عرض إعداد عقد الانضمام."),
            null, null, null,
            [new("تعديل العقد", ["SubscriberContract.Manage"], "حفظ نص عقد الانضمام وبياناته.")]),
        new(FeatureKeys.Clients, "المشتركون", "فواتير التجهيز", "receipt", 24,
            new(["InstallationInvoices.View"]),
            null, null, null,
            [new("تحصيل واعتماد", ["InstallationInvoices.Manage"], "اعتماد الفاتورة وتسجيل الدفع.")]),
        new(FeatureKeys.Clients, "المشتركون", "تسعير التجهيز", "tags", 26,
            new(["InstallationPricing.View"]),
            null, null, null,
            [new("تعديل الأسعار", ["InstallationPricing.Manage"])]),
        new(FeatureKeys.Clients, "المشتركون", "صفحات التواصل", "thumbs-up", 28,
            new(["ClientPresence.View"]),
            null, null, null,
            [new("إدارة الصفحات", ["ClientPresence.Manage"])]),

        new(FeatureKeys.Requests, "الطلبات والدعم", "الطلبات", "headset", 40,
            new(["Requests.View"], "لوحة الطلبات وطلبات الصيانة."),
            null, null, null,
            [
                new("إدارة صيانة", ["MaintenanceRequests.Manage"], "قبول ورفض وإتمام طلبات الصيانة."),
                new("موافقة تغيير السرعة", ["SpeedChange.Approve"], "الموافقة أو الرفض. يتفعّل معها عرض الطلبات."),
                new("تنفيذ تغيير السرعة", ["SpeedChange.Implement"], "تنفيذ التغيير على MikroTik.")
            ]),
        new(FeatureKeys.Requests, "الطلبات والدعم", "فواتير الصيانة", "file-invoice", 42,
            new(["MaintenanceInvoices.View"]),
            null, null, null,
            [new("تسديد فاتورة", ["MaintenanceInvoices.Pay"])]),
        new(FeatureKeys.Networks, "الطلبات والدعم", "تسعير الصيانة", "tags", 44,
            new(["MaintenancePricing.View"]),
            null, null, null,
            [new("تعديل التسعير", ["MaintenancePricing.Manage"])]),
        new(FeatureKeys.PasswordResets, "الطلبات والدعم", "استعادة كلمة المرور", "key", 46,
            new(["PasswordResets.View"]),
            null, null, null,
            [new("تنفيذ أو رفض", ["PasswordResets.Manage"])]),
        new(FeatureKeys.Requests, "الطلبات والدعم", "موافقات الموظفين", "user-check", 48,
            new(["EmployeeApprovals.View"], "عرض طلبات الموظفين بانتظار الموافقة."),
            null, null, null,
            [new("اعتماد أو رفض", ["EmployeeApprovals.Manage"])]),

        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "صندوق محادثات AI", "comments", 60,
            new(["CustomerService.Inbox"], "المحادثات والرد والإحالة."),
            null, null, null, []),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "منشورات السوشال", "share-alt", 62,
            new(["CustomerService.Social"]),
            null, null, null,
            [new("إنشاء وتعديل", ["CustomerService.Social.Manage"])]),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "موظفو AI", "user-tie", 64,
            new(["CustomerService.Agents"]),
            null, null, null,
            [new("إعداد الموظفين", ["CustomerService.Agents.Manage"])]),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "تدريب فريق AI", "graduation-cap", 66,
            new(["CustomerService.Training"]),
            null, null, null,
            [new("تعديل المعرفة", ["CustomerService.Training.Manage"])]),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "مكتب المدير", "briefcase", 68,
            new(["CustomerService.Desk"]),
            null, null, null, []),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "تقرير فريق AI", "chart-line", 70,
            new(["CustomerService.Report"]),
            null, null, null, []),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "التقارير التقنية", "chart-area", 71,
            new(["CustomerService.TechnicalReports"], "تقارير الترافك والأداء التقنية."),
            null, null, null,
            [new("إنشاء تقرير", ["CustomerService.TechnicalReports.Manage"])]),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "تقارير الدراسات", "graduation-cap", 72,
            new(["CustomerService.StudyReports"], "تقارير الدراسات التشغيلية."),
            null, null, null,
            [new("إنشاء دراسة", ["CustomerService.StudyReports.Manage"])]),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "سكربتات MikroTik", "terminal", 73,
            new(["CustomerService.MikroTikScripts"], "عرض مسودات سكربتات MikroTik."),
            null, null, null,
            [new("إنشاء وتطبيق", ["CustomerService.MikroTikScripts.Manage"])]),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "تنظيم عمل AI", "clock", 74,
            new(["CustomerService.Workforce"], "لوحة تنظيم عمل موظفي AI."),
            null, null, null,
            [new("إدارة التنظيم", ["CustomerService.Workforce.Manage"])]),
        new(FeatureKeys.Clients, "الذكاء الاصطناعي", "إعدادات فريق AI", "sliders-h", 76,
            new(["CustomerService.Settings"]),
            null, null, null,
            [new("حفظ الإعدادات", ["CustomerService.Settings.Manage"])]),

        new(FeatureKeys.Networks, "الشبكة", "إعدادات الشبكة", "sitemap", 80,
            new(["Network.View"]),
            null, null, null,
            [new("تعديل الشبكة", ["Network.Manage"])]),
        new(FeatureKeys.Profiles, "الشبكة", "باقات السرعة", "boxes", 82,
            new(["Profiles.View"]),
            null, null, null,
            [new("إدارة الباقات", ["Profiles.Manage"], "إضافة وتعديل وحذف باقات السرعة.")]),
        new(FeatureKeys.MikroTikServers, "الشبكة", "خوادم MikroTik", "server", 84,
            new(["MikroTikServers.View"]),
            new(["MikroTikServers.Create"]),
            new(["MikroTikServers.Edit"], "يشمل النسخ الاحتياطي وعتبات التنبيه."),
            new(["MikroTikServers.Delete"], "حذف سيرفر عملية حساسة."),
            []),
        new(FeatureKeys.MikroTikServers, "الشبكة", "مراقبة الترافك", "chart-area", 86,
            new(["MikroTikTraffic.View"]),
            null, null, null, []),
        new(FeatureKeys.Sectors, "الشبكة", "المرسلات", "tower-broadcast", 88,
            new(["Sectors.View"]),
            new(["Sectors.Create"], "ينشئ الموظف المرسل، ويُستكمل بعد موافقة مدير الشركة."),
            new(["Sectors.Edit"], "تعديل بيانات المرسل يحتاج موافقة مدير الشركة."),
            new(["Sectors.Delete"], "حذف حساس."),
            [
                new("التحكم الراديوي", ["Sectors.RadioEngineering"]),
                new("مراقبة القطاعات", ["Sectors.RadioMonitoring"])
            ]),
        new(FeatureKeys.Receivers, "الشبكة", "المستقبلات", "satellite-dish", 90,
            new(["Receivers.View"], "قائمة المستقبلات وتفاصيلها."),
            new(["Receivers.Create"], "إضافة المستقبل من الموظف تتطلب موافقة مدير الشركة."),
            new(["Receivers.Edit"], "تعديل بيانات المستقبل يتطلب موافقة مدير الشركة."),
            new(["Receivers.Delete"], "حذف حساس."),
            []),
        new(FeatureKeys.Receivers, "الشبكة", "معايرة الهوائي", "crosshairs", 92,
            new(["AntennaCalibration.View"], "غرف المعايرة الحية ومسح QR وتثبيت الإشارة."),
            null, null, null, []),

        new(CompanyPages, "المالية", "محفظة الشركة", "wallet", 100,
            new(["Wallet.View"], "نظرة عامة وسجل الحركات."),
            null, null, null,
            [new("طلب تعبئة رصيد", ["Wallet.TopUp"], "تقديم طلب تعبئة محفظة الشركة.")]),
        new(CompanyPages, "المالية", "تغذية رصيد للمشتركين", "user-check", 102,
            new(["ClientWalletTopUp.View"]),
            null, null, null,
            [new("اعتماد أو رفض", ["ClientWalletTopUp.Manage"])]),
        new(CompanyPages, "المالية", "الصندوق النقدي", "cash-register", 104,
            new(["CashBox.View"]),
            null, null, null,
            [new("إيداع وسحب", ["CashBox.Manage"])]),
        new(CompanyPages, "المالية", "سجل القبض", "receipt", 106,
            new(["Receipts.View"]),
            null, null, null, []),
        new(CompanyPages, "المالية", "نظرة أعمال الشركة", "chart-pie", 108,
            new(["CompanyBusiness.View"]),
            null, null, null, []),
        new(FeatureKeys.Warehouse, "المالية", "المستودع", "warehouse", 110,
            new(["Warehouse.View"], "عرض الأصناف والحركات والكميات."),
            null, null, null,
            [new("إدارة مخزون", ["Warehouse.Manage"], "تسجيل وارد وصادر وتصحيح المخزون.")]),
        new(FeatureKeys.Warehouse, "المالية", "جرد المستودع", "clipboard-check", 112,
            null, null, null, null,
            [new("تنفيذ الجرد", ["WarehouseStocktake.Manage"], "تنفيذ جرد المستودع واعتماد الفروقات.")]),
        new(FeatureKeys.Warehouse, "المالية", "فواتير شراء المواد", "cart-shopping", 114,
            new(["MaterialPurchase.View"]),
            null, null, null,
            [new("إدارة الفواتير", ["MaterialPurchase.Manage"])]),
        new(FeatureKeys.Warehouse, "المالية", "فواتير بيع المواد", "cash-register", 116,
            new(["MaterialSales.View"]),
            null, null, null,
            [new("إدارة الفواتير", ["MaterialSales.Manage"])]),
        new(FeatureKeys.MoneyDiary, "المالية", "دفتر الإيراد والمصروف", "book", 118,
            new(["MoneyDiary.View"]),
            null, null, null,
            [new("تسجيل القيود", ["MoneyDiary.Manage"])]),
        new(FeatureKeys.MoneyDiary, "المالية", "الجرد المالي", "balance-scale", 120,
            new(["FinancialReconciliation.View"]),
            null, null, null, []),
        new(FeatureKeys.Payroll, "المالية", "رواتب الموظفين", "money-check-dollar", 122,
            new(["Payroll.View"]),
            null, null, null,
            [
                new("إدارة الرواتب", ["Payroll.Manage"], "إعداد الدفعات وتمييزها مدفوعة."),
                new("طلب تغذية محفظة", ["Payroll.WalletTopUp.Request"]),
                new("إدارة تغذية المحفظة", ["Payroll.WalletTopUp.Manage"])
            ]),
        new(FeatureKeys.Networks, "المالية", "تجديد نقاط التحصيل", "sync-alt", 124,
            new(["CollectionPointRenewals.View"]),
            null, null, null,
            [new("معالجة التجديد", ["CollectionPointRenewals.Manage"])]),
        new(FeatureKeys.Networks, "المالية", "تذكير التجديد", "bell", 126,
            new(["RenewalReminders.View"]),
            null, null, null,
            [new("تعديل التذكير", ["RenewalReminders.Manage"])]),

        new(FeatureKeys.Erp, "نظام ERP", "لوحة ERP", "gauge-high", 140,
            new(["Erp.View"], "الدخول إلى لوحة ERP."),
            null, null, null,
            [new("إدارة عامة", ["Erp.Manage"], "صلاحية إدارة شاملة لوحدات ERP.")]),
        new(FeatureKeys.Erp, "نظام ERP", "الأضابير الإلكترونية", "folder-open", 142,
            new(["Erp.Dossier.View"]),
            null, null, null, []),
        new(FeatureKeys.Erp, "نظام ERP", "عملاء ERP", "handshake", 144,
            new(["Erp.Customers.View"]),
            null, null, null,
            [new("إدارة العملاء", ["Erp.Customers.Manage"])]),
        new(FeatureKeys.Erp, "نظام ERP", "الموردون", "truck", 146,
            new(["Erp.Suppliers.View"]),
            null, null, null,
            [new("إدارة الموردين", ["Erp.Suppliers.Manage"])]),
        new(FeatureKeys.Erp, "نظام ERP", "مهام الموظفين", "tasks", 148,
            new(["Erp.Tasks.View"]),
            null, null, null,
            [new("إدارة المهام", ["Erp.Tasks.Manage"])]),
        new(FeatureKeys.Erp, "نظام ERP", "مكافآت وعقوبات", "award", 150,
            new(["Erp.Rewards.View"]),
            null, null, null,
            [new("إدارة المكافآت", ["Erp.Rewards.Manage"])]),
        new(FeatureKeys.Erp, "نظام ERP", "المحاسبة", "calculator", 152,
            new(["Erp.Accounting.View"]),
            null, null, null,
            [new("قيود وفترات", ["Erp.Accounting.Manage"])]),
        new(FeatureKeys.Erp, "نظام ERP", "تقارير ERP", "chart-bar", 154,
            new(["Erp.Reports.View"]),
            null, null, null, []),

        new(FeatureKeys.Reports, "التقارير والاشتراك", "تقارير الشبكة", "chart-bar", 170,
            new(["Reports.View"]),
            null, null, null,
            [
                new("تصدير وطباعة", ["Reports.Export"], "تصدير Excel والطباعة."),
                new("قوالب التقارير", ["Reports.Manage"], "تعديل قوالب التقارير ومظهر المستندات.")
            ]),
        new(CompanyPages, "التقارير والاشتراك", "الخدمات والاشتراك", "puzzle-piece", 172,
            new(["CompanySubscription.View"], "عرض باقات المنصة والخدمات."),
            null, null, null,
            [new("طلب اشتراك", ["CompanySubscription.Manage"], "إرسال طلب اشتراك يخصم من المحفظة.")]),

        new(FeatureKeys.Users, "الفريق والحسابات", "الموظفون", "user-tie", 180,
            new(["Employees.View"], "عرض قائمة الموظفين."),
            null, null, null,
            [new("إضافة وتعديل ومنح صلاحيات", ["Employees.Manage"], "يشمل إنشاء الموظف وتغيير صلاحياته وحذفه.")]),
        new(FeatureKeys.Users, "الفريق والحسابات", "نقاط التحصيل", "store", 182,
            new(["CollectionPointAccounts.View"], "عرض تسديدات مشتركي الشركة عبر نقاط التحصيل مع التصفية بين تاريخين والطباعة."),
            null, null, null,
            [new("إدارة النقاط", ["CollectionPointAccounts.Manage"], "تعديل أرصدة النقاط وتصفيرها.")]),
        new(FeatureKeys.Users, "الفريق والحسابات", "حسابات البوابة", "user-friends", 184,
            new(["PortalAccounts.View"]),
            null, null, null, []),
        new(CompanyPages, "الفريق والحسابات", "إعدادات الشركة", "sliders", 186,
            new(["CompanySettings.View"], "عرض إعدادات الملكية والشركاء."),
            null, null, null,
            [
                new("تعديل الإعدادات", ["CompanySettings.Manage"]),
                new("موافقات الإعدادات", ["CompanySettings.Approvals"])
            ]),
        new(CompanyPages, "خدمات إضافية", "الخدمات المخصصة", "layer-group", 200,
            new(["CustomServices.View"]),
            null, null, null,
            [new("إدارة السجلات", ["CustomServices.Manage"])]),
    ];

    private static readonly (EmployeePermissionCrudAction Action, string Label)[] CrudColumnMeta =
    [
        (EmployeePermissionCrudAction.View, "عرض"),
        (EmployeePermissionCrudAction.Create, "إضافة"),
        (EmployeePermissionCrudAction.Edit, "تعديل"),
        (EmployeePermissionCrudAction.Delete, "حذف")
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

        foreach (ServiceDef def in Definitions.OrderBy(d => d.SortOrder))
        {
            if (!enabledFeatureKeys.Contains(def.FeatureKey))
            {
                continue;
            }

            List<EmployeeServicePermissionUiCrudCell> crudCells = BuildCrudCells(def, keyToId);
            List<EmployeeServicePermissionUiSlot> specials = new List<EmployeeServicePermissionUiSlot>();

            foreach (SpecialDef special in def.Specials)
            {
                List<int> ids = ResolveIds(special.Keys, keyToId);
                if (ids.Count == 0)
                {
                    continue;
                }

                specials.Add(new EmployeeServicePermissionUiSlot
                {
                    Label = special.Label,
                    PermissionIds = ids,
                    Hint = special.Hint
                });
            }

            bool hasAnyCrud = crudCells.Any(c => c.IsAvailable);
            if (!hasAnyCrud && specials.Count == 0)
            {
                continue;
            }

            rows.Add(new EmployeeServicePermissionUiRow
            {
                FeatureKey = def.FeatureKey,
                Group = def.Group,
                Title = def.Title,
                Icon = def.Icon,
                CrudCells = crudCells,
                SpecialSlots = specials
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
            .SelectMany(EnumeratePermissionKeys)
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
            foreach (EmployeeServicePermissionUiCrudCell cell in row.CrudCells)
            {
                foreach (int id in cell.PermissionIds)
                {
                    set.Add(id);
                }
            }

            foreach (EmployeeServicePermissionUiSlot slot in row.SpecialSlots)
            {
                foreach (int id in slot.PermissionIds)
                {
                    set.Add(id);
                }
            }
        }

        return set;
    }

    private static List<EmployeeServicePermissionUiCrudCell> BuildCrudCells(
        ServiceDef def,
        Dictionary<string, int> keyToId)
    {
        List<EmployeeServicePermissionUiCrudCell> cells = new List<EmployeeServicePermissionUiCrudCell>(4);

        foreach ((EmployeePermissionCrudAction action, string label) in CrudColumnMeta)
        {
            CrudKeyDef? keyDef = action switch
            {
                EmployeePermissionCrudAction.View => def.View,
                EmployeePermissionCrudAction.Create => def.Create,
                EmployeePermissionCrudAction.Edit => def.Edit,
                EmployeePermissionCrudAction.Delete => def.Delete,
                _ => null
            };

            List<int> ids = keyDef?.Keys == null
                ? []
                : ResolveIds(keyDef.Keys, keyToId);

            cells.Add(new EmployeeServicePermissionUiCrudCell
            {
                Action = action,
                Label = label,
                PermissionIds = ids,
                Hint = keyDef?.Hint
            });
        }

        return cells;
    }

    private static IEnumerable<string> EnumeratePermissionKeys(ServiceDef def)
    {
        foreach (CrudKeyDef? crud in new[] { def.View, def.Create, def.Edit, def.Delete })
        {
            if (crud?.Keys == null)
            {
                continue;
            }

            foreach (string key in crud.Keys)
            {
                yield return key;
            }
        }

        foreach (SpecialDef special in def.Specials)
        {
            foreach (string key in special.Keys)
            {
                yield return key;
            }
        }
    }

    private static List<int> ResolveIds(string[] keys, Dictionary<string, int> keyToId)
    {
        List<int> ids = new List<int>();
        foreach (string key in keys)
        {
            if (keyToId.TryGetValue(key, out int id))
            {
                ids.Add(id);
            }
        }

        return ids.Distinct().ToList();
    }
}
