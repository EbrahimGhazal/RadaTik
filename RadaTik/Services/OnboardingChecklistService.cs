using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Maintenance;
using RadaTik.Security;
using RadaTik.Models;
using RadaTik.ViewModels.Onboarding;

namespace RadaTik.Services;

public interface IOnboardingChecklistService
{
    Task<OnboardingChecklistViewModel?> GetCompanyChecklistAsync(
        string userId,
        int networkId,
        CancellationToken cancellationToken = default);

    Task<OnboardingChecklistViewModel?> GetSystemChecklistAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> CanDismissSystemAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> DismissCompanyAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> DismissSystemAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class OnboardingChecklistService(
    ApplicationDbContext context,
    ISystemAdminPricingReadinessService pricingReadiness) : IOnboardingChecklistService
{
    private readonly ApplicationDbContext _context = context;
    private readonly ISystemAdminPricingReadinessService _pricingReadiness = pricingReadiness;

    public async Task<OnboardingChecklistViewModel?> GetCompanyChecklistAsync(
        string userId,
        int networkId,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return null;
        }

        bool hasServer = await _context.MikroTikServers
            .AsNoTracking()
            .AnyAsync(s => s.NetworkId == networkId, cancellationToken);
        bool hasProfile = await _context.Profiles
            .AsNoTracking()
            .AnyAsync(p => p.NetworkId == networkId && p.IsActive, cancellationToken);
        bool hasClient = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.NetworkId == networkId, cancellationToken);
        bool hasSector = await _context.Sectors
            .AsNoTracking()
            .AnyAsync(s => s.NetworkId == networkId, cancellationToken);
        bool hasReceiver = await _context.Receivers
            .AsNoTracking()
            .AnyAsync(r => r.NetworkId == networkId, cancellationToken);

