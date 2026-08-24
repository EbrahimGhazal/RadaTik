using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.PricingPolicies;
using RadaTik.Services.PricingPreview;
using RadaTik.Helpers;
using RadaTik.Security;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace RadaTik.Controllers
{
    public partial class ClientsController : Controller
    {
        // GET: Clients/Create
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = new Client
            {
                ServiceStartDate = DateTime.Now.Date,
                AccountExpirationDate = DateTime.Now.Date.AddMonths(1)
            };
            ApplyCreateFormViewData(await _app.FormViewData.BuildCreateFormDataAsync(networkId.Value, client));

            return View(client);
        }

        // POST: Clients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Create")]
        public async Task<IActionResult> Create([Bind("Id,Name,SID,UserName,Password,ProfileId,PhoneNumber,ResidenceAddress,Occupation,Workplace,Latitude,Longitude,PowerSource,Building,Floor,IsActive,ReceiverId,Service,Address,MikroTikServerId,ServiceStartDate,AccountExpirationDate,IsVip,VipNote")] Client client, string? dbUserName, string? dbPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            if (!ModelState.IsValid)
            {
                ApplyCreateFormViewData(await _app.FormViewData.BuildCreateFormDataAsync(networkId.Value, client));
                return View(client);
            }

            var userRoles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();
            var isEmployee = (userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy)) &&
                             !userRoles.Contains(RoleNames.NetworkAdministrator);

            if (user == null)
            {
                ApplyCreateFormViewData(await _app.FormViewData.BuildCreateFormDataAsync(networkId.Value, client));
                return View(client);
            }

            ClientCreateOutcome outcome = await _app.Provisioning.CreateClientAsync(new ClientCreateRequest
            {
                Client = client,
                DbUserName = dbUserName,
                DbPassword = dbPassword,
                NetworkId = networkId.Value,
                ActorUserId = user.Id,
                IsEmployee = isEmployee
            });

            switch (outcome.Status)
            {
                case ClientCreateStatus.Success:
                    TempData["Success"] = $"✅ {outcome.Message}";
                    return RedirectToAction(nameof(Index));
                case ClientCreateStatus.EmployeePendingApproval:
                    TempData["Info"] = outcome.Message;
                    return RedirectToAction(nameof(Index));
                case ClientCreateStatus.ValidationError:
                    if (outcome.FieldErrors != null)
                    {
                        foreach (KeyValuePair<string, string> error in outcome.FieldErrors)
                        {
                            ModelState.AddModelError(error.Key, error.Value);
                        }
                    }
                    break;
                case ClientCreateStatus.Failed:
                    ModelState.AddModelError(string.Empty, $"❌ {outcome.Message}");
                    break;
            }

            ApplyCreateFormViewData(await _app.FormViewData.BuildCreateFormDataAsync(networkId.Value, client));
            return View(client);
        }

        // GET: Clients/Edit/5
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.Profile)
                .Include(c => c.Receiver)
                    .ThenInclude(r => r!.Sector)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            // التحقق من صلاحيات الموظف
            var userRoles = await _userManager.GetRolesAsync(currentUser!);
            bool isEmployee = (userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy)) &&
                              !userRoles.Contains(RoleNames.NetworkAdministrator);

            ApplyEditFormViewData(await _app.FormViewData.BuildEditFormDataAsync(networkId.Value, client));
            var linkedUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ClientId == client.Id);
            ViewBag.DbUserName = linkedUser?.UserName ?? client.UserName;
            ViewBag.IsEmployee = isEmployee;
            ViewBag.ApplyMikroTikChanges = false;
            return View(client);
        }

        // POST: Clients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,SID,UserName,Password,ProfileId,PhoneNumber,ResidenceAddress,Occupation,Workplace,Latitude,Longitude,PowerSource,Building,Floor,ServiceStartDate,CreatedDate,IsActive,ReceiverId,Service,Address,Uptime,ConnectionStatus,MacAddress,MikroTikServerId,AccountExpirationDate,IsVip,VipNote")] Client client,
            string? dbUserName,
            string? dbPassword,
            bool applyMikroTikChanges = false)
        {
            if (id != client.Id)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var userRoles = await _userManager.GetRolesAsync(currentUser!);
            bool isEmployee = (userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy)) &&
                              !userRoles.Contains(RoleNames.NetworkAdministrator);

            if (!ModelState.IsValid || currentUser == null)
            {
                ApplyEditFormViewData(await _app.FormViewData.BuildEditFormDataAsync(networkId.Value, client));
                ViewBag.DbUserName = string.IsNullOrWhiteSpace(dbUserName) ? client.UserName : dbUserName;
                ViewBag.IsEmployee = isEmployee;
                ViewBag.ApplyMikroTikChanges = applyMikroTikChanges && !isEmployee;
                return View(client);
            }

            ClientEditOutcome outcome = await _app.Provisioning.UpdateClientAsync(new ClientEditRequest
            {
                ClientId = id,
                SubmittedClient = client,
                DbUserName = dbUserName,
                DbPassword = dbPassword,
                NetworkId = networkId.Value,
                ActorUserId = currentUser.Id,
                IsEmployee = isEmployee,
                ApplyMikroTikChanges = applyMikroTikChanges && !isEmployee
            });

            switch (outcome.Status)
            {
                case ClientEditStatus.Success:
                    TempData["Success"] = $"✅ {outcome.Message}";
                    return RedirectToAction(nameof(Index));
                case ClientEditStatus.EmployeePendingApproval:
                    TempData["Info"] = outcome.Message;
                    return RedirectToAction(nameof(Index));
                case ClientEditStatus.NotFound:
                    return NotFound();
                case ClientEditStatus.Failed:
                    ModelState.AddModelError(string.Empty, $"❌ {outcome.Message}");
                    break;
            }

            ApplyEditFormViewData(await _app.FormViewData.BuildEditFormDataAsync(networkId.Value, client));
            ViewBag.DbUserName = string.IsNullOrWhiteSpace(dbUserName) ? client.UserName : dbUserName;
            ViewBag.IsEmployee = isEmployee;
            ViewBag.ApplyMikroTikChanges = applyMikroTikChanges && !isEmployee;
            return View(client);
        }

        // GET: Clients/Delete/5
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.Receiver)
                    .ThenInclude(r => r!.Sector)
                .Include(c => c.MikroTikServer)
                .Include(c => c.Profile)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            bool existsOnMikroTik = false;
            if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                bool? exists = await _app.Provisioning.TryCheckUserExistsOnMikroTikAsync(
                    client.UserName,
                    client.MikroTikServerId.Value);
                existsOnMikroTik = exists == true;
            }

            ViewBag.ExistsOnMikroTik = existsOnMikroTik;
            return View(client);
        }

        // POST: Clients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            ClientOperationOutcome outcome = await _app.Provisioning.DeleteClientAsync(id, networkId.Value);
            return ApplyClientOperationOutcome(outcome, nameof(Index));
        }

    }
}
