using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Security;

namespace RadaTik.Controllers
{
    /// <summary>
    /// مسار قديم للتوافق: /CollectionPoints/*
    /// تم نقل المنطق إلى Area: CompanyAdmin ضمن /networkManager/CollectionPoints/*
    /// </summary>
    [Authorize(Roles = RoleNames.NetworkAdministrator)]
    [Route("CollectionPoints")]
    [Route("CollectionPoints/{*path}")]
    public class CollectionPointsController : Controller
    {
        [HttpGet, HttpPost, HttpPut, HttpDelete]
        public IActionResult Forward(string? path = null)
        {
            var suffix = string.IsNullOrEmpty(path) ? "" : "/" + path;
            var target = "/networkManager/CollectionPoints" + suffix + Request.QueryString;
            return RedirectPreserveMethod(target);
        }
    }
}

