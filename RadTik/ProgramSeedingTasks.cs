using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik;

internal static class ProgramSeedingTasks
{
    /// <summary>
    /// بيانات مدير النظام الافتراضي عند أول تشغيل (يُنشأ فقط إن لم يوجد مستخدم باسم admin).
    /// يُنصح بتغيير كلمة المرور فوراً في الإنتاج.
    /// </summary>
    private const string DefaultSystemAdminUserName = "Admin";
    private const string DefaultSystemAdminPassword = "admin@123";
    private const string DefaultSystemAdminEmail = "ebrahimGhazal@gmail.com";

    public static async Task CreateDefaultRolesAndSeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await EnsureAspNetRolesTableReadyAsync(db, logger))
        {
            logger.LogCritical(
                "تعذر التأكد من جداول Identity (AspNetRoles). لن تُنشأ الأدوار تلقائياً. نفّذ: dotnet ef database update");
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

            foreach (var roleName in roleNames)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createResult.Succeeded)
                {
                    logger.LogWarning(
                        "تعذر إنشاء الدور {RoleName}: {Errors}",
                        roleName,
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));
                }
            }

            await SeedDefaultPermissions(db);
            await SeedDefaultPaymentMethods(db);
            await SeedDefaultSystemFeaturePricing(db);

            await EnsureDefaultSystemAdministratorIfMissingAsync(userManager, roleManager, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "فشل إنشاء الأدوار أو البذور الأساسية بعد التحقق من الجداول.");
        }
    }

    private static async Task EnsureDefaultSystemAdministratorIfMissingAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        var existing = await userManager.FindByNameAsync(DefaultSystemAdminUserName)
            ?? await userManager.FindByEmailAsync(DefaultSystemAdminEmail);
        if (existing != null)
        {
            var needsUpdate = false;
            if (!string.Equals(existing.UserName, DefaultSystemAdminUserName, StringComparison.Ordinal))
            {
                existing.UserName = DefaultSystemAdminUserName;
                existing.NormalizedUserName = userManager.NormalizeName(DefaultSystemAdminUserName);
                needsUpdate = true;
            }

            if (!string.Equals(existing.Email, DefaultSystemAdminEmail, StringComparison.OrdinalIgnoreCase))
            {
                existing.Email = DefaultSystemAdminEmail;
                existing.NormalizedEmail = userManager.NormalizeEmail(DefaultSystemAdminEmail);
                existing.EmailConfirmed = true;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var updateResult = await userManager.UpdateAsync(existing);
                if (!updateResult.Succeeded)
                {
                    logger.LogWarning(
                        "تعذر تحديث بيانات حساب مدير النظام الافتراضي ({UserName}): {Errors}",
                        existing.UserName,
                        string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(existing, RoleNames.SystemAdministrator))
            {
                var addRole = await userManager.AddToRoleAsync(existing, RoleNames.SystemAdministrator);
                if (!addRole.Succeeded)
                {
                    logger.LogWarning(
                        "تعذر إضافة دور {Role} للمستخدم {UserName}: {Errors}",
                        RoleNames.SystemAdministrator,
                        existing.UserName,
                        string.Join("; ", addRole.Errors.Select(e => e.Description)));
                }
                else
                {
                    logger.LogInformation(
                        "تم منح دور مدير النظام للمستخدم الموجود {UserName}.",
                        existing.UserName);
                }
            }

            return;
        }

        if (!await roleManager.RoleExistsAsync(RoleNames.SystemAdministrator))
        {
            await roleManager.CreateAsync(new IdentityRole(RoleNames.SystemAdministrator));
        }

        var user = new ApplicationUser
        {
            UserName = DefaultSystemAdminUserName,
            Email = DefaultSystemAdminEmail,
            FullName = "مدير النظام",
            EmailConfirmed = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, DefaultSystemAdminPassword);
        if (!createResult.Succeeded)
        {
            logger.LogWarning(
                "تعذر إنشاء حساب مدير النظام الافتراضي ({UserName}): {Errors}",
                DefaultSystemAdminUserName,
                string.Join("; ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, RoleNames.SystemAdministrator);
        if (!roleResult.Succeeded)
        {
            logger.LogWarning(
                "تم إنشاء المستخدم {UserName} لكن تعذر ربطه بدور مدير النظام: {Errors}",
                user.UserName,
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation(
            "تم إنشاء حساب مدير النظام الافتراضي (اسم المستخدم: {UserName}).",
            DefaultSystemAdminUserName);
    }

    private static async Task<bool> EnsureAspNetRolesTableReadyAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await AspNetRolesTableExistsAsync(db))
        {
            return true;
        }

        logger.LogWarning("جدول AspNetRoles غير موجود. إعادة محاولة تطبيق الهجرات...");
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "فشل تطبيق الهجرات عند محاولة إنشاء جداول Identity.");
            return false;
        }

        if (await AspNetRolesTableExistsAsync(db))
        {
            return true;
        }

        logger.LogCritical(
            "AspNetRoles ما زال غير موجوداً بعد الهجرات. تحقق من سلسلة الهجرات وقاعدة البيانات المحددة في الاتصال.");
        return false;
    }

    private static async Task<bool> AspNetRolesTableExistsAsync(ApplicationDbContext db)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
            {
                await conn.OpenAsync();
            }

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'AspNetRoles'
                    """;
                var scalar = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(scalar) > 0;
            }
            finally
            {
                if (!wasOpen)
                {
                    await conn.CloseAsync();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static async Task SeedDefaultPermissions(ApplicationDbContext db)
    {
        try
        {
            var defaults = new List<(string Key, string DisplayName, string Category)>
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
            };

            var existingKeys = await db.Permissions.Select(p => p.Key).ToListAsync();
            var existingSet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = defaults
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
            var defaults = new List<(string Name, int Order, bool IsCash)>
            {
                ("كاش", 0, true),
                ("نقدي", 1, true),
                ("دفع مباشر", 2, false),
                ("شام كاش", 3, false),
                ("بنك", 4, false),
                ("بنك بيمو", 5, false),
                ("بنك البركة", 6, false)
            };

            var existingNames = await db.PaymentMethods.Select(m => m.Name).ToListAsync();
            var existingSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = defaults
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
            var defaults = new List<(string FeatureKey, PricingChargeUnit Unit, decimal AmountSyp, decimal AmountUsd, bool IsActive)>
            {
                (FeatureKeys.CollectionCommission, PricingChargeUnit.PercentOfCollectedAmount, 0m, 0m, true),
                (FeatureKeys.MaintenanceTransportFee, PricingChargeUnit.Flat, 0m, 0m, true),
                (FeatureKeys.MaintenanceCommission, PricingChargeUnit.Flat, 0m, 0m, true)
            };

            foreach (var d in defaults)
            {
                var exists = await db.FeaturePricings.AnyAsync(p =>
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
}
