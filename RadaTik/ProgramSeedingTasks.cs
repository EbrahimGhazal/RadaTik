using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;

namespace RadaTik;

internal static class ProgramSeedingTasks
{
    public static async Task CreateDefaultRolesAndSeedData(IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await EnsureIdentityTablesReadyAsync(db, logger))
        {
            logger.LogCritical(
                "تعذر التأكد من جداول Identity (AspNetRoles/AspNetUsers). لن تُنشأ الأدوار أو حساب مدير النظام. نفّذ: dotnet ef database update");
            return;
        }

        try
        {
            string[] roleNames =
            {
                RoleNames.SystemAdministrator,
                RoleNames.NetworkAdministrator,
                RoleNames.CompanyEmployee,
                RoleNames.SystemEmployee,
                RoleNames.EmployeeLegacy,
                RoleNames.CollectionPoint,
                RoleNames.Client
            };

            foreach (string roleName in roleNames)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                IdentityResult createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createResult.Succeeded)
                {
                    logger.LogWarning(
                        "تعذر إنشاء الدور {RoleName}: {Errors}",
                        roleName,
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));
                }
            }

            await DefaultSystemAdministratorAccountBootstrap.EnsureWhenIdentityTablesExistAsync(
                db,
                userManager,
                roleManager,
                logger);

            await SeedDefaultPermissions(db);
            await SeedDefaultPaymentMethods(db);
            await SeedDefaultSystemFeaturePricing(db);
            await SeedCompanyBusinessModulesAsync(db, logger);

            IUsageBasedSubscriptionChargeService usageCharge =
                scope.ServiceProvider.GetRequiredService<IUsageBasedSubscriptionChargeService>();
            await EnsureAllMainCompanyNetworksFullEntitlementsAsync(db, usageCharge, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "فشل إنشاء الأدوار أو البذور الأساسية بعد التحقق من الجداول.");
        }
    }

    /// <summary>
    /// يفعّل جميع خدمات النظام لكل شبكة شركة رئيسية (المدراء الحاليون والجديدون عبر الشبكة).
    /// </summary>
    private static async Task EnsureAllMainCompanyNetworksFullEntitlementsAsync(
        ApplicationDbContext db,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ILogger logger)
    {
        try
        {
            List<int> companyNetworkIds = await db.Networks
                .AsNoTracking()
                .Where(n => n.ParentNetworkId == null)
                .Select(n => n.Id)
                .ToListAsync();

            foreach (int networkId in companyNetworkIds)
            {
                try
                {
                    await CompanySubscriptionBootstrap.EnsureFullCompanyEntitlementsAsync(
                        db,
                        usageChargeService,
                        networkId,
                        logger);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "تعذر تفعيل جميع الخدمات للشبكة الرئيسية #{NetworkId}",
                        networkId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "تعذر بذر تفعيل الخدمات الكامل لشبكات الشركات.");
        }
    }

    private static async Task<bool> EnsureIdentityTablesReadyAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await DefaultSystemAdministratorAccountBootstrap.IdentityTablesExistAsync(db))
        {
            return true;
        }

        logger.LogWarning("جداول Identity غير مكتملة. إعادة محاولة تطبيق الهجرات...");
        try
        {
            await db.Database.MigrateAsync();
            await SchemaColumnRepair.EnsurePendingColumnsAsync(db, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "فشل تطبيق الهجرات عند محاولة إنشاء جداول Identity.");
            return false;
        }

        if (await DefaultSystemAdministratorAccountBootstrap.IdentityTablesExistAsync(db))
        {
            return true;
        }

        logger.LogCritical(
            "جداول AspNetRoles/AspNetUsers غير موجودة بعد الهجرات. تحقق من سلسلة الهجرات وقاعدة البيانات.");
        return false;
    }

    private static async Task SeedDefaultPermissions(ApplicationDbContext db)
    {
        try
        {
            List<(string Key, string DisplayName, string Category)> defaults = new List<(string Key, string DisplayName, string Category)>
            {
                ("Sectors.View", "المرسلات (القطاعات) - عرض", "المرسلات"),
                ("Sectors.Create", "المرسلات (القطاعات) - إضافة", "المرسلات"),
                ("Sectors.Edit", "المرسلات (القطاعات) - تعديل", "المرسلات"),
                ("Sectors.Delete", "المرسلات (القطاعات) - حذف", "المرسلات"),

                ("Receivers.View", "المستقبلات - عرض", "المستقبلات"),
                ("Receivers.Create", "المستقبلات - إضافة", "المستقبلات"),
                ("Receivers.Edit", "المستقبلات - تعديل", "المستقبلات"),
                ("Receivers.Delete", "المستقبلات - حذف", "المستقبلات"),

                ("Clients.View", "العملاء - عرض", "العملاء"),
                ("Clients.Create", "العملاء - إضافة", "العملاء"),
                ("Clients.Edit", "العملاء - تعديل", "العملاء"),
                ("Clients.Delete", "العملاء - حذف", "العملاء"),
                ("Clients.ImportFromServer", "العملاء - استيراد من السيرفر", "العملاء"),

                ("MikroTikServers.View", "المخدمات - عرض", "المخدمات"),
                ("MikroTikServers.Create", "المخدمات - إضافة", "المخدمات"),
                ("MikroTikServers.Edit", "المخدمات - تعديل", "المخدمات"),
                ("MikroTikServers.Delete", "المخدمات - حذف", "المخدمات"),
                ("MikroTikServers.Manage", "المخدمات - إدارة (قديم)", "المخدمات"),

                ("Requests.View", "إدارة الطلبات - عرض", "الطلبات"),
                ("MaintenanceRequests.Manage", "طلبات الصيانة - إدارة/معالجة", "الطلبات"),
                ("SpeedChange.Approve", "طلبات تغيير السرعة - موافقة أو رفض", "الطلبات"),
                ("SpeedChange.Implement", "طلبات تغيير السرعة - تنفيذ التغيير", "الطلبات"),

                ("MaintenancePricing.View", "تسعير الصيانة - عرض", "التسعير"),
                ("MaintenancePricing.Manage", "تسعير الصيانة - إدارة", "التسعير"),
                ("MaintenanceInvoices.View", "فواتير الصيانة - عرض", "الطلبات"),
                ("MaintenanceInvoices.Pay", "فواتير الصيانة - تسديد", "الطلبات"),

                ("Warehouse.View", "المستودع - عرض الأصناف والحركات", "المستودع"),
                ("Warehouse.Manage", "المستودع - إدارة (وارد/صادر/تصحيح)", "المستودع"),
                ("WarehouseStocktake.Manage", "جرد المستودع - تنفيذ واعتماد", "المستودع"),
                ("MaterialPurchase.View", "فواتير شراء المواد - عرض", "المشتريات"),
                ("MaterialPurchase.Manage", "فواتير شراء المواد - إنشاء وتعديل", "المشتريات"),
                ("MaterialSales.View", "فواتير بيع المواد - عرض", "المبيعات"),
                ("MaterialSales.Manage", "فواتير بيع المواد - إنشاء وتعديل", "المبيعات"),

                ("MoneyDiary.View", "دفتر الإيراد والمصروف - عرض", "المالية"),
                ("MoneyDiary.Manage", "دفتر الإيراد والمصروف - تسجيل واعتماد", "المالية"),
                ("FinancialReconciliation.View", "الجرد المالي - عرض", "المالية"),
                ("Payroll.View", "رواتب الموظفين - عرض", "المالية"),
                ("Payroll.Manage", "رواتب الموظفين - إدارة الدفعات", "المالية"),
                ("Payroll.WalletTopUp.Request", "محفظة الموظف - طلب تغذية", "المالية"),
                ("Payroll.WalletTopUp.Manage", "محفظة الموظف - إدارة التغذية", "المالية"),

                ("Erp.View", "نظام ERP - عرض", "ERP"),
                ("Erp.Manage", "نظام ERP - إدارة", "ERP"),
            };

            List<string> existingKeys = await db.Permissions.Select(p => p.Key).ToListAsync();
            HashSet<string> existingSet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<Permission> toAdd = defaults
                .Where(p => !existingSet.Contains(p.Key))
                .Select(p => new Permission
                {
                    Key = p.Key,
                    DisplayName = p.DisplayName,
                    Category = p.Category
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                db.Permissions.AddRange(toAdd);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"تعذر إنشاء الصلاحيات الافتراضية: {ex.Message}");
        }
    }

    private static async Task SeedDefaultPaymentMethods(ApplicationDbContext db)
    {
        try
        {
            List<(string Name, int Order, bool IsCash)> defaults = new List<(string Name, int Order, bool IsCash)>
            {
                ("كاش", 0, true),
                ("نقدي", 1, true),
                ("دفع مباشر", 2, false),
                ("شام كاش", 3, false),
                ("بنك", 4, false),
                ("بنك بيمو", 5, false),
                ("بنك البركة", 6, false)
            };

            List<string> existingNames = await db.PaymentMethods.Select(m => m.Name).ToListAsync();
            HashSet<string> existingSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<PaymentMethod> toAdd = defaults
                .Where(x => !existingSet.Contains(x.Name))
                .Select(x => new PaymentMethod
                {
                    Name = x.Name,
                    DisplayOrder = x.Order,
                    IsActive = true,
                    IsCash = x.IsCash,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                db.PaymentMethods.AddRange(toAdd);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"تعذر إنشاء طرق الدفع الافتراضية: {ex.Message}");
        }
    }

    private static async Task SeedDefaultSystemFeaturePricing(ApplicationDbContext db)
    {
        try
        {
            List<(string FeatureKey, PricingChargeUnit Unit, decimal AmountSyp, decimal AmountUsd, bool IsActive)> defaults = new List<(string FeatureKey, PricingChargeUnit Unit, decimal AmountSyp, decimal AmountUsd, bool IsActive)>
            {
                (FeatureKeys.CollectionCommission, PricingChargeUnit.PercentOfCollectedAmount, 0m, 0m, true),
                (FeatureKeys.MaintenanceTransportFee, PricingChargeUnit.Flat, 0m, 0m, true),
                (FeatureKeys.MaintenanceCommission, PricingChargeUnit.Flat, 0m, 0m, true)
            };

            foreach ((string FeatureKey, PricingChargeUnit Unit, decimal AmountSyp, decimal AmountUsd, bool IsActive) d in defaults)
            {
                bool exists = await db.FeaturePricings.AnyAsync(p =>
                    p.FeatureKey == d.FeatureKey &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime);
                if (exists)
                {
                    continue;
                }

                db.FeaturePricings.Add(new FeaturePricing
                {
                    FeatureKey = d.FeatureKey,
                    BillingPeriod = PricingBillingPeriod.OneTime,
                    ChargeUnit = d.Unit,
                    AmountSYP = d.AmountSyp,
                    AmountUSD = d.AmountUsd,
                    Currency = PricingCurrency.SYP_New,
                    IsActive = d.IsActive,
                    Notes = "Auto-seeded default",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"تعذر إنشاء التسعير الافتراضي: {ex.Message}");
        }
    }

    private static async Task SeedCompanyBusinessModulesAsync(ApplicationDbContext db, ILogger logger)
    {
        try
        {
            (string Key, string Title, string Description)[] modules =
            [
                (FeatureKeys.Warehouse, "المستودع", "جرد الأصناف: وارد، صادر، وتصحيح — منفصل عن المحفظة والصندوق."),
                (FeatureKeys.MoneyDiary, "دفتر الإيراد والمصروف", "تسجيل يومي لما دخل وخرج نقداً أو بنكياً."),
                (FeatureKeys.Payroll, "رواتب الموظفين", "متابعة رواتب فريق العمل شهرياً.")
            ];

            foreach ((string key, string title, string description) in modules)
            {
                bool hasInfo = await db.FeaturePublicInfos.AnyAsync(f => f.FeatureKey == key);
                if (!hasInfo)
                {
                    db.FeaturePublicInfos.Add(new FeaturePublicInfo
                    {
                        FeatureKey = key,
                        DetailHtml = $"<p><strong>{title}</strong></p><p>{description}</p>",
                        PricingPolicyHtml = "<p class=\"text-muted mb-0\">يتم تفعيل الخدمة بعد طلب اشتراك وموافقة مدير النظام وفق التسعير المعتمد.</p>",
                        RenewalPolicyHtml = "<p class=\"text-muted mb-0\">يُجدَّد الاشتراك شهرياً من محفظة الشركة وفق السعر المعتمد في كتالوج الخدمات.</p>",
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                bool hasPricing = await db.FeaturePricings.AnyAsync(p =>
                    p.FeatureKey == key && p.BillingPeriod == PricingBillingPeriod.Monthly);
                if (!hasPricing)
                {
                    db.FeaturePricings.Add(new FeaturePricing
                    {
                        FeatureKey = key,
                        BillingPeriod = PricingBillingPeriod.Monthly,
                        ChargeUnit = PricingChargeUnit.PerNetwork,
                        AmountSYP = 0m,
                        AmountUSD = 0m,
                        Currency = PricingCurrency.SYP_New,
                        IsActive = true,
                        Notes = "Company business module (auto-seeded)",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await db.SaveChangesAsync();

            List<int> companyNetworkIds = await db.Networks
                .AsNoTracking()
                .Where(n => n.ParentNetworkId == null)
                .Select(n => n.Id)
                .ToListAsync();

            DateTime now = DateTime.Now;
            foreach (int networkId in companyNetworkIds)
            {
                foreach ((string key, _, _) in modules)
                {
                    bool subscribed = await db.NetworkServiceSubscriptions.AnyAsync(s =>
                        s.NetworkId == networkId && s.FeatureKey == key);
                    if (subscribed)
                    {
                        continue;
                    }

                    db.NetworkServiceSubscriptions.Add(new NetworkServiceSubscription
                    {
                        NetworkId = networkId,
                        FeatureKey = key,
                        BillingPeriod = PricingBillingPeriod.Monthly,
                        StartAt = now,
                        ExpiresAt = BillingPeriodDateCalculator.AddPeriod(now, PricingBillingPeriod.Monthly),
                        Status = NetworkServiceSubscriptionStatus.Active,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "تعذر بذر وحدات إدارة الشركة.");
        }
    }
}
