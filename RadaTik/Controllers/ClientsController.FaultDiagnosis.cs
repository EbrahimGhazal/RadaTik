using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Domain.FaultDiagnosis;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Clients;

namespace RadaTik.Controllers
{
    public partial class ClientsController : Controller
    {
        /// <summary>
        /// تشخيص سبب العطل لدى المشترك (عزل بالنطاق + سلسلة Ping + أسئلة LED عند توفرها).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiagnoseFaultJson(
            int id,
            string? routerPowerOn,
            string? internetLedOn,
            string? wanLedOn,
            string? neighborsOnSwitchDown,
            CancellationToken cancellationToken)
        {
            IActionResult? denied = await DenyClientOnlyOrMissingClientViewAsync();
            if (denied != null)
            {
                return denied;
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                return BadRequest(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً" });
            }

            try
            {
                SubscriberFaultLedAnswers led = SubscriberFaultLedAnswersParser.From(
                    routerPowerOn,
                    internetLedOn,
                    wanLedOn,
                    neighborsOnSwitchDown);
                SubscriberFaultDiagnosisDto dto = await _app.FaultDiagnosis.DiagnoseAsync(
                    id,
                    networkId.Value,
                    led,
                    user?.Id,
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaintenanceFromDiagnosis(
            int id,
            long diagnosisId,
            CancellationToken cancellationToken)
        {
            IActionResult? denied = await DenyClientOnlyOrMissingClientViewAsync();
            if (denied != null)
            {
                return denied;
            }

            SubscriberFaultDiagnosisDto? diagnosis = await _app.FaultDiagnosis.GetByIdAsync(diagnosisId, cancellationToken);
            if (diagnosis == null || !diagnosis.Success || diagnosis.ClientId != id)
            {
                return NotFound(new { success = false, status = "NotFound", message = "سجل التشخيص غير موجود." });
            }

            if (string.Equals(diagnosis.Cause, nameof(SubscriberFaultComponent.Account), StringComparison.Ordinal)
                || !diagnosis.CanCreateMaintenance)
            {
                return BadRequest(new
                {
                    success = false,
                    status = "NotApplicable",
                    message = diagnosis.MaintenanceRequestId.HasValue
                        ? "تم ربط هذا التشخيص بطلب صيانة مسبقاً."
                        : "سبب العطل محاسبي ولا يحتاج طلب صيانة."
                });
            }

            Client? client = await _context.Clients
                .AsNoTracking()
                .Include(c => c.Receiver)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (client == null)
            {
                return NotFound(new { success = false, status = "NotFound", message = "المشترك غير موجود." });
            }

            MaintenanceType type = MaintenanceType.TechnicianVisit;
            if (!string.IsNullOrWhiteSpace(diagnosis.SuggestedMaintenanceType)
                && Enum.TryParse(diagnosis.SuggestedMaintenanceType, ignoreCase: true, out MaintenanceType parsed))
            {
                type = parsed;
            }

            MaintenanceRequest request = new()
            {
                ClientId = client.Id,
                Type = type,
                Description = SubscriberFaultDiagnosisText.AppendToDescription(null, diagnosis.CauseLabel, diagnosis.Summary),
                Priority = diagnosis.Cause is "Server" or "Sector" or "Receiver" ? RequestPriority.Urgent : RequestPriority.High,
                Status = MaintenanceRequestStatus.Pending,
                RequestDate = DateTime.Now,
                ContactPhone = client.PhoneNumber,
                Address = client.Receiver?.Name
            };

            _context.MaintenanceRequests.Add(request);
            await _context.SaveChangesAsync(cancellationToken);

            SubscriberFaultDiagnosisDto linked = await _app.FaultDiagnosis.LinkToMaintenanceRequestAsync(
                diagnosisId,
                request.Id,
                cancellationToken);

            string? detailsUrl = Url.Action("MaintenanceRequestDetails", "RequestsManagement", new { id = request.Id });
            return Json(new
            {
                success = true,
                status = "Ok",
                maintenanceRequestId = request.Id,
                diagnosisId = linked.DiagnosisId,
                detailsUrl,
                message = "تم إنشاء طلب الصيانة من نتيجة التشخيص."
            });
        }

        private async Task<IActionResult?> DenyClientOnlyOrMissingClientViewAsync()
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

            return null;
        }
    }
}
