using Microsoft.AspNetCore.Mvc;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Clients;

namespace RadaTik.Controllers
{
    public partial class ClientsController : Controller
    {
        /// <summary>
        /// تشخيص سبب العطل لدى المشترك (عزل بالنطاق + سلسلة Ping عند توفر السيرفر).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiagnoseFaultJson(int id, CancellationToken cancellationToken)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, status = "Unauthorized", message = "يجب تسجيل الدخول." });
            }

            IReadOnlyList<string> roles = (await _userManager.GetRolesAsync(user)).ToList();
            bool isEmployee = roles.Contains(RoleNames.CompanyEmployee) || roles.Contains(RoleNames.EmployeeLegacy);
            bool isClientOnly = roles.Contains(RoleNames.Client) && !isEmployee &&
                                !roles.Contains(RoleNames.NetworkAdministrator);
            if (isClientOnly)
            {
                return StatusCode(403, new { success = false, status = "Forbidden", message = "التشخيص متاح لفريق الشبكة فقط." });
            }

            if (!await _app.Permission.HasPermissionAsync(User, "Clients.View"))
            {
                return StatusCode(403, new { success = false, status = "Forbidden", message = "غير مصرح بعرض المشتركين." });
            }

            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                return BadRequest(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً" });
            }

            try
            {
                SubscriberFaultDiagnosisDto dto = await _app.FaultDiagnosis.DiagnoseAsync(
                    id,
                    networkId.Value,
                    cancellationToken);
                if (!dto.Success)
                {
                    return dto.Status switch
                    {
                        "NotFound" => NotFound(dto),
                        "Forbidden" => StatusCode(403, dto),
                        _ => BadRequest(dto)
                    };
                }

                return Json(dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "فشل تشخيص عطل المشترك {ClientId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    status = "Error",
                    message = "تعذر إكمال التشخيص. حاول مرة أخرى."
                });
            }
        }
    }
}
