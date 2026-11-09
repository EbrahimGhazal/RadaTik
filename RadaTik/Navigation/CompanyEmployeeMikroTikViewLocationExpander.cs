using Microsoft.AspNetCore.Mvc.Razor;

namespace RadaTik.Navigation;

/// <summary>
/// شاشات المخدمات للموظف تستخدم عروض المدير مع تخطيط الموظف، لأن المنطق مشترك.
/// </summary>
public sealed class CompanyEmployeeMikroTikViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (!string.Equals(context.AreaName, "CompanyEmployee", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.ControllerName, "MikroTikServers", StringComparison.OrdinalIgnoreCase))
        {
            return viewLocations;
        }

        return viewLocations.Concat(
        [
            "/Areas/CompanyAdmin/Views/{1}/{0}.cshtml",
            "/Areas/CompanyAdmin/Views/Shared/{0}.cshtml"
        ]);
    }
}