        Network? network = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId, cancellationToken);
        int companyNetworkId = network?.ParentNetworkId ?? networkId;

        bool hasExtraFeature = await _context.NetworkFeatures
            .AsNoTracking()
            .AnyAsync(
                f => f.NetworkId == companyNetworkId
                     && f.IsEnabled
                     && (f.Key == FeatureKeys.Warehouse
                         || f.Key == FeatureKeys.MoneyDiary
                         || f.Key == FeatureKeys.Payroll),
                cancellationToken);

        bool hasMaintenancePricing = await _context.NetworkMaintenancePrices
            .AsNoTracking()
            .AnyAsync(
                p => p.NetworkId == companyNetworkId
                     && p.IsActive
                     && MaintenanceSolutionTypes.Values.Contains(p.MaintenanceType),
                cancellationToken);

        List<OnboardingChecklistItem> items =
        [
            new()
            {
                Key = "network",
                Title = "إنشاء الشبكة",
                Description = "بيانات الشركة والشبكة الأساسية.",
                ActionUrl = "/networkManager/Network",
                ActionLabel = "إدارة الشبكة",
                IsCompleted = true,
                IsRequired = true
            },
            new()
            {
                Key = "mikrotik",
                Title = "ربط خادم MikroTik",
                Description = "مطلوب لمزامنة العملاء والباقات.",
                ActionUrl = "/networkManager/MikroTikServers/Create",
                ActionLabel = "إضافة خادم",
                IsCompleted = hasServer,
                IsRequired = true
            },
            new()
            {
                Key = "profile",
                Title = "إنشاء باقة سرعة (Profile)",
                Description = "تُستخدم عند إضافة المشتركين.",
                ActionUrl = "/networkManager/Profile/Create",
                ActionLabel = "إنشاء باقة",
                IsCompleted = hasProfile,
                IsRequired = true
            },
            new()
            {
                Key = "maintenance-pricing",
                Title = "تسعير الصيانة",
                Description = "أسعار طرق الحل (زيارة، قطع، نقل…) لفواتير طلبات الصيانة.",
                ActionUrl = "/networkManager/MaintenancePricing",
                ActionLabel = "ضبط التسعير",
                IsCompleted = hasMaintenancePricing,
                IsRequired = false
            },
            new()
            {
                Key = "coverage",
                Title = "قطاع أو مستقبل (اختياري)",
                Description = "لتنظيم التغطية والخرائط (موصى به).",
                ActionUrl = hasSector ? "/networkManager/Receiver/Create" : "/networkManager/Sector/Create",
                ActionLabel = hasSector ? "إضافة مستقبل" : "إضافة قطاع",
                IsCompleted = hasSector || hasReceiver,
                IsRequired = false
            },
            new()
            {
                Key = "client",
                Title = "إضافة أول مشترك (اختياري)",
                Description = "تجربة عملية لاختبار الإعداد.",
                ActionUrl = "/networkManager/Clients/Create",
                ActionLabel = "إضافة مشترك",
                IsCompleted = hasClient,
                IsRequired = false
            },
            new()
            {
                Key = "features",
                Title = "تفعيل الخدمات الإضافية (اختياري)",
                Description = "مستودع، دفتر إيراد، رواتب… حسب حاجتك.",
                ActionUrl = "/networkManager/features",
                ActionLabel = "مركز الخدمات",
                IsCompleted = hasExtraFeature,
                IsRequired = false
            }
        ];

        return BuildViewModel(
            user.OnboardingCompanyDismissedAt.HasValue,
            "/networkManager/onboarding/dismiss",
            "ابدأ هنا — تهيئة شبكتك",
            "أكمل الخطوات الأساسية لتشغيل RadaTik بسرعة. يمكنك تخطي البطاقة لاحقاً.",
            items);
    }

    public async Task<OnboardingChecklistViewModel?> GetSystemChecklistAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return null;
        }

        int pendingManagers = await _context.JoinRequests
            .AsNoTracking()
            .CountAsync(
                j => j.RequestType == JoinRequestType.NetworkAdministrator
                     && (j.Status == JoinRequestStatus.Pending || j.Status == JoinRequestStatus.UnderReview),
                cancellationToken);

        bool hasCompany = await _context.Networks
            .AsNoTracking()
            .AnyAsync(n => n.ParentNetworkId == null, cancellationToken);

        bool hasPaymentMethod = await _context.PaymentMethods
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        SystemAdminPricingReadiness pricingStatus = await _pricingReadiness.EvaluateAsync(cancellationToken);
        bool passwordOk = !user.MustChangePassword;

        List<OnboardingChecklistItem> items =
        [
            new()
            {
                Key = "password",
                Title = "تعيين كلمة مرور قوية",
                Description = "إلزامي عند أول دخول لحماية حساب مدير النظام.",
                ActionUrl = "/systemAdmin/setup/requiredPassword",
                ActionLabel = "تعيين كلمة المرور",
                IsCompleted = passwordOk,
                IsRequired = true
            },
            new()
            {
                Key = "pricing",
                Title = "تهيئة أسعار الخدمات",
                Description = pricingStatus.IsComplete
                    ? "جميع أسعار الإنشاء والتجديد مضبوطة."
                    : $"متبقي {pricingStatus.MissingItems.Count} عنصر/عناصر.",
                ActionUrl = "/systemAdmin/setup/pricing",
                ActionLabel = "متابعة التهيئة",
                IsCompleted = pricingStatus.IsComplete,
                IsRequired = true
            },
            new()
            {
                Key = "payment-methods",
                Title = "طرق الدفع",
                Description = "لشحن المحافظ وطلبات التمويل.",
                ActionUrl = "/systemAdmin/paymentMethods",
                ActionLabel = "إعداد الدفع",
                IsCompleted = hasPaymentMethod,
                IsRequired = true
            },
            new()
            {
                Key = "join-requests",
                Title = "مراجعة طلبات مديري الشركات",
                Description = pendingManagers > 0
                    ? $"لديك {pendingManagers} طلب/طلبات بانتظار المراجعة."
                    : "لا توجد طلبات معلقة حالياً.",
                ActionUrl = "/systemAdmin/JoinRequests",
                ActionLabel = "فتح الطلبات",
                IsCompleted = pendingManagers == 0,
                IsRequired = false
            },
            new()
            {
                Key = "companies",
                Title = "شركة/شبكة مسجّلة في النظام",
                Description = "تظهر بعد قبول أول مدير شركة.",
                ActionUrl = "/systemAdmin/Network",
                ActionLabel = "عرض الشبكات",
                IsCompleted = hasCompany,
                IsRequired = false
            }
        ];

        return BuildViewModel(
            user.OnboardingSystemDismissedAt.HasValue,
            "/systemAdmin/onboarding/dismiss",
            "ابدأ هنا — تشغيل المنصة",
            "أكمل كلمة المرور وأسعار الخدمات قبل قبول طلبات الشركات.",
            items);
    }

    public async Task<bool> CanDismissSystemAsync(string userId, CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (user.MustChangePassword)
        {
            return false;
        }

        SystemAdminPricingReadiness pricing = await _pricingReadiness.EvaluateAsync(cancellationToken);
        return pricing.IsComplete;
    }

    public async Task<bool> DismissCompanyAsync(string userId, CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.OnboardingCompanyDismissedAt = DateTime.UtcNow;
        user.LastUpdated = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DismissSystemAsync(string userId, CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.OnboardingSystemDismissedAt = DateTime.UtcNow;
        user.LastUpdated = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static OnboardingChecklistViewModel BuildViewModel(
        bool isDismissed,
        string dismissUrl,
        string title,
        string subtitle,
        IReadOnlyList<OnboardingChecklistItem> items)
    {
        int totalRequired = items.Count(i => i.IsRequired);
        int completedRequired = items.Count(i => i.IsRequired && i.IsCompleted);
        int progressPercent = totalRequired == 0
            ? 100
            : (int)Math.Round(completedRequired * 100.0 / totalRequired);

        return new OnboardingChecklistViewModel
        {
            Title = title,
            Subtitle = subtitle,
            Items = items,
            IsDismissed = isDismissed,
            DismissUrl = dismissUrl,
            CompletedRequired = completedRequired,
            TotalRequired = totalRequired,
            ProgressPercent = progressPercent
        };
    }
}
