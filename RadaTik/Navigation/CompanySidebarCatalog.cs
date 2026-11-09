using RadaTik.Security;

namespace RadaTik.Navigation;

/// <summary>كتالوج ثابت لروابط مدير الشبكة والموظف — إعادة تجميع لا حذف وظائف.</summary>
public static class CompanySidebarCatalog
{
    public static IReadOnlyList<NavSectionDef> GetSections() =>
    [
        // ترتيب تشغيلي: بداية → مشتركون → طلبات → AI → شبكة → مالية → ERP → تقارير → فريق → حساب
        HomeAdmin(),
        HomeEmployee(),
        WorkHeadingEmployee(),
        SubscribersAdmin(),
        SubscribersEmployee(),
        SupportAdmin(),
        SupportEmployee(),
        AiAdmin(),
        AiEmployee(),
        NetworkAdmin(),
        NetworkEmployee(),
        FinanceAdmin(),
        FinanceEmployee(),
        ErpAdmin(),
        ErpEmployee(),
        ReportsAdmin(),
        ReportsEmployee(),
        TeamAdmin(),
        TeamEmployee(),
        CustomAdmin(),
        AccountAdmin(),
        AccountEmployee()
    ];

    private static NavSectionDef HomeAdmin() => new()
    {
        Key = "home",
        Title = "البداية",
        IconClass = "fas fa-gauge-high",
        CssClass = "nav-section-home",
        Flat = true,
        Audience = NavAudience.Admin,
        Entries =
        [
            Entry(Link("admin-company-settings-finish", "أكمل تهيئة إعدادات الشركة", "fas fa-exclamation-triangle", NavAudience.Admin,
                route: "networkManager-company-settings-setup",
                activeControllers: ["CompanySettings"],
                activeActions: ["Setup"],
                featured: true,
                extraCss: "nav-link-setup-alert",
                hint: "مطلوب لإنهاء الإعداد",
                visibleWhen: ctx => ctx.CompanySettingsSetupIncomplete)),
            Entry(Link("admin-dashboard", "لوحة التحكم", "fas fa-gauge-high", NavAudience.Admin,
                route: "networkManager-dashboard",
                activeControllers: ["Dashboard"],
                excludeActions: ["Operations"])),
            Entry(Link("admin-ops", "مركز العمليات", "fas fa-bolt", NavAudience.Admin,
                route: "networkManager-operations-hub",
                activeControllers: ["Dashboard"],
                activeActions: ["Operations"],
                featured: true)),
            Entry(Link("admin-notif", "التنبيهات", "fas fa-bell", NavAudience.Admin,
                route: "networkManager-notifications",
                activeControllers: ["Notifications"],
                badgeDomId: "notificationsUnreadBadge"))
        ]
    };

    private static NavSectionDef HomeEmployee() => new()
    {
        Key = "home",
        Title = "البداية",
        IconClass = "fas fa-gauge-high",
        CssClass = "nav-section-quick",
        StaticTitle = true,
        Audience = NavAudience.Employee,
        Entries =
        [
            Entry(Link("emp-dashboard", "لوحة التحكم", "fas fa-home", NavAudience.Employee,
                route: "employee-dashboard",
                activeControllers: ["Dashboard"],
                excludeActions: ["Operations"])),
            Entry(Link("emp-company-dashboard", "لوحة الشركة", "fas fa-gauge-high", NavAudience.Employee,
                route: "networkManager-dashboard",
                permission: "CompanyDashboard.View",
                activeControllers: ["Dashboard"],
                excludeActions: ["Operations"])),
            Entry(Link("emp-ops", "مركز العمليات", "fas fa-bolt", NavAudience.Employee,
                route: "networkManager-operations-hub",
                permission: "Operations.View",
                activeControllers: ["Dashboard"],
                activeActions: ["Operations"])),
            Entry(Link("emp-requests-board", "لوحة الطلبات", "fas fa-clipboard-list", NavAudience.Employee,
                route: "employee-requestsManagement",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Requests,
                permission: "Requests.View",
                activeControllers: ["RequestsManagement"],
                activeActions: ["Index"],
                badgeDomId: "pendingRequestsBadge")),
            Entry(Link("emp-my-tasks", "مهامي (ERP)", "fas fa-tasks", NavAudience.Employee,
                route: "employee-my-tasks",
                feature: FeatureKeys.Erp,
                activeControllers: ["MyEmployeeTasks"])),
            Entry(Link("emp-notif", "التنبيهات", "fas fa-bell", NavAudience.Employee,
                route: "employee-notifications",
                activeControllers: ["Notifications"],
                badgeDomId: "employeeNotificationsUnreadBadge"))
        ]
    };

    private static NavSectionDef WorkHeadingEmployee() => new()
    {
        Key = "work-heading",
        Title = "مهامي وصلاحياتي",
        IconClass = "fas fa-briefcase",
        CssClass = "nav-section-heading",
        StaticTitle = true,
        IsHeadingOnly = true,
        Audience = NavAudience.Employee,
        Entries = []
    };

    private static NavSectionDef SubscribersAdmin() => new()
    {
        Key = "subscribers",
        Title = "المشتركون",
        IconClass = "fas fa-users",
        CssClass = "nav-section-subscribers",
        Audience = NavAudience.Admin,
        ExpandControllers =
        [
            "Clients", "NewSubscriberWizard", "SubscriberInstallationPricing",
            "SubscriberInstallationInvoices", "PrivateSubscriberSetup", "ClientPresence"
        ],
        Entries =
        [
            Entry(Link("admin-add-client", "إضافة مشترك", "fas fa-user-plus", NavAudience.Admin,
                route: "networkManager-new-subscriber-wizard",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Clients,
                activeControllers: ["NewSubscriberWizard"],
                featured: true)),
            Entry(Link("admin-clients", "قائمة المشتركين", "fas fa-list", NavAudience.Admin,
                route: "networkManager-clients",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Clients,
                activeControllers: ["Clients"],
                excludeActions: ["ContractTemplateSettings"])),
            Dropdown("تجهيز وعقود", "fas fa-file-signature",
                ["Clients", "SubscriberInstallationInvoices", "SubscriberInstallationPricing", "ClientPresence"],
                NavAudience.Admin,
                children:
                [
                    Link("admin-contract", "عقد الانضمام", "fas fa-file-signature", NavAudience.Admin,
                        route: "networkManager-clients",
                        routeValues: ActionId("ContractTemplateSettings"),
                        feature: FeatureKeys.Clients,
                        activeControllers: ["Clients"],
                        activeActions: ["ContractTemplateSettings"]),
                    Link("admin-install-inv", "فواتير التجهيز", "fas fa-receipt", NavAudience.Admin,
                        route: "networkManager-subscriber-installation-invoices",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Clients,
                        activeControllers: ["SubscriberInstallationInvoices"]),
                    Link("admin-install-price", "تسعير التجهيز", "fas fa-tags", NavAudience.Admin,
                        route: "networkManager-subscriber-installation-pricing",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Clients,
                        activeControllers: ["SubscriberInstallationPricing"]),
                    Link("admin-presence", "صفحات التواصل", "fas fa-thumbs-up", NavAudience.Admin,
                        route: "networkManager-client-presence",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Clients,
                        activeControllers: ["ClientPresence"])
                ])
        ]
    };

