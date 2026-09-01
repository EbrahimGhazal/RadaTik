using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Controllers;

public partial class ClientsController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SystemAdministrator,NetworkAdministrator,CompanyEmployee,Employee")]
    [RequirePermission("Clients.Edit")]
    public async Task<IActionResult> UploadNationalId(int id, string? side, IFormFile? image, bool remove = false)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
        if (!networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction(nameof(Index));
        }

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
        if (client == null)
        {
            return NotFound();
        }

        string? error = await ApplyNationalIdUploadAsync(client, side, image, remove);
        if (error != null)
        {
            TempData["Error"] = error;
        }
        else
        {
            TempData["Success"] = remove ? "تم حذف صورة الهوية." : "تم حفظ صورة الهوية.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<string?> ApplyNationalIdUploadAsync(
        Client client,
        string? side,
        IFormFile? image,
        bool remove)
    {
        bool isFront = string.Equals(side, "front", StringComparison.OrdinalIgnoreCase);
        bool isBack = string.Equals(side, "back", StringComparison.OrdinalIgnoreCase);
        if (!isFront && !isBack)
        {
            return "حدد وجه الهوية (أمامي أو خلفي).";
        }

        if (remove)
        {
            if (isFront)
            {
                _app.NationalIdImages.DeleteOwned(client.NationalIdFrontPath, client.Id);
                client.NationalIdFrontPath = null;
            }
            else
            {
                _app.NationalIdImages.DeleteOwned(client.NationalIdBackPath, client.Id);
                client.NationalIdBackPath = null;
            }

            client.LastUpdated = DateTime.Now;
            await _context.SaveChangesAsync();
            return null;
        }

        Domain.Common.ServiceResult<string> saved = await _app.NationalIdImages.SaveAsync(client.Id, image!);
        if (!saved.IsSuccess || string.IsNullOrWhiteSpace(saved.Value))
        {
            return saved.ErrorMessage ?? "تعذر حفظ صورة الهوية.";
        }

        if (isFront)
        {
            _app.NationalIdImages.DeleteOwned(client.NationalIdFrontPath, client.Id);
            client.NationalIdFrontPath = saved.Value;
        }
        else
        {
            _app.NationalIdImages.DeleteOwned(client.NationalIdBackPath, client.Id);
            client.NationalIdBackPath = saved.Value;
        }

        client.LastUpdated = DateTime.Now;
        await _context.SaveChangesAsync();
        return null;
    }
}
