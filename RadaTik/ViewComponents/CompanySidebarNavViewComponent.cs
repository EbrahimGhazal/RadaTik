using Microsoft.AspNetCore.Mvc;
using RadaTik.Navigation;

namespace RadaTik.ViewComponents;

public sealed class CompanySidebarNavViewComponent : ViewComponent
{
    private readonly ICompanySidebarNavBuilder _builder;

    public CompanySidebarNavViewComponent(ICompanySidebarNavBuilder builder)
    {
        _builder = builder;
    }

    public async Task<IViewComponentResult> InvokeAsync(CompanySidebarRole role)
    {
        SidebarNavViewModel vm = await _builder.BuildAsync(
            HttpContext.User,
            HttpContext,
            role,
            HttpContext.RequestAborted);
        return View(vm);
    }
}