    private static NavSectionDef SubscribersEmployee() => new()
    {
        Key = "subscribers",
        Title = "المشتركون",
        IconClass = "fas fa-users",
        Audience = NavAudience.Employee,
        ExpandControllers = ["Clients", "NewSubscriberWizard"],
        Entries =
        [
            Dropdown("المشتركون", "fas fa-user-circle", ["Clients", "NewSubscriberWizard"], NavAudience.Employee,
                visibleWhen: ctx => ctx.CanClients,
                children:
                [
                    Link("emp-clients", "قائمة المشتركين", "fas fa-list", NavAudience.Employee,
                        area: "CompanyEmployee", controller: "Clients", action: "Index",
                        feature: FeatureKeys.Clients, permission: "Clients.View",
                        activeControllers: ["Clients"], activeActions: ["Index"]),
                    Link("emp-add-client", "إضافة مشترك", "fas fa-user-plus", NavAudience.Employee,
                        route: "employee-new-subscriber-wizard",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Clients, permission: "Clients.Create",
                        activeControllers: ["NewSubscriberWizard"]),
                    Link("emp-import", "استيراد من السيرفر", "fas fa-cloud-download-alt", NavAudience.Employee,
                        area: "CompanyEmployee", controller: "Clients", action: "ImportFromServer",
                        feature: FeatureKeys.Clients, permission: "Clients.ImportFromServer",
                        activeControllers: ["Clients"], activeActions: ["ImportFromServer"]),
                    Link("emp-contract", "عقد الانضمام", "fas fa-file-signature", NavAudience.Employee,
                        route: "networkManager-clients",
                        routeValues: ActionId("ContractTemplateSettings"),
                        feature: FeatureKeys.Clients, permission: "SubscriberContract.View",
                        activeControllers: ["Clients"], activeActions: ["ContractTemplateSettings"]),
                    Link("emp-install-inv", "فواتير التجهيز", "fas fa-receipt", NavAudience.Employee,
                        route: "networkManager-subscriber-installation-invoices",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Clients, permission: "InstallationInvoices.View",
                        activeControllers: ["SubscriberInstallationInvoices"]),
                    Link("emp-install-price", "تسعير التجهيز", "fas fa-tags", NavAudience.Employee,
                        route: "networkManager-subscriber-installation-pricing",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Clients, permission: "InstallationPricing.View",
                        activeControllers: ["SubscriberInstallationPricing"]),
                    Link("emp-presence", "صفحات التواصل", "fas fa-thumbs-up", NavAudience.Employee,
                        route: "networkManager-client-presence",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Clients, permission: "ClientPresence.View",
                        activeControllers: ["ClientPresence"])
                ])
        ]
    };

