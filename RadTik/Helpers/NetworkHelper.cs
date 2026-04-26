using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using System.Linq;

namespace RadTik.Helpers
{
    public static class NetworkHelper
    {
        /// <summary>
        /// التحقق من صلاحية الوصول إلى شبكة معينة حسب دور المستخدم.
        /// - SystemAdministrator: جميع الشبكات
        /// - NetworkAdministrator (مدير الشركة): الشبكة الرئيسية + الشبكات الفرعية التابعة لها
        /// - CompanyEmployee/EmployeeLegacy/Client/...: فقط شبكته
        /// </summary>
        public static async Task<bool> IsNetworkAccessibleAsync(
            HttpContext httpContext,
            ApplicationDbContext context,
            ApplicationUser? user,
            int networkId)
        {
            if (user == null)
            {
                return false;
            }

            // مدير النظام: يمكنه الوصول لأي شبكة
            if (httpContext.User.IsInRole(RoleNames.SystemAdministrator))
            {
                return await context.Networks.AnyAsync(n => n.Id == networkId);
            }

            // مدير الشركة: شبكته الرئيسية + الشبكات الفرعية التابعة لها
            if (httpContext.User.IsInRole(RoleNames.NetworkAdministrator))
            {
                if (user.NetworkId.HasValue && user.NetworkId.Value == networkId)
                {
                    return true;
                }

                if (!user.NetworkId.HasValue)
                {
                    return false;
                }

                return await context.Networks.AnyAsync(n => n.Id == networkId && n.ParentNetworkId == user.NetworkId.Value);
            }

            // باقي الأدوار: الشبكة المرتبطة مباشرة بالمستخدم فقط
            return user.NetworkId.HasValue && user.NetworkId.Value == networkId;
        }

        /// <summary>
        /// الحصول على معرف الشبكة المحددة حالياً من Session أو من المستخدم
        /// </summary>
        public static int? GetCurrentNetworkId(HttpContext httpContext, ApplicationDbContext context, ApplicationUser? user)
        {
            // محاولة الحصول من Session أولاً
            var sessionNetworkId = httpContext.Session.GetInt32("SelectedNetworkId");
            if (sessionNetworkId.HasValue)
            {
                // حماية: تأكد أن الشبكة في Session متاحة فعلاً لهذا المستخدم
                // ملاحظة: لا نستدعي async هنا لتجنب تغييرات كبيرة على توقيع الدالة، لذلك نستخدم Any() بشكل متزامن.
                // حجم الشبكات عادة صغير، وهذا يستخدم في كل طلب، لذا نُبقيه بسيطاً.
                if (user != null)
                {
                    // SystemAdministrator: يسمح بأي شبكة موجودة
                    if (httpContext.User.IsInRole(RoleNames.SystemAdministrator))
                    {
                        return context.Networks.Any(n => n.Id == sessionNetworkId.Value)
                            ? sessionNetworkId.Value
                            : null;
                    }

                    // NetworkAdministrator: الشبكة الرئيسية + الفرعية التابعة لها
                    if (httpContext.User.IsInRole(RoleNames.NetworkAdministrator))
                    {
                        if (user.NetworkId.HasValue && user.NetworkId.Value == sessionNetworkId.Value)
                        {
                            return sessionNetworkId.Value;
                        }

                        if (user.NetworkId.HasValue)
                        {
                            var ok = context.Networks.Any(n => n.Id == sessionNetworkId.Value && n.ParentNetworkId == user.NetworkId.Value);
                            if (ok)
                            {
                                return sessionNetworkId.Value;
                            }
                        }
                    }
                    else
                    {
                        // CompanyEmployee/EmployeeLegacy/Client/...: فقط شبكته
                        if (user.NetworkId.HasValue && user.NetworkId.Value == sessionNetworkId.Value)
                        {
                            return sessionNetworkId.Value;
                        }
                    }

                    // إذا كانت Session تحمل قيمة غير مسموحة، نفرغها ونعود لشبكة المستخدم
                    httpContext.Session.Remove("SelectedNetworkId");
                }
            }

            // إذا لم يكن في Session، استخدم شبكة المستخدم
            if (user?.NetworkId.HasValue == true)
            {
                // تعيينها في Session للاستخدام المستقبلي
                SetCurrentNetworkId(httpContext, user.NetworkId.Value);
                return user.NetworkId.Value;
            }

            return null;
        }

        /// <summary>
        /// تعيين الشبكة المحددة في Session
        /// </summary>
        public static void SetCurrentNetworkId(HttpContext httpContext, int networkId)
        {
            httpContext.Session.SetInt32("SelectedNetworkId", networkId);
        }

        /// <summary>
        /// الحصول على قائمة الشبكات المتاحة للمستخدم
        /// </summary>
        public static async Task<List<Network>> GetAvailableNetworksAsync(ApplicationDbContext context, ApplicationUser? user, UserManager<ApplicationUser>? userManager = null)
        {
            if (user == null)
            {
                return [];
            }

            if (userManager != null)
            {
                var userRoles = await userManager.GetRolesAsync(user);
                
                // إذا كان مدير نظام، يمكنه رؤية جميع الشبكات
                if (userRoles.Contains(RoleNames.SystemAdministrator))
                {
                    return await context.Networks.OrderBy(n => n.Name).ToListAsync();
                }
                
                // إذا كان مدير شركة (NetworkAdministrator)، يمكنه الوصول لشبكته الرئيسية + الشبكات الفرعية التابعة لها
                if (userRoles.Contains(RoleNames.NetworkAdministrator))
                {
                    if (!user.NetworkId.HasValue)
                    {
                        return [];
                    }

                    var mainId = user.NetworkId.Value;
                    return await context.Networks
                        .Where(n => n.Id == mainId || n.ParentNetworkId == mainId)
                        .OrderBy(n => n.ParentNetworkId.HasValue) // الرئيسية أولاً
                        .ThenBy(n => n.Name)
                        .ToListAsync();
                }

                // موظف شركة/عميل/نقطة تحصيل: شبكة واحدة فقط
                if (userRoles.Contains(RoleNames.CompanyEmployee) ||
                    userRoles.Contains(RoleNames.EmployeeLegacy) ||
                    userRoles.Contains(RoleNames.Client) ||
                    userRoles.Contains(RoleNames.CollectionPoint))
                {
                    if (!user.NetworkId.HasValue)
                    {
                        return [];
                    }

                    var network = await context.Networks.Where(n => n.Id == user.NetworkId.Value).ToListAsync();
                    return network;
                }
            }

            return [];
        }
    }
}
