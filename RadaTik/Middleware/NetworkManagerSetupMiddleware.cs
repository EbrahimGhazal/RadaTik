using Microsoft.AspNetCore.Identity;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;

namespace RadaTik.Middleware;

/// <summary>
/// يفرض على مدير الشركة: تغيير كلمة المرور، إنشاء الشبكة الأولى، ثم تمويل المحفظة بالحد الأدنى.
/// </summary>
public sealed class NetworkManagerSetupMiddleware(
    RequestDelegate next,
    ILogger<NetworkManagerSetupMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        ICompanyWalletOnboardingFundingService fundingService)
    {
        if (context.User?.Identity?.IsAuthenticated != true ||
            !context.User.IsInRole(RoleNames.NetworkAdministrator))
        {
            await next(context);
            return;
        }

        // مدير النظام الذي يحمل الدور أيضاً لا يُقيَّد بمسار شركة.
        if (context.User.IsInRole(RoleNames.SystemAdministrator))
        {
            await next(context);
            return;
        }

        string path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/networkManager", StringComparison.OrdinalIgnoreCase)
            && !NetworkManagerSetupAccess.IsAccountPath(path))
        {
            await next(context);
            return;
        }

        ApplicationUser? user = await userManager.GetUserAsync(context.User);
        if (user == null)
        {
            await next(context);
            return;
        }

        if (user.MustChangePassword)
        {
            if (NetworkManagerSetupAccess.IsAllowedDuringMandatoryPasswordChange(path))
            {
                await next(context);
                return;
            }

            logger.LogInformation(
                "Redirecting network manager {UserName} to required password change (blocked path: {Path}).",
                user.UserName,
                path);
            context.Response.Redirect("/networkManager/setup/requiredPassword");
            return;
        }

        if (!user.NetworkId.HasValue)
        {
            if (NetworkManagerSetupAccess.IsAllowedBeforeMainNetwork(path))
            {
                await next(context);
                return;
            }

            if (path.StartsWith("/networkManager", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Redirecting network manager {UserName} to create main network (blocked path: {Path}).",
                    user.UserName,
                    path);
                context.Response.Redirect("/networkManager/Network/Create");
                return;
            }

            await next(context);
            return;
        }

        int companyNetworkId = user.NetworkId.Value;
        CompanyWalletOnboardingFundingStatus funding;
        try
        {
            // لا نربط بـ RequestAborted: فتح اتصال SQL قد يُلغى عند إيقاف التطبيق أو مغادرة الصفحة
            // فيظهر TaskCanceledException داخل middleware قبل اكتمال التوجيه.
            funding = await fundingService.EvaluateAsync(companyNetworkId, CancellationToken.None);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        if (!funding.RequiresFundingGate)
        {
            await next(context);
            return;
        }

        if (NetworkManagerSetupAccess.IsAllowedDuringMandatoryWalletFunding(path))
        {
            await next(context);
            return;
        }

        if (path.StartsWith("/networkManager", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Redirecting network manager {UserName} to mandatory wallet top-up (blocked path: {Path}, min={Min}).",
                user.UserName,
                path,
                funding.MinimumRequiredSyp);
            context.Response.Redirect("/networkManager/wallet/topup");
            return;
        }

        await next(context);
    }
}