    /// <summary>قسم مستقل لكل أدوات موظفي AI — أوضح من دمجها تحت الطلبات.</summary>
    private static NavSectionDef AiAdmin() => new()
    {
        Key = "ai",
        Title = "الذكاء الاصطناعي",
        IconClass = "nav-icon-ai",
        CssClass = "nav-section-ai",
        Audience = NavAudience.Admin,
        ExpandControllers = ["CustomerService"],
        Entries =
        [
            Entry(Link("admin-cs-inbox", "صندوق محادثات AI", "fas fa-comments", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "Index",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["Index", "Conversation"],
                badgeDomId: "csWaitingHumanBadge")),
            Entry(Link("admin-cs-social", "منشورات السوشال", "fas fa-share-alt", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "SocialPosts",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["SocialPosts"])),
            Entry(Link("admin-cs-tech-reports", "التقارير التقنية", "fas fa-chart-area", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiTechnicalReports",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiTechnicalReports", "AiTechnicalReport"])),
            Entry(Link("admin-cs-study-reports", "تقارير الدراسات", "fas fa-graduation-cap", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiStudyReports",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiStudyReports", "AiStudyReport"])),
            Entry(Link("admin-cs-mt-scripts", "سكربتات MikroTik", "fas fa-terminal", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiMikroTikScripts",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiMikroTikScripts", "AiMikroTikScript"])),
            Entry(Link("admin-cs-ai-agents", "موظفو AI", "fas fa-user-tie", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiAgents",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiAgents", "AiAgent"])),
            Entry(Link("admin-cs-ai-training", "تدريب فريق AI", "fas fa-graduation-cap", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiTraining",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiTraining"])),
            Entry(Link("admin-cs-ai-workforce", "تنظيم عمل AI", "fas fa-clock", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiWorkforce",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiWorkforce", "AiWorkReport"])),
            Entry(Link("admin-cs-ai-desk", "مكتب المدير", "fas fa-briefcase", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiDesk",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiDesk", "AiDeskThread"])),
            Entry(Link("admin-cs-ai-report", "تقرير فريق AI", "fas fa-chart-line", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "AiTeamReport",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["AiTeamReport"])),
            Entry(Link("admin-cs-settings", "إعدادات فريق AI", "fas fa-sliders-h", NavAudience.Admin,
                area: "CompanyAdmin", controller: "CustomerService", action: "Settings",
                feature: FeatureKeys.Clients,
                activeControllers: ["CustomerService"],
                activeActions: ["Settings"]))
        ]
    };

    private static NavSectionDef AiEmployee() => new()
    {
        Key = "ai",
        Title = "الذكاء الاصطناعي",
        IconClass = "nav-icon-ai",
        CssClass = "nav-section-ai",
        Audience = NavAudience.Employee,
        ExpandControllers = ["CustomerService"],
        Entries =
        [
            Entry(Link("emp-cs-inbox", "صندوق محادثات AI", "fas fa-comments", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "Index",
                feature: FeatureKeys.Clients,
                permission: "CustomerService.Inbox",
                activeControllers: ["CustomerService"],
                activeActions: ["Index", "Conversation"],
                badgeDomId: "csWaitingHumanBadge")),
            Entry(Link("emp-cs-social", "منشورات السوشال", "fas fa-share-alt", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "SocialPosts",
                feature: FeatureKeys.Clients,
                permission: "CustomerService.Social",
                activeControllers: ["CustomerService"],
                activeActions: ["SocialPosts"])),
            Entry(Link("emp-cs-tech-reports", "التقارير التقنية", "fas fa-chart-area", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiTechnicalReports",
                feature: FeatureKeys.Clients,
                permission: "CustomerService.TechnicalReports",
                activeControllers: ["CustomerService"],
                activeActions: ["AiTechnicalReports", "AiTechnicalReport"])),
            Entry(Link("emp-cs-study-reports", "تقارير الدراسات", "fas fa-graduation-cap", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiStudyReports",
                feature: FeatureKeys.Clients,
                permission: "CustomerService.StudyReports",
                activeControllers: ["CustomerService"],
                activeActions: ["AiStudyReports", "AiStudyReport"])),
            Entry(Link("emp-cs-mt-scripts", "سكربتات MikroTik", "fas fa-terminal", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiMikroTikScripts",
                feature: FeatureKeys.Clients,
                permission: "CustomerService.MikroTikScripts",
                activeControllers: ["CustomerService"],
                activeActions: ["AiMikroTikScripts", "AiMikroTikScript"])),
            Entry(Link("emp-cs-agents", "موظفو AI", "fas fa-user-tie", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiAgents",
                feature: FeatureKeys.Clients, permission: "CustomerService.Agents",
                activeControllers: ["CustomerService"], activeActions: ["AiAgents", "AiAgent"])),
            Entry(Link("emp-cs-training", "تدريب فريق AI", "fas fa-graduation-cap", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiTraining",
                feature: FeatureKeys.Clients, permission: "CustomerService.Training",
                activeControllers: ["CustomerService"], activeActions: ["AiTraining"])),
            Entry(Link("emp-cs-workforce", "تنظيم عمل AI", "fas fa-clock", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiWorkforce",
                feature: FeatureKeys.Clients, permission: "CustomerService.Workforce",
                activeControllers: ["CustomerService"], activeActions: ["AiWorkforce", "AiWorkReport"])),
            Entry(Link("emp-cs-desk", "مكتب المدير", "fas fa-briefcase", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiDesk",
                feature: FeatureKeys.Clients, permission: "CustomerService.Desk",
                activeControllers: ["CustomerService"], activeActions: ["AiDesk", "AiDeskThread"])),
            Entry(Link("emp-cs-report", "تقرير فريق AI", "fas fa-chart-line", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "AiTeamReport",
                feature: FeatureKeys.Clients, permission: "CustomerService.Report",
                activeControllers: ["CustomerService"], activeActions: ["AiTeamReport"])),
            Entry(Link("emp-cs-settings", "إعدادات فريق AI", "fas fa-sliders-h", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CustomerService", action: "Settings",
                feature: FeatureKeys.Clients, permission: "CustomerService.Settings",
                activeControllers: ["CustomerService"], activeActions: ["Settings"]))
        ]
    };

    private static NavSectionDef SupportAdmin() => new()
    {
        Key = "support",
        Title = "الطلبات والدعم",
        IconClass = "fas fa-headset",
        Audience = NavAudience.Admin,
        ExpandControllers = ["RequestsManagement", "EmployeeServiceApprovals", "MaintenanceInvoices", "MaintenancePricing", "JoinRequests"],
        Entries =
        [
            Entry(Link("admin-req-board", "لوحة الطلبات", "fas fa-clipboard-list", NavAudience.Admin,
                route: "networkManager-requestsManagement",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Requests,
                activeControllers: ["RequestsManagement"],
                activeActions: ["Index"],
                badgeDomId: "pendingRequestsBadge")),
            Entry(Link("admin-emp-approvals", "موافقات الموظفين", "fas fa-user-check", NavAudience.Admin,
                route: "networkManager-employee-approvals",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Requests,
                activeControllers: ["EmployeeServiceApprovals"],
                usePendingApprovalsBadge: true)),
            Entry(Link("admin-speed", "تغيير السرعة", "fas fa-tachometer-alt", NavAudience.Admin,
                route: "networkManager-requestsManagement",
                routeValues: ActionId("SpeedChangeRequests"),
                feature: FeatureKeys.Requests,
                activeControllers: ["RequestsManagement"],
                activeActions: ["SpeedChangeRequests"])),
            Dropdown("الصيانة", "fas fa-wrench",
                ["MaintenanceInvoices", "MaintenancePricing", "RequestsManagement"],
                NavAudience.Admin,
                children:
                [
                    Link("admin-maint-req", "طلبات الصيانة", "fas fa-tools", NavAudience.Admin,
                        route: "networkManager-requestsManagement",
                        routeValues: ActionId("MaintenanceRequests"),
                        feature: FeatureKeys.Requests,
                        activeControllers: ["RequestsManagement"],
                        activeActions: ["MaintenanceRequests"]),
                    Link("admin-maint-inv", "فواتير الصيانة", "fas fa-file-invoice", NavAudience.Admin,
                        route: "networkManager-maintenance-invoices",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Requests,
                        activeControllers: ["MaintenanceInvoices"]),
                    Link("admin-maint-price", "تسعير الصيانة", "fas fa-tags", NavAudience.Admin,
                        route: "networkManager-maintenance-pricing",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Networks,
                        activeControllers: ["MaintenancePricing"])
                ]),
            Entry(Link("admin-password-resets", "استعادة كلمة المرور", "fas fa-key", NavAudience.Admin,
                route: "networkManager-joinRequests-passwordResets",
                routeValues: ActionId("PasswordResets"),
                feature: FeatureKeys.PasswordResets,
                activeControllers: ["JoinRequests"],
                activeActions: ["PasswordResets", "Index"]))
        ]
    };

    private static NavSectionDef SupportEmployee() => new()
    {
        Key = "support",
        Title = "الطلبات والدعم",
        IconClass = "fas fa-headset",
        Audience = NavAudience.Employee,
        ExpandControllers = ["RequestsManagement"],
        Entries =
        [
            Dropdown("إدارة الطلبات", "fas fa-tasks", ["RequestsManagement"], NavAudience.Employee,
                children:
                [
                    GroupLabel("صيانة · سرعة"),
                    Link("emp-maint", "طلبات الصيانة", "fas fa-wrench", NavAudience.Employee,
                        route: "employee-requestsManagement",
                        routeValues: Dict(("action", "MaintenanceRequests")),
                        feature: FeatureKeys.Requests,
                        permission: "Requests.View|MaintenanceRequests.Manage",
                        activeControllers: ["RequestsManagement"],
                        activeActions: ["MaintenanceRequests"]),
                    Link("emp-speed", "طلبات تغيير السرعة", "fas fa-tachometer-alt", NavAudience.Employee,
                        route: "employee-requestsManagement",
                        routeValues: Dict(("action", "SpeedChangeRequests")),
                        feature: FeatureKeys.Requests,
                        permission: "Requests.View|SpeedChange.Implement|SpeedChange.Approve",
                        activeControllers: ["RequestsManagement"],
                        activeActions: ["SpeedChangeRequests"]),
                    Link("emp-maint-inv", "فواتير الصيانة", "fas fa-file-invoice", NavAudience.Employee,
                        route: "networkManager-maintenance-invoices",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Requests, permission: "MaintenanceInvoices.View",
                        activeControllers: ["MaintenanceInvoices"]),
                    Link("emp-maint-price", "تسعير الصيانة", "fas fa-tags", NavAudience.Employee,
                        route: "networkManager-maintenance-pricing",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Networks, permission: "MaintenancePricing.View",
                        activeControllers: ["MaintenancePricing"]),
                    Link("emp-password-resets", "استعادة كلمة المرور", "fas fa-key", NavAudience.Employee,
                        route: "networkManager-joinRequests-passwordResets",
                        routeValues: ActionId("PasswordResets"),
                        feature: FeatureKeys.PasswordResets, permission: "PasswordResets.View",
                        activeControllers: ["JoinRequests"]),
                    Link("emp-approvals", "موافقات الموظفين", "fas fa-user-check", NavAudience.Employee,
                        route: "networkManager-employee-approvals",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Requests, permission: "EmployeeApprovals.View|EmployeeApprovals.Manage",
                        activeControllers: ["EmployeeServiceApprovals"])
                ])
        ]
    };

    private static NavSectionDef NetworkAdmin() => new()
    {
        Key = "network",
        Title = "الشبكة",
        IconClass = "fas fa-network-wired",
        CssClass = "nav-section-infra",
        Audience = NavAudience.Admin,
        ExpandControllers =
        [
            "Network", "Profile", "MikroTikServers", "MikroTikTraffic", "Sector", "Receiver", "AntennaCalibration"
        ],
        Entries =
        [
            Entry(Link("admin-network", "إعدادات الشبكة", "fas fa-sitemap", NavAudience.Admin,
                route: "networkManager-network",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Networks,
                activeControllers: ["Network"])),
            Entry(Link("admin-profiles", "باقات السرعة", "fas fa-boxes", NavAudience.Admin,
                route: "networkManager-profile",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Profiles,
                activeControllers: ["Profile"])),
            Dropdown("MikroTik", "fas fa-server", ["MikroTikServers", "MikroTikTraffic"], NavAudience.Admin,
                visibleWhen: ctx => ctx.CanMikroTik,
                children:
                [
                    Link("admin-mt-servers", "الخوادم", "fas fa-server", NavAudience.Admin,
                        route: "networkManager-mikrotikservers",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.MikroTikServers,
                        activeControllers: ["MikroTikServers"],
                        excludeActions: ["TrafficAlerts", "TrafficAlertSettings"]),
                    Link("admin-mt-thresholds", "العتبات", "fas fa-sliders", NavAudience.Admin,
                        route: "networkManager-mikrotikservers",
                        routeValues: ActionId("TrafficAlerts"),
                        feature: FeatureKeys.MikroTikServers,
                        requiresNetworkAdmin: true,
                        activeControllers: ["MikroTikServers"],
                        activeActions: ["TrafficAlerts", "TrafficAlertSettings"]),
                    Link("admin-mt-traffic", "مراقبة الترافك", "fas fa-chart-area", NavAudience.Admin,
                        route: "networkManager-mikrotik-traffic",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.MikroTikServers,
                        requiresNetworkAdmin: true,
                        activeControllers: ["MikroTikTraffic"])
                ]),
            Dropdown("المرسلات", "fas fa-broadcast-tower", ["Sector"], NavAudience.Admin,
                visibleWhen: ctx => ctx.CanSectors,
                children:
                [
                    Link("admin-sectors", "قائمة المرسلات", "fas fa-broadcast-tower", NavAudience.Admin,
                        route: "networkManager-sector",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Sectors,
                        activeControllers: ["Sector"],
                        activeActions: ["Index", "Details", "Create", "Edit"]),
                    Link("admin-radio-eng", "التحكم الراديوي", "fas fa-flask-vial", NavAudience.Admin,
                        route: "networkManager-sector",
                        routeValues: ActionId("RadioEngineering"),
                        feature: FeatureKeys.Sectors,
                        activeControllers: ["Sector"],
                        activeActions: ["RadioEngineering"]),
                    Link("admin-radio-mon", "مراقبة القطاعات", "fas fa-signal", NavAudience.Admin,
                        route: "networkManager-sector",
                        routeValues: ActionId("RadioMonitoring"),
                        feature: FeatureKeys.Sectors,
                        activeControllers: ["Sector"],
                        activeActions: ["RadioMonitoring"])
                ]),
            Entry(Link("admin-receivers", "المستقبلات", "fas fa-satellite-dish", NavAudience.Admin,
                route: "networkManager-receiver",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Receivers,
                activeControllers: ["Receiver"])),
            Entry(Link("admin-calibration", "معايرة الهوائي", "fas fa-crosshairs", NavAudience.Admin,
                route: "networkManager-calibration",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Receivers,
                activeControllers: ["AntennaCalibration"]))
        ]
    };

    private static NavSectionDef NetworkEmployee() => new()
    {
        Key = "network",
        Title = "الشبكة",
        IconClass = "fas fa-network-wired",
        Audience = NavAudience.Employee,
        ExpandControllers = ["Network", "Profile", "MikroTikServers", "MikroTikTraffic", "Sector", "Receiver", "AntennaCalibration"],
        Entries =
        [
            Dropdown("البنية التحتية", "fas fa-server",
                ["MikroTikServers", "Sector", "Receiver", "AntennaCalibration"],
                NavAudience.Employee,
                children:
                [
                    GroupLabel("خوادم · مرسلات · مستقبلات"),
                    Link("emp-network", "إعدادات الشبكة", "fas fa-sitemap", NavAudience.Employee,
                        route: "networkManager-network",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Networks, permission: "Network.View",
                        activeControllers: ["Network"]),
                    Link("emp-profiles", "باقات السرعة", "fas fa-boxes", NavAudience.Employee,
                        route: "networkManager-profile",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Profiles, permission: "Profiles.View",
                        activeControllers: ["Profile"]),
                    Link("emp-mt", "المخدمات", "fas fa-server", NavAudience.Employee,
                        area: "CompanyEmployee", controller: "MikroTikServers", action: "Index",
                        feature: FeatureKeys.MikroTikServers, permission: "MikroTikServers.View",
                        activeControllers: ["MikroTikServers"],
                        excludeActions: ["TrafficAlerts", "TrafficAlertSettings"]),
                    Link("emp-mt-thresholds", "عتبات الترافك", "fas fa-sliders", NavAudience.Employee,
                        route: "networkManager-mikrotikservers",
                        routeValues: ActionId("TrafficAlerts"),
                        feature: FeatureKeys.MikroTikServers, permission: "MikroTikServers.Edit",
                        activeControllers: ["MikroTikServers"],
                        activeActions: ["TrafficAlerts", "TrafficAlertSettings"]),
                    Link("emp-mt-traffic", "مراقبة الترافك", "fas fa-chart-area", NavAudience.Employee,
                        route: "networkManager-mikrotik-traffic",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.MikroTikServers, permission: "MikroTikTraffic.View",
                        activeControllers: ["MikroTikTraffic"]),
                    Link("emp-sectors", "المرسلات (القطاعات)", "fas fa-broadcast-tower", NavAudience.Employee,
                        area: "CompanyEmployee", controller: "Sector", action: "Index",
                        feature: FeatureKeys.Sectors, permission: "Sectors.View",
                        activeControllers: ["Sector"],
                        activeActions: ["Index", "Details", "Create", "Edit"]),
                    Link("emp-receivers", "المستقبلات", "fas fa-satellite-dish", NavAudience.Employee,
                        area: "CompanyEmployee", controller: "Receiver", action: "Index",
                        feature: FeatureKeys.Receivers, permission: "Receivers.View",
                        activeControllers: ["Receiver"]),
                    Link("emp-calibration", "معايرة الهوائي", "fas fa-crosshairs", NavAudience.Employee,
                        route: "employee-calibration",
                        routeValues: ActionId("Index"),
                        feature: FeatureKeys.Receivers, permission: "AntennaCalibration.View",
                        activeControllers: ["AntennaCalibration"]),
                    Link("emp-radio-eng", "التحكم الراديوي", "fas fa-flask-vial", NavAudience.Employee,
                        route: "networkManager-sector",
                        routeValues: ActionId("RadioEngineering"),
                        feature: FeatureKeys.Sectors, permission: "Sectors.RadioEngineering",
                        activeControllers: ["Sector"], activeActions: ["RadioEngineering"]),
                    Link("emp-radio-mon", "مراقبة القطاعات", "fas fa-signal", NavAudience.Employee,
                        route: "networkManager-sector",
                        routeValues: ActionId("RadioMonitoring"),
                        feature: FeatureKeys.Sectors, permission: "Sectors.RadioMonitoring",
                        activeControllers: ["Sector"], activeActions: ["RadioMonitoring"])
                ])
        ]
    };

    private static NavSectionDef FinanceAdmin() => new()
    {
        Key = "finance",
        Title = "المالية",
        IconClass = "fas fa-coins",
        Audience = NavAudience.Admin,
        ExpandControllers =
        [
            "Wallet", "ClientWalletTopUpRequests", "CashBox", "Receipts", "CompanyBusiness",
            "Warehouse", "MaterialPurchaseInvoices", "MaterialSalesInvoices", "WarehouseStocktake",
            "MoneyDiary", "Payroll", "FinancialReconciliation", "RenewalRequests"
        ],
        Entries =
        [
            Dropdown("محفظة RadaTik", "fas fa-wallet", ["Wallet", "ClientWalletTopUpRequests"], NavAudience.Admin,
                children:
                [
                    Link("admin-wallet", "نظرة عامة", "fas fa-chart-pie", NavAudience.Admin,
                        route: "networkManager-wallet-index",
                        activeControllers: ["Wallet"], activeActions: ["Index"]),
                    Link("admin-wallet-topup", "طلب تعبئة رصيد", "fas fa-plus-circle", NavAudience.Admin,
                        route: "networkManager-wallet-topup",
                        activeControllers: ["Wallet"], activeActions: ["TopUp"]),
                    Link("admin-wallet-tx", "سجل الحركات", "fas fa-list", NavAudience.Admin,
                        route: "networkManager-wallet-transactions",
                        activeControllers: ["Wallet"], activeActions: ["Transactions"]),
                    Link("admin-client-topups", "تغذية رصيد للمشتركين", "fas fa-user-check", NavAudience.Admin,
                        route: "networkManager-client-wallet-topups",
                        routeValues: ActionId("Index"),
                        activeControllers: ["ClientWalletTopUpRequests"])
                ]),
            Dropdown("النقد والقبض", "fas fa-money-bill-wave", ["CashBox", "Receipts"], NavAudience.Admin,
                children:
                [
                    Link("admin-cashbox", "الصندوق النقدي", "fas fa-cash-register", NavAudience.Admin,
                        route: "networkManager-cashbox",
                        routeValues: ActionId("Index"),
                        activeControllers: ["CashBox"]),
                    Link("admin-receipts", "سجل القبض", "fas fa-receipt", NavAudience.Admin,
                        route: "networkManager-receipts",
                        routeValues: ActionId("Index"),
                        activeControllers: ["Receipts"])
                ]),
            Dropdown("دفاتر الشركة", "fas fa-building",
                [
                    "CompanyBusiness", "Warehouse", "MaterialPurchaseInvoices", "MaterialSalesInvoices",
                    "WarehouseStocktake", "MoneyDiary", "Payroll", "FinancialReconciliation"
                ],
                NavAudience.Admin,
                visibleWhen: ctx => ctx.HasCompanyBooks,
                children: BooksFullChildren()),
            Entry(Link("admin-renewal", "تجديد نقاط التحصيل", "fas fa-sync-alt", NavAudience.Admin,
                route: "networkManager-renewalRequests",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Networks,
                activeControllers: ["RenewalRequests"]))
        ]
    };

    private static NavLinkDef[] BooksFullChildren() =>
    [
        Link("admin-biz", "نظرة عامة", "fas fa-chart-pie", NavAudience.Admin,
            route: "networkManager-company-business",
            routeValues: ActionId("Index"),
            activeControllers: ["CompanyBusiness"]),
        Link("admin-wh", "المستودع", "fas fa-warehouse", NavAudience.Admin,
            route: "networkManager-warehouse",
            routeValues: ActionId("Index"),
            feature: FeatureKeys.Warehouse,
            activeControllers: ["Warehouse"]),
        Link("admin-mat-buy", "شراء مواد", "fas fa-cart-plus", NavAudience.Admin,
            route: "networkManager-material-purchase",
            routeValues: ActionId("Index"),
            feature: FeatureKeys.Warehouse,
            activeControllers: ["MaterialPurchaseInvoices"]),
        Link("admin-mat-sell", "بيع مواد", "fas fa-receipt", NavAudience.Admin,
            route: "networkManager-material-sales",
            routeValues: ActionId("Index"),
            feature: FeatureKeys.Warehouse,
            activeControllers: ["MaterialSalesInvoices"]),
        Link("admin-stocktake", "جرد المستودع", "fas fa-clipboard-check", NavAudience.Admin,
            route: "networkManager-warehouse-stocktake",
            routeValues: ActionId("Index"),
            feature: FeatureKeys.Warehouse,
            activeControllers: ["WarehouseStocktake"]),
        Link("admin-diary", "دفتر الإيراد والمصروف", "fas fa-book", NavAudience.Admin,
            route: "networkManager-money-diary",
            routeValues: ActionId("Index"),
            feature: FeatureKeys.MoneyDiary,
            activeControllers: ["MoneyDiary"]),
        Link("admin-recon", "جرد مالي", "fas fa-balance-scale", NavAudience.Admin,
            route: "networkManager-financial-reconciliation",
            routeValues: ActionId("Index"),
            feature: FeatureKeys.MoneyDiary,
            activeControllers: ["FinancialReconciliation"]),
        Link("admin-payroll", "رواتب الموظفين", "fas fa-hand-holding-usd", NavAudience.Admin,
            route: "networkManager-payroll",
            routeValues: ActionId("Index"),
            feature: FeatureKeys.Payroll,
            activeControllers: ["Payroll"])
    ];

    private static NavSectionDef FinanceEmployee() => new()
    {
        Key = "finance",
        Title = "المالية",
        IconClass = "fas fa-coins",
        Audience = NavAudience.Employee,
        ExpandControllers =
        [
            "CompanyPayroll", "MoneyDiary", "Warehouse", "WarehouseStocktake",
            "MaterialPurchaseInvoices", "MaterialSalesInvoices", "Wallet", "CashBox",
            "Receipts", "CompanyBusiness", "ClientWalletTopUpRequests", "RenewalRequests", "RenewalReminders"
        ],
        Entries =
        [
            Entry(Link("emp-co-payroll", "رواتب الشركة", "fas fa-money-check-alt", NavAudience.Employee,
                area: "CompanyEmployee", controller: "CompanyPayroll", action: "Index",
                feature: FeatureKeys.Payroll, permission: "Payroll.View",
                activeControllers: ["CompanyPayroll"])),
            Entry(Link("emp-diary", "دفتر الإيراد والمصروف", "fas fa-book", NavAudience.Employee,
                area: "CompanyEmployee", controller: "MoneyDiary", action: "Index",
                feature: FeatureKeys.MoneyDiary, permission: "MoneyDiary.View",
                activeControllers: ["MoneyDiary"], activeActions: ["Index"])),
            Entry(Link("emp-recon", "الجرد المالي", "fas fa-balance-scale", NavAudience.Employee,
                area: "CompanyEmployee", controller: "MoneyDiary", action: "Reconciliation",
                feature: FeatureKeys.MoneyDiary, permission: "FinancialReconciliation.View",
                activeControllers: ["MoneyDiary"], activeActions: ["Reconciliation"])),
            Entry(Link("emp-wh", "المستودع", "fas fa-boxes", NavAudience.Employee,
                area: "CompanyEmployee", controller: "Warehouse", action: "Index",
                feature: FeatureKeys.Warehouse, permission: "Warehouse.View",
                activeControllers: ["Warehouse"], activeActions: ["Index"])),
            Entry(Link("emp-stocktake", "جرد المستودع", "fas fa-clipboard-check", NavAudience.Employee,
                area: "CompanyEmployee", controller: "WarehouseStocktake", action: "Index",
                feature: FeatureKeys.Warehouse, permission: "WarehouseStocktake.Manage",
                activeControllers: ["WarehouseStocktake"])),
            Entry(Link("emp-mat-buy", "متابعة فواتير الشراء", "fas fa-file-invoice", NavAudience.Employee,
                area: "CompanyEmployee", controller: "MaterialPurchaseInvoices", action: "Index",
                feature: FeatureKeys.Warehouse, permission: "MaterialPurchase.View",
                activeControllers: ["MaterialPurchaseInvoices"])),
            Entry(Link("emp-mat-sell", "متابعة فواتير البيع", "fas fa-receipt", NavAudience.Employee,
                area: "CompanyEmployee", controller: "MaterialSalesInvoices", action: "Index",
                feature: FeatureKeys.Warehouse, permission: "MaterialSales.View",
                activeControllers: ["MaterialSalesInvoices"])),
            Entry(Link("emp-wallet", "محفظة الشركة", "fas fa-wallet", NavAudience.Employee,
                route: "networkManager-wallet-index",
                permission: "Wallet.View",
                activeControllers: ["Wallet"], activeActions: ["Index", "Transactions"])),
            Entry(Link("emp-wallet-topup", "طلب تعبئة الرصيد", "fas fa-plus-circle", NavAudience.Employee,
                route: "networkManager-wallet-topup",
                permission: "Wallet.TopUp",
                activeControllers: ["Wallet"], activeActions: ["TopUp"])),
            Entry(Link("emp-client-topups", "تغذية رصيد للمشتركين", "fas fa-user-check", NavAudience.Employee,
                route: "networkManager-client-wallet-topups",
                routeValues: ActionId("Index"),
                permission: "ClientWalletTopUp.View|ClientWalletTopUp.Manage",
                activeControllers: ["ClientWalletTopUpRequests"])),
            Entry(Link("emp-cashbox", "الصندوق النقدي", "fas fa-cash-register", NavAudience.Employee,
                route: "networkManager-cashbox",
                routeValues: ActionId("Index"),
                permission: "CashBox.View",
                activeControllers: ["CashBox"])),
            Entry(Link("emp-receipts", "سجل القبض", "fas fa-receipt", NavAudience.Employee,
                route: "networkManager-receipts",
                routeValues: ActionId("Index"),
                permission: "Receipts.View",
                activeControllers: ["Receipts"])),
            Entry(Link("emp-biz", "نظرة أعمال الشركة", "fas fa-chart-pie", NavAudience.Employee,
                route: "networkManager-company-business",
                routeValues: ActionId("Index"),
                permission: "CompanyBusiness.View",
                activeControllers: ["CompanyBusiness"])),
            Entry(Link("emp-renewal", "تجديد نقاط التحصيل", "fas fa-sync-alt", NavAudience.Employee,
                route: "networkManager-renewalRequests",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Networks, permission: "CollectionPointRenewals.View",
                activeControllers: ["RenewalRequests"])),
            Entry(Link("emp-renewal-reminders", "تذكير التجديد", "fas fa-bell", NavAudience.Employee,
                area: "CompanyAdmin", controller: "RenewalReminders", action: "Index",
                feature: FeatureKeys.Networks, permission: "RenewalReminders.View",
                activeControllers: ["RenewalReminders"]))
        ]
    };

    private static NavSectionDef ErpEmployee() => new()
    {
        Key = "erp",
        Title = "نظام ERP",
        IconClass = "fas fa-sitemap",
        Audience = NavAudience.Employee,
        ExpandControllers =
        [
            "ErpHub", "ErpCustomers", "ErpSuppliers", "CompanyEmployeeTasks",
            "EmployeeRewardPenalties", "CompanyAccounting", "ErpReports", "ErpDossier"
        ],
        Entries =
        [
            Entry(Link("emp-erp-hub", "لوحة ERP", "fas fa-gauge-high", NavAudience.Employee,
                route: "networkManager-erp",
                feature: FeatureKeys.Erp, permission: "Erp.View|Erp.Manage",
                activeControllers: ["ErpHub"])),
            Entry(Link("emp-erp-dossier", "الأضابير الإلكترونية", "fas fa-folder-open", NavAudience.Employee,
                route: "networkManager-erp-dossier",
                feature: FeatureKeys.Erp, permission: "Erp.Dossier.View",
                activeControllers: ["ErpDossier"])),
            Entry(Link("emp-erp-cust", "عملاء ERP", "fas fa-handshake", NavAudience.Employee,
                route: "networkManager-erp-customers",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp, permission: "Erp.Customers.View|Erp.Customers.Manage",
                activeControllers: ["ErpCustomers"])),
            Entry(Link("emp-erp-sup", "الموردون", "fas fa-truck", NavAudience.Employee,
                route: "networkManager-erp-suppliers",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp, permission: "Erp.Suppliers.View|Erp.Suppliers.Manage",
                activeControllers: ["ErpSuppliers"])),
            Entry(Link("emp-erp-tasks", "مهام الموظفين", "fas fa-tasks", NavAudience.Employee,
                route: "networkManager-erp-tasks",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp, permission: "Erp.Tasks.View|Erp.Tasks.Manage",
                activeControllers: ["CompanyEmployeeTasks"])),
            Entry(Link("emp-erp-rewards", "مكافآت وعقوبات", "fas fa-award", NavAudience.Employee,
                route: "networkManager-erp-rewards",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp, permission: "Erp.Rewards.View|Erp.Rewards.Manage",
                activeControllers: ["EmployeeRewardPenalties"])),
            Entry(Link("emp-erp-acc", "المحاسبة", "fas fa-calculator", NavAudience.Employee,
                route: "networkManager-erp-accounting",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp, permission: "Erp.Accounting.View|Erp.Accounting.Manage",
                activeControllers: ["CompanyAccounting"])),
            Entry(Link("emp-erp-rep", "تقارير ERP", "fas fa-chart-bar", NavAudience.Employee,
                route: "networkManager-erp-reports",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp, permission: "Erp.Reports.View",
                activeControllers: ["ErpReports"]))
        ]
    };

    private static NavSectionDef ReportsEmployee() => new()
    {
        Key = "reports",
        Title = "التقارير",
        IconClass = "fas fa-chart-line",
        Audience = NavAudience.Employee,
        ExpandControllers = ["Reports", "Features"],
        Entries =
        [
            Entry(Link("emp-reports", "تقارير الشبكة", "fas fa-chart-bar", NavAudience.Employee,
                route: "networkManager-reports",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Reports, permission: "Reports.View",
                activeControllers: ["Reports"])),
            Entry(Link("emp-features", "الخدمات والاشتراك", "fas fa-puzzle-piece", NavAudience.Employee,
                route: "networkManager-features",
                permission: "CompanySubscription.View",
                activeControllers: ["Features"]))
        ]
    };

    private static NavSectionDef TeamEmployee() => new()
    {
        Key = "team",
        Title = "الفريق",
        IconClass = "fas fa-users-cog",
        Audience = NavAudience.Employee,
        ExpandControllers = ["Admin", "CollectionPoints", "CompanySettings"],
        Entries =
        [
            Entry(Link("emp-employees", "الموظفون", "fas fa-user-tie", NavAudience.Employee,
                route: "networkManager-users-employees",
                feature: FeatureKeys.Users, permission: "Employees.View|Employees.Manage",
                activeControllers: ["Admin"],
                activeQueryType: "employees")),
            Entry(Link("emp-points", "حسابات نقاط التحصيل", "fas fa-store", NavAudience.Employee,
                route: "networkManager-users-collectionPoints",
                feature: FeatureKeys.Users, permission: "CollectionPointAccounts.View|CollectionPointAccounts.Manage",
                activeControllers: ["Admin"],
                activeQueryType: "points")),
            Entry(Link("emp-points-ops", "سجل نقاط التحصيل", "fas fa-receipt", NavAudience.Employee,
                route: "networkManager-collectionpoints",
                feature: FeatureKeys.CollectionPoints, permission: "CollectionPointAccounts.View|CollectionPointAccounts.Manage",
                activeControllers: ["CollectionPoints"])),
            Entry(Link("emp-portal-users", "حسابات البوابة", "fas fa-user-friends", NavAudience.Employee,
                route: "networkManager-users-clients",
                feature: FeatureKeys.Users, permission: "PortalAccounts.View",
                activeControllers: ["Admin"],
                activeQueryType: "clients")),
            Entry(Link("emp-company-settings", "إعدادات الشركة", "fas fa-sliders", NavAudience.Employee,
                route: "networkManager-company-settings",
                routeValues: ActionId("Index"),
                permission: "CompanySettings.View|CompanySettings.Manage",
                activeControllers: ["CompanySettings"],
                activeActions: ["Index"])),
            Entry(Link("emp-company-approvals", "موافقات الإعدادات", "fas fa-clipboard-check", NavAudience.Employee,
                route: "networkManager-company-settings",
                routeValues: ActionId("ChangeRequests"),
                permission: "CompanySettings.Approvals",
                activeControllers: ["CompanySettings"],
                activeActions: ["ChangeRequests"]))
        ]
    };

    private static NavSectionDef ErpAdmin() => new()
    {
        Key = "erp",
        Title = "نظام ERP",
        IconClass = "fas fa-sitemap",
        CssClass = "nav-section-erp",
        Audience = NavAudience.Admin,
        ExpandControllers =
        [
            "ErpHub", "ErpCustomers", "ErpSuppliers", "CompanyEmployeeTasks",
            "EmployeeRewardPenalties", "CompanyAccounting", "ErpReports", "ErpDossier"
        ],
        Entries =
        [
            Entry(Link("admin-erp-hub", "لوحة ERP", "fas fa-gauge-high", NavAudience.Admin,
                route: "networkManager-erp",
                feature: FeatureKeys.Erp,
                activeControllers: ["ErpHub"],
                featured: true)),
            Entry(Link("admin-erp-dossier", "الأضابير الإلكترونية", "fas fa-folder-open", NavAudience.Admin,
                route: "networkManager-erp-dossier",
                feature: FeatureKeys.Erp,
                activeControllers: ["ErpDossier"])),
            Entry(Link("admin-erp-cust", "عملاء ERP", "fas fa-handshake", NavAudience.Admin,
                route: "networkManager-erp-customers",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp,
                activeControllers: ["ErpCustomers"])),
            Entry(Link("admin-erp-sup", "الموردون", "fas fa-truck", NavAudience.Admin,
                route: "networkManager-erp-suppliers",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp,
                activeControllers: ["ErpSuppliers"],
                visibleWhen: ctx => ctx.CanErp && ctx.CanWarehouse)),
            Entry(Link("admin-erp-tasks", "مهام الموظفين", "fas fa-tasks", NavAudience.Admin,
                route: "networkManager-erp-tasks",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp,
                activeControllers: ["CompanyEmployeeTasks"])),
            Entry(Link("admin-erp-rewards", "مكافآت وعقوبات", "fas fa-award", NavAudience.Admin,
                route: "networkManager-erp-rewards",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp,
                activeControllers: ["EmployeeRewardPenalties"])),
            Entry(Link("admin-erp-acc", "المحاسبة", "fas fa-calculator", NavAudience.Admin,
                route: "networkManager-erp-accounting",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp,
                activeControllers: ["CompanyAccounting"])),
            Entry(Link("admin-erp-rep", "تقارير ERP", "fas fa-chart-bar", NavAudience.Admin,
                route: "networkManager-erp-reports",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Erp,
                activeControllers: ["ErpReports"]))
        ]
    };

    private static NavSectionDef ReportsAdmin() => new()
    {
        Key = "reports",
        Title = "التقارير",
        IconClass = "fas fa-chart-line",
        Audience = NavAudience.Admin,
        ExpandControllers = ["Reports", "Features"],
        Entries =
        [
            Entry(Link("admin-reports", "تقارير الشبكة", "fas fa-chart-bar", NavAudience.Admin,
                route: "networkManager-reports",
                routeValues: ActionId("Index"),
                feature: FeatureKeys.Reports,
                activeControllers: ["Reports"])),
            Entry(Link("admin-features", "الخدمات والاشتراك", "fas fa-puzzle-piece", NavAudience.Admin,
                route: "networkManager-features",
                activeControllers: ["Features"],
                hint: "باقات المنصة والميزات"))
        ]
    };

    private static NavSectionDef TeamAdmin() => new()
    {
        Key = "team",
        Title = "الفريق",
        IconClass = "fas fa-users-cog",
        Audience = NavAudience.Admin,
        ExpandControllers = ["Admin", "CollectionPoints"],
        Entries =
        [
            Entry(Link("admin-employees", "الموظفون", "fas fa-user-tie", NavAudience.Admin,
                route: "networkManager-users-employees",
                feature: FeatureKeys.Users,
                activeControllers: ["Admin"],
                activeQueryType: "employees")),
            Entry(Link("admin-points", "حسابات نقاط التحصيل", "fas fa-store", NavAudience.Admin,
                route: "networkManager-users-collectionPoints",
                feature: FeatureKeys.Users,
                activeControllers: ["Admin"],
                activeQueryType: "points")),
            Entry(Link("admin-points-ops", "سجل نقاط التحصيل", "fas fa-receipt", NavAudience.Admin,
                route: "networkManager-collectionpoints",
                feature: FeatureKeys.CollectionPoints,
                activeControllers: ["CollectionPoints"])),
            Entry(Link("admin-portal-users", "حسابات البوابة", "fas fa-user-friends", NavAudience.Admin,
                route: "networkManager-users-clients",
                feature: FeatureKeys.Users,
                activeControllers: ["Admin"],
                activeQueryType: "clients")),
            Entry(Link("admin-add-emp", "إضافة موظف", "fas fa-user-plus", NavAudience.Admin,
                route: "networkManager-addnewEmployee",
                feature: FeatureKeys.Users,
                activeControllers: []))
        ]
    };

    private static NavSectionDef CustomAdmin() => new()
    {
        Key = "custom",
        Title = "خدمات إضافية",
        IconClass = "fas fa-layer-group",
        Audience = NavAudience.Admin,
        Entries = [] // filled by builder
    };

    private static NavSectionDef AccountAdmin() => new()
    {
        Key = "account",
        CssClass = "nav-section-account",
        Flat = true,
        Audience = NavAudience.Admin,
        ExpandControllers = ["Account", "CompanySettings"],
        Entries =
        [
            Entry(Link("admin-company-settings", "إعدادات الشركة", "fas fa-sliders", NavAudience.Admin,
                route: "networkManager-company-settings",
                routeValues: ActionId("Index"),
                activeControllers: ["CompanySettings"],
                activeActions: ["Index"],
                hint: "ملكية · شركاء · زكاة",
                visibleWhen: ctx => !ctx.CompanySettingsSetupIncomplete)),
            Entry(Link("admin-company-settings-setup-account", "إنهاء تهيئة إعدادات الشركة", "fas fa-sliders", NavAudience.Admin,
                route: "networkManager-company-settings-setup",
                activeControllers: ["CompanySettings"],
                activeActions: ["Setup"],
                featured: true,
                extraCss: "nav-link-setup-alert",
                hint: "لم تكتمل بعد",
                visibleWhen: ctx => ctx.CompanySettingsSetupIncomplete)),
            Entry(Link("admin-company-settings-approvals", "موافقات الإعدادات", "fas fa-clipboard-check", NavAudience.Admin,
                route: "networkManager-company-settings",
                routeValues: ActionId("ChangeRequests"),
                activeControllers: ["CompanySettings"],
                activeActions: ["ChangeRequests"],
                useCompanySettingsApprovalsBadge: true)),
            Entry(Link("admin-profile", "الملف الشخصي", "fas fa-user-circle", NavAudience.Admin,
                route: "networkManager-account-profile",
                activeControllers: ["Account"],
                activeActions: ["Profile"]))
        ]
    };

    private static NavSectionDef AccountEmployee() => new()
    {
        Key = "account",
        Title = "حسابي الشخصي",
        IconClass = "fas fa-user-circle",
        CssClass = "nav-section-account",
        StaticTitle = true,
        Audience = NavAudience.Employee,
        ExpandControllers = ["Account", "MyPayroll"],
        Entries =
        [
            Entry(Link("emp-profile", "بروفايلي", "fas fa-id-card", NavAudience.Employee,
                route: "employee-profile",
                activeControllers: ["Account"],
                hint: "الاسم · البريد · كلمة المرور")),
            Entry(Link("emp-my-payroll", "راتبي ومحفظتي", "fas fa-wallet", NavAudience.Employee,
                route: "employee-my-payroll",
                feature: FeatureKeys.Payroll,
                activeControllers: ["MyPayroll"],
                hint: "مستحقات · مكافآت · خصومات",
                extraCss: "nav-link-personal-wallet"))
        ]
    };

    // —— helpers ——

    private static NavEntryDef Entry(NavLinkDef link) => new()
    {
        Link = link,
        Audience = link.Audience,
        VisibleWhen = link.VisibleWhen
    };

    private static NavEntryDef Dropdown(
        string title,
        string icon,
        string[] openControllers,
        NavAudience audience,
        IReadOnlyList<NavLinkDef>? children = null,
        Func<NavBuildContext, bool>? visibleWhen = null)
    {
        List<NavLinkDef> kids = [];
        if (children != null)
        {
            foreach (NavLinkDef c in children)
            {
                kids.Add(c);
            }
        }

        return new NavEntryDef
        {
            IsDropdown = true,
            DropdownTitle = title,
            DropdownIcon = icon,
            DropdownOpenControllers = openControllers,
            Children = kids,
            Audience = audience,
            VisibleWhen = visibleWhen
        };
    }

    private static NavLinkDef GroupLabel(string text) => new()
    {
        Key = "label:" + text,
        Title = text,
        IconClass = "",
        Audience = NavAudience.Both
    };

    private static NavLinkDef Link(
        string key,
        string title,
        string icon,
        NavAudience audience,
        string? route = null,
        string? area = null,
        string? controller = null,
        string? action = null,
        IReadOnlyDictionary<string, string?>? routeValues = null,
        string? feature = null,
        string? permission = null,
        string[]? activeControllers = null,
        string[]? activeActions = null,
        string[]? excludeActions = null,
        string? activeQueryType = null,
        bool featured = false,
        string? badgeDomId = null,
        bool usePendingApprovalsBadge = false,
        bool useCompanySettingsApprovalsBadge = false,
        string? hint = null,
        string? extraCss = null,
        bool requiresNetworkAdmin = false,
        Func<NavBuildContext, bool>? visibleWhen = null) => new()
    {
        Key = key,
        Title = title,
        IconClass = icon,
        Audience = audience,
        RouteName = route,
        Area = area,
        Controller = controller,
        Action = action,
        RouteValues = routeValues,
        FeatureKey = feature,
        PermissionKey = permission,
        ActiveControllers = activeControllers ?? [],
        ActiveActions = activeActions,
        ExcludeActions = excludeActions,
        ActiveQueryType = activeQueryType,
        Featured = featured,
        BadgeDomId = badgeDomId,
        UsePendingApprovalsBadge = usePendingApprovalsBadge,
        UseCompanySettingsApprovalsBadge = useCompanySettingsApprovalsBadge,
        Hint = hint,
        ExtraCssClass = extraCss,
        RequiresNetworkAdminRole = requiresNetworkAdmin,
        VisibleWhen = visibleWhen
    };

    private static IReadOnlyDictionary<string, string?> ActionId(string action) =>
        Dict(("action", action), ("id", ""));

    private static IReadOnlyDictionary<string, string?> Dict(params (string Key, string? Value)[] pairs)
    {
        Dictionary<string, string?> d = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string k, string? v) in pairs)
        {
            d[k] = v;
        }

        return d;
    }
}
