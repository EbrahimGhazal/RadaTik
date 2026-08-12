using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Filters
{
    /// <summary>
    /// يسجل العمليات غير (GET) تلقائياً في جدول AuditLogs.
    /// </summary>
    public class AuditActionFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditActionFilter(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // نفذ الـ action أولاً
            var executed = await next();

            try
            {
                var httpContext = context.HttpContext;
                var method = httpContext.Request.Method?.ToUpperInvariant() ?? "UNKNOWN";

                // تجاهل عمليات القراءة
                if (method == "GET" || method == "HEAD" || method == "OPTIONS")
                {
                    return;
                }

                var controller = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) ? c : null;
                var action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var a) ? a : null;

                // تجاهل بعض العمليات الحساسة/الضجيج
                if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(controller, "SpaAuth", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var user = await _userManager.GetUserAsync(httpContext.User);
                var userId = user?.Id;
                var userName = user?.UserName ?? httpContext.User.Identity?.Name;

                var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();
                var rolesStr = roles.Count > 0 ? string.Join(",", roles) : null;

                int? statusCode = executed.HttpContext.Response?.StatusCode;

                int? networkId = null;
                if (user != null)
                {
                    networkId = NetworkHelper.GetCurrentNetworkId(httpContext, _db, user);
                }

                // محاولة استخراج entity id من route أو من action args
                string? entityId = null;
                if (context.RouteData.Values.TryGetValue("id", out var routeId) && routeId != null)
                {
                    entityId = routeId.ToString();
                }
                else
                {
                    // شائع في Forms
                    if (context.ActionArguments.TryGetValue("id", out var argId) && argId != null)
                    {
                        entityId = argId.ToString();
                    }
                    else if (context.ActionArguments.TryGetValue("clientId", out var clientId) && clientId != null)
                    {
                        entityId = clientId.ToString();
                    }
                }

                var path = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value : null;

                var log = new AuditLog
                {
                    CreatedAt = DateTime.Now,
                    UserId = userId,
                    UserName = userName,
                    Roles = rolesStr,
                    HttpMethod = method,
                    Controller = controller,
                    Action = action,
                    Path = path,
                    StatusCode = statusCode,
                    NetworkId = networkId,
                    EntityType = controller, // كقيمة افتراضية
                    EntityId = entityId,
                    Summary = $"{method} {controller}/{action}"
                };

                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // لا نسمح للتدقيق أن يكسر الطلب
            }
        }
    }
}

