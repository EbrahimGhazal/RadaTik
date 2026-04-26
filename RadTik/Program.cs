using System.Data;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.Logging;
using RadTik.Data;
using RadTik.Filters;
using RadTik.Middleware;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.Services.SystemAdminPricing;
using RadTik.Services.PricingPolicies;
using RadTik.Services.MikroTikSync;
using RadTik.Services.SectorRadio;
using RadTik.Services.Traffic;
using RadTik.Hubs;
using RadTik.Routing;

namespace RadTik
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Plain HTTP (e.g. Docker without TLS termination): cookies and redirects must not require HTTPS.
            var insecureHttp = builder.Configuration.GetValue<bool>("RadTik:InsecureHttp")
                || string.Equals(
                    Environment.GetEnvironmentVariable("RADTIK_INSECURE_HTTP"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);
            var cookieSecure = insecureHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

            // Add services to the container.
            builder.Services.AddScoped<AuditActionFilter>();
            var mvcBuilder = builder.Services.AddControllersWithViews(options =>
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
                // Password settings
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

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

            builder.Services.RegisterHostedServices();

            // MikroTik interface traffic (SignalR: NetworkAdministrator + Client portal)
            builder.Services.RegisterTrafficMonitoringServices(builder.Environment);

            // إضافة خدمة التسجيل
            builder.Services.AddLogging();


            var app = builder.Build();

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

            // واجهة React (radtik-web): الملفات تحت wwwroot/app، المسار العام /app/...
            // يجب تقديم index.html لمسارات الـ SPA قبل MVC حتى لا يُفسَّر /app/login كـ {controller}/{action}.
            app.Use(async (context, next) =>
            {
                if (!IsSpaDocumentRequest(context.Request.Path))
                {
                    await next(context);
                    return;
                }

                var indexPath = Path.Combine(app.Environment.WebRootPath ?? "", "app", "index.html");
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
            app.UseMiddleware<LegacyRootControllerRedirectMiddleware>();
            app.UseMiddleware<AreaIsolationMiddleware>();
            app.UseAuthorization();

            app.MapRadTikRoutes();

            // تطبيق الهجرات المعلقة (مثل عمود Address) قبل أي استخدام لقاعدة البيانات
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogCritical(ex, "فشل تطبيق الهجرات. سيتم إيقاف التطبيق لتجنب تشغيله على schema غير متوافقة.");
                    throw;
                }
            }

            // إنشاء الأدوار الافتراضية والبذور الأساسية (بدون إنشاء مدير نظام تلقائي).
            ProgramSeedingTasks.CreateDefaultRolesAndSeedData(app.Services).GetAwaiter().GetResult();

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
            var path = requestPath.Value ?? string.Empty;
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

            var rest = path[5..];
            if (string.IsNullOrEmpty(rest))
            {
                return true;
            }

            var segment = rest.Split('?', 2)[0];
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