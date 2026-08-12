using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RadaTik.Services;

namespace RadaTik.Helpers
{
    /// <summary>
    /// Attribute لفرض صلاحية معينة على Action/Controller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RequirePermissionAttribute : TypeFilterAttribute
    {
        public RequirePermissionAttribute(string permissionKey) : base(typeof(RequirePermissionFilter))
        {
            Arguments = new object[] { permissionKey };
        }
    }

    public sealed class RequirePermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly IPermissionService _permissionService;
        private readonly string _permissionKey;

        public RequirePermissionFilter(IPermissionService permissionService, string permissionKey)
        {
            _permissionService = permissionService;
            _permissionKey = permissionKey;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            bool allowed = await _permissionService.HasPermissionAsync(context.HttpContext.User, _permissionKey);
            if (!allowed)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}

