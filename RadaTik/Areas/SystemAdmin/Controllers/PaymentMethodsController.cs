using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class PaymentMethodsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentMethodsController> _logger;

        public PaymentMethodsController(ApplicationDbContext context, ILogger<PaymentMethodsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "طرق الدفع";

            List<PaymentMethod> list = await _context.PaymentMethods
                .AsNoTracking()
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.Name)
                .ToListAsync();

            ViewBag.Items = list;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, int displayOrder = 0, bool isActive = true, bool isCash = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "الاسم مطلوب.";
                return RedirectToAction(nameof(Index));
            }

            name = name.Trim();

            try
            {
                bool exists = await _context.PaymentMethods.AnyAsync(m => m.Name == name);
                if (exists)
                {
                    TempData["Error"] = "يوجد طريقة دفع بنفس الاسم.";
                    return RedirectToAction(nameof(Index));
                }

                _context.PaymentMethods.Add(new PaymentMethod
                {
                    Name = name,
                    DisplayOrder = displayOrder,
                    IsActive = isActive,
                    IsCash = isCash,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = AppMessages.OperationSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create payment method.");
                TempData["Error"] = "تعذر إضافة طريقة الدفع.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, string name, int displayOrder = 0, bool isActive = true, bool isCash = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "الاسم مطلوب.";
                return RedirectToAction(nameof(Index));
            }

            name = name.Trim();

            try
            {
                PaymentMethod? row = await _context.PaymentMethods.FindAsync(id);
                if (row == null)
                {
                    return NotFound();
                }

                bool exists = await _context.PaymentMethods.AnyAsync(m => m.Id != id && m.Name == name);
                if (exists)
                {
                    TempData["Error"] = "يوجد طريقة دفع أخرى بنفس الاسم.";
                    return RedirectToAction(nameof(Index));
                }

                row.Name = name;
                row.DisplayOrder = displayOrder;
                row.IsActive = isActive;
                row.IsCash = isCash;
                row.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                TempData["Success"] = AppMessages.OperationSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update payment method.");
                TempData["Error"] = "تعذر تحديث طريقة الدفع.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                PaymentMethod? row = await _context.PaymentMethods.FindAsync(id);
                if (row == null)
                {
                    return NotFound();
                }

                _context.PaymentMethods.Remove(row);
                await _context.SaveChangesAsync();
                TempData["Success"] = AppMessages.OperationSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete payment method.");
                TempData["Error"] = "تعذر حذف طريقة الدفع (قد تكون مستخدمة في طلبات سابقة).";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

