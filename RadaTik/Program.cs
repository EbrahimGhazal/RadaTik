using System.Data;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Filters;
using RadaTik.Middleware;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.SystemAdminPricing;
using RadaTik.Services.PricingPolicies;
using RadaTik.Services.MikroTikSync;
using RadaTik.Services.SectorRadio;
using RadaTik.Services.Traffic;
using RadaTik.Hubs;
using RadaTik.Routing;

namespace RadaTik
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (ProgramCommandTasks.IsPrintBootstrapIdentityCommand(args))
            {
                ProgramCommandTasks.PrintBootstrapIdentityValues();
                return;
            }

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Plain HTTP (e.g. Docker without TLS termination): cookies and redirects must not require HTTPS.
            bool insecureHttp = builder.Configuration.GetValue<bool>("RadaTik:InsecureHttp")
                || string.Equals(
                    Environment.GetEnvironmentVariable("RADATIK_INSECURE_HTTP"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);
            bool isTestMode = builder.Environment.IsEnvironment("Testing")
                || string.Equals(Environment.GetEnvironmentVariable("RADATIK_TEST_MODE"), "true", StringComparison.OrdinalIgnoreCase);
            bool disableHostedServices = isTestMode
                || builder.Configuration.GetValue<bool>("RadaTik:DisableHostedServices")
                || string.Equals(Environment.GetEnvironmentVariable("RADATIK_DISABLE_HOSTED_SERVICES"), "true", StringComparison.OrdinalIgnoreCase);
            bool skipStartupDataInit = isTestMode
                || builder.Configuration.GetValue<bool>("RadaTik:SkipStartupDataInit")
                || string.Equals(Environment.GetEnvironmentVariable("RADATIK_SKIP_STARTUP_DATA_INIT"), "true", StringComparison.OrdinalIgnoreCase);
            CookieSecurePolicy cookieSecure = insecureHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

            DirectoryInfo dataProtectionKeys = RadaTikDataProtection.EnsureKeysDirectory();
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(dataProtectionKeys)
                .SetApplicationName("RadaTik");

            // Add services to the container.
            builder.Services.AddScoped<AuditActionFilter>();
            IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews(options =>
            {
                // تدقيق تلقائي لكل العمليات غير GET
                options.Filters.Add<AuditActionFilter>();
            });

            // في التطوير: تحميل الـ .cshtml من القرص حتى لا تعتمد على مجموعات العرض المجمّعة فقط (يُصلح "view not found" عند تعارض البناء).
            if (builder.Environment.IsDevelopment())
            {
                mvcBuilder.AddRazorRuntimeCompilation();
            }

            // Razor Views / Areas configuration
            // لا نضيف مسارات المناطق إلى ViewLocationFormats العامة، حتى لا يختار المحرك عرضاً
            // من منطقة (مثل SystemAdmin/Views/Account/Profile) عند استدعاء Controller من الجذر
            // (مثل Account/Profile) فيحدث تعارض نوع النموذج (ProfileViewModel vs SystemAdminProfileViewModel).
            // عرض كل منطقة يُختار تلقائياً عبر AreaViewLocationFormats عندما يكون الطلب ضمن تلك المنطقة.
            builder.Services.Configure<RazorViewEngineOptions>(_ => { });

            // إضافة Session
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = cookieSecure;
            });

            builder.Services.AddMemoryCache();

            // Configure DbContext with SQL Server + مزامنة MikroTik التلقائية
            builder.Services.AddSingleton<IMikroTikSyncQueue, MikroTikSyncQueue>();
            builder.Services.AddScoped<MikroTikSaveChangesInterceptor>();
            builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("MyDBConnection"));
                options.AddInterceptors(sp.GetRequiredService<MikroTikSaveChangesInterceptor>());
            });

            // Configure Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // القواعد الافتراضية للـ Identity: مشتركون (عبر ClientId). الحسابات الأخرى تُراجع في StrongPasswordValidator.
                ClientPasswordRules.ConfigureIdentityOptions(options.Password);

                // User settings
                options.User.RequireUniqueEmail = true;

                // SignIn settings
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedAccount = false;

                // Lockout settings (mitigate brute-force attempts)
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            builder.Services.AddScoped<IPasswordValidator<ApplicationUser>, StrongPasswordValidator>();

            // Cookie settings
            builder.Services.ConfigureApplicationCookie(options =>
            {
                // Friendly URLs (new structure)
                // المطلوب: إظهار Account بدل regLog في عنوان الصفحة
                options.LoginPath = "/Account/login";
                options.LogoutPath = "/Account/logout";
                options.AccessDeniedPath = "/Account/accessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = cookieSecure;
            });

            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = cookieSecure;
            });

            builder.Services.RegisterApplicationServices();

            // خدمة التسعيرات (تُطبَّق على واجهات مدراء الشركات)
            builder.Services.RegisterPricingServices();

            builder.Services.RegisterSectorRadioServices();

            // Ensure authorization services are registered (policies/handlers)
            builder.Services.AddAuthorization();
            builder.Services.AddHealthChecks();

            builder.Services.RegisterHostedServices(includeHostedServices: !disableHostedServices);

            // MikroTik interface traffic (SignalR: NetworkAdministrator + Client portal)
            builder.Services.RegisterTrafficMonitoringServices(builder.Environment);

            // إضافة خدمة التسجيل
            builder.Services.AddLogging();


            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                if (!insecureHttp)
                {
                    app.UseHsts();
                }
            }

            app.UseMiddleware<SecurityHeadersMiddleware>();
            if (!insecureHttp)
            {
                app.UseHttpsRedirection();
            }
            app.UseStaticFiles();

            // واجهة React (radatik-web): الملفات تحت wwwroot/app، المسار العام /app/...
            // يجب تقديم index.html لمسارات الـ SPA قبل MVC حتى لا يُفسَّر /app/login كـ {controller}/{action}.
            app.Use(async (context, next) =>
            {
                if (!IsSpaDocumentRequest(context.Request.Path))
                {
                    await next(context);
                    return;
                }

                string indexPath = Path.Combine(app.Environment.WebRootPath ?? "", "app", "index.html");
                if (!System.IO.File.Exists(indexPath))
                {
                    await next(context);
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(indexPath);
            });

            app.UseRouting();
            app.UseSession(); // إضافة Session middleware
            app.UseAuthentication();
            app.UseMiddleware<NetworkTenantMiddleware>();
            app.UseMiddleware<LegacyRootControllerRedirectMiddleware>();
            app.UseMiddleware<AreaIsolationMiddleware>();
            app.UseAuthorization();
            app.UseMiddleware<SystemAdminSetupMiddleware>();
            app.UseMiddleware<NetworkManagerSetupMiddleware>();
            app.UseMiddleware<ClientPortalSetupMiddleware>();

            app.MapRadaTikRoutes();

            // تطبيق الهجرات المعلقة (مثل عمود Address) قبل أي استخدام لقاعدة البيانات
            if (!skipStartupDataInit)
            {
                using IServiceScope scope = app.Services.CreateScope();
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    db.Database.Migrate();
                    ILogger<Program> migrateLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    SchemaColumnRepair.EnsurePendingColumnsAsync(db, migrateLogger).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogCritical(ex, "فشل تطبيق الهجرات. سيتم إيقاف التطبيق لتجنب تشغيله على schema غير متوافقة.");
                    throw;
                }
            }

            // إنشاء الأدوار، حساب مدير النظام الافتراضي (admin)، والبذور الأساسية.
            if (!skipStartupDataInit)
            {
                ProgramSeedingTasks.CreateDefaultRolesAndSeedData(app.Services).GetAwaiter().GetResult();
            }

            if (ProgramCommandTasks.IsEnsureDefaultAdminSqlCommand(args))
            {
                bool reset = args.Any(a => string.Equals(a, "--reset-password", StringComparison.OrdinalIgnoreCase));
                ProgramCommandTasks.EnsureDefaultAdminViaSqlAsync(app.Services, reset).GetAwaiter().GetResult();
                return;
            }

            if (ProgramCommandTasks.IsBootstrapAdminCommand(args))
            {
                ProgramCommandTasks.BootstrapSystemAdministratorAsync(app.Services, args).GetAwaiter().GetResult();
                return;
            }

            if (ProgramCommandTasks.IsReencryptSensitiveFieldsCommand(args))
            {
                ProgramCommandTasks.ReencryptSensitiveFieldsAsync(app.Services).GetAwaiter().GetResult();
                return;
            }

            app.MapHealthChecks("/health");
            app.Run();
        }

        /// <summary>
        /// طلبات وثيقة SPA تحت /app (مثل /app/login) بدون امتداد ملف حقيقي — يُخدمها ASP.NET مباشرة ولا تمر إلى MVC.
        /// </summary>
        private static bool IsSpaDocumentRequest(PathString requestPath)
        {
            string path = requestPath.Value ?? string.Empty;
            if (path.Length == 0)
            {
                return false;
            }

            if (!path.StartsWith("/app", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // لا نطابق /apple أو غيره
            if (path.Length > 4 && path[4] != '/' && path[4] != '?')
            {
                return false;
            }

            if (path.Length == 4)
            {
                return true;
            }

            if (path[4] == '?')
            {
                return true;
            }

            string rest = path[5..];
            if (string.IsNullOrEmpty(rest))
            {
                return true;
            }

            string segment = rest.Split('?', 2)[0];
            if (segment.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (segment.StartsWith("mock/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (segment.Contains('.', StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

    }
}
