using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Areas.SystemAdmin.ViewModels;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services.SystemAdminPricing;

namespace RadTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class ServiceCatalogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServiceCatalogController> _logger;
        private readonly IRecurringServicePricingHandlerResolver _recurringPricingHandlerResolver;
        private readonly IStandaloneServicePricingHandlerResolver _standalonePricingHandlerResolver;
        private readonly IServiceCatalogSnapshotProvider _serviceCatalogSnapshotProvider;

        public ServiceCatalogController(
            ApplicationDbContext context,
            ILogger<ServiceCatalogController> logger,
            IRecurringServicePricingHandlerResolver recurringPricingHandlerResolver,
            IStandaloneServicePricingHandlerResolver standalonePricingHandlerResolver,
            IServiceCatalogSnapshotProvider serviceCatalogSnapshotProvider)
        {
            _context = context;
            _logger = logger;
            _recurringPricingHandlerResolver = recurringPricingHandlerResolver;
            _standalonePricingHandlerResolver = standalonePricingHandlerResolver;
            _serviceCatalogSnapshotProvider = serviceCatalogSnapshotProvider;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "كتالوج الخدمات";

            var snapshot = await _serviceCatalogSnapshotProvider.BuildAsync();
            var model = new ServiceCatalogPageViewModel
            {
                Items = snapshot.Items,
                NetworkPricing = MapPricing(snapshot.NetworkPricing),
                ServerPricing = MapPricing(snapshot.ServerPricing),
                SectorPricing = MapPricing(snapshot.SectorPricing),
                ReceiverPricing = MapPricing(snapshot.ReceiverPricing),
                ClientPricing = MapPricing(snapshot.ClientPricing),
                UserPricing = MapPricing(snapshot.UserPricing),
                SpeedProfilePricing = MapPricing(snapshot.SpeedProfilePricing),
                ReportPricing = new ServicePricingCardViewModel
                {
                    ServiceName = "التقارير",
                    Description = "توليد/إنشاء تقرير من مدير الشركة مع خصم مباشر حسب السعر المحدد من مدير النظام.",
                    InitialPriceSyp = snapshot.ReportPricing.InitialPriceSyp,
                    RenewalBillingPeriod = PricingBillingPeriod.OneTime,
                    RenewalPricePerUnitSyp = 0m,
                    HasInitialPricing = snapshot.ReportPricing.HasInitialPricing,
                    HasRenewalPricing = false
                },
                ProfilePriceTax = new FlatServicePriceViewModel
                {
                    ServiceName = "ضريبة سعر البروفايل",
                    Description = "نسبة الضريبة المئوية التي تطبق تلقائياً على سعر البروفايل عند إدخاله من مدير الشركة.",
                    Price = snapshot.ProfilePriceTax
                },
                MaintenanceCommission = new MaintenanceCommissionSettingsViewModel
                {
                    ServiceName = "عمولة طلبات الصيانة",
                    Description = "تُضاف تلقائياً إلى إجمالي فاتورة الصيانة على العميل.",
                    CommissionMode = snapshot.MaintenanceCommissionMode,
                    CommissionValue = snapshot.MaintenanceCommissionValue
                }
            };
            return View(model);
        }

        private static ServicePricingCardViewModel MapPricing(RecurringServiceSnapshot s) => new()
        {
            ServiceName = s.ServiceName,
            Description = s.Description,
            InitialPriceSyp = s.InitialPriceSyp,
            RenewalBillingPeriod = s.RenewalBillingPeriod,
            RenewalPricePerUnitSyp = s.RenewalPricePerUnitSyp,
            HasInitialPricing = s.HasInitialPricing,
            HasRenewalPricing = s.HasRenewalPricing,
            FreeInitialUnits = s.FreeInitialUnits,
            FreeRenewalUnits = s.FreeRenewalUnits
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNetworkPricing(
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits = 0,
            int freeRenewalUnits = 0)
        {
            return await UpdateRecurringServicePricingAsync(
                RecurringPricingHandlerKeys.Networks,
                initialPriceSyp,
                renewalBillingPeriod,
                renewalPricePerUnitSyp,
                freeInitialUnits,
                freeRenewalUnits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateServerPricing(
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits = 0,
            int freeRenewalUnits = 0)
        {
            return await UpdateRecurringServicePricingAsync(
                RecurringPricingHandlerKeys.Servers,
                initialPriceSyp,
                renewalBillingPeriod,
                renewalPricePerUnitSyp,
                freeInitialUnits,
                freeRenewalUnits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSectorPricing(
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits = 0,
            int freeRenewalUnits = 0)
        {
            return await UpdateRecurringServicePricingAsync(
                RecurringPricingHandlerKeys.Sectors,
                initialPriceSyp,
                renewalBillingPeriod,
                renewalPricePerUnitSyp,
                freeInitialUnits,
                freeRenewalUnits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReceiverPricing(
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits = 0,
            int freeRenewalUnits = 0)
        {
            return await UpdateRecurringServicePricingAsync(
                RecurringPricingHandlerKeys.Receivers,
                initialPriceSyp,
                renewalBillingPeriod,
                renewalPricePerUnitSyp,
                freeInitialUnits,
                freeRenewalUnits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateClientPricing(
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits = 0,
            int freeRenewalUnits = 0)
        {
            return await UpdateRecurringServicePricingAsync(
                RecurringPricingHandlerKeys.Clients,
                initialPriceSyp,
                renewalBillingPeriod,
                renewalPricePerUnitSyp,
                freeInitialUnits,
                freeRenewalUnits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserPricing(
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits = 0,
            int freeRenewalUnits = 0)
        {
            return await UpdateRecurringServicePricingAsync(
                RecurringPricingHandlerKeys.Users,
                initialPriceSyp,
                renewalBillingPeriod,
                renewalPricePerUnitSyp,
                freeInitialUnits,
                freeRenewalUnits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSpeedProfilePricing(
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits = 0,
            int freeRenewalUnits = 0)
        {
            return await UpdateRecurringServicePricingAsync(
                RecurringPricingHandlerKeys.SpeedProfiles,
                initialPriceSyp,
                renewalBillingPeriod,
                renewalPricePerUnitSyp,
                freeInitialUnits,
                freeRenewalUnits);
        }

        private async Task<IActionResult> UpdateRecurringServicePricingAsync(
            string handlerKey,
            decimal initialPriceSyp,
            PricingBillingPeriod renewalBillingPeriod,
            decimal renewalPricePerUnitSyp,
            int freeInitialUnits,
            int freeRenewalUnits)
        {
            if (!_recurringPricingHandlerResolver.TryResolve(handlerKey, out var handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج التسعير لهذه الخدمة.";
                return RedirectToAction(nameof(Index));
            }

            var result = await handler.UpdateAsync(new RecurringPricingUpdateInput
            {
                InitialPriceSyp = initialPriceSyp,
                RenewalBillingPeriod = renewalBillingPeriod,
                RenewalPricePerUnitSyp = renewalPricePerUnitSyp,
                FreeInitialUnits = freeInitialUnits,
                FreeRenewalUnits = freeRenewalUnits
            });

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> UpdateReportPricingAsync(decimal initialPriceSyp)
        {
            if (!_standalonePricingHandlerResolver.TryResolveReport(out var handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج تسعير التقارير.";
                return RedirectToAction(nameof(Index));
            }

            var result = await handler.UpdateAsync(initialPriceSyp);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> UpdateProfileTaxPricingAsync(decimal taxPercentage)
        {
            if (!_standalonePricingHandlerResolver.TryResolveProfileTax(out var handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج ضريبة سعر البروفايل.";
                return RedirectToAction(nameof(Index));
            }

            var result = await handler.UpdateAsync(taxPercentage);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> UpdateMaintenanceCommissionPricingAsync(
            MaintenanceCommissionMode commissionMode,
            decimal commissionValue)
        {
            if (!_standalonePricingHandlerResolver.TryResolveMaintenanceCommission(out var handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج عمولة الصيانة.";
                return RedirectToAction(nameof(Index));
            }

            var result = await handler.UpdateAsync(commissionMode, commissionValue);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReportPricing(decimal initialPriceSyp)
        {
            return await UpdateReportPricingAsync(initialPriceSyp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfilePriceTax(decimal taxPercentage)
        {
            return await UpdateProfileTaxPricingAsync(taxPercentage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMaintenanceCommissionPricing(
            MaintenanceCommissionMode commissionMode,
            decimal commissionValue)
        {
            return await UpdateMaintenanceCommissionPricingAsync(commissionMode, commissionValue);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string key, string displayName, string? description = null, string? iconClass = null, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(displayName))
            {
                TempData["Error"] = "المفتاح واسم الخدمة مطلوبان.";
                return RedirectToAction(nameof(Index));
            }

            key = key.Trim();
            displayName = displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            iconClass = string.IsNullOrWhiteSpace(iconClass) ? null : iconClass.Trim();

            try
            {
                // Prevent collisions with built-in feature keys
                if (!key.StartsWith("CUSTOM:", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "مفتاح الخدمة يجب أن يبدأ بـ CUSTOM: لتجنب التعارض.";
                    return RedirectToAction(nameof(Index));
                }

                var exists = await _context.SystemServices.AnyAsync(s => s.Key == key);
                if (exists)
                {
                    TempData["Error"] = "يوجد خدمة بنفس المفتاح.";
                    return RedirectToAction(nameof(Index));
                }

                _context.SystemServices.Add(new SystemService
                {
                    Key = key,
                    DisplayName = displayName,
                    Description = description,
                    IconClass = iconClass,
                    IsActive = isActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "تمت إضافة الخدمة.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create SystemService.");
                TempData["Error"] = "تعذر إضافة الخدمة.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, string displayName, string? description = null, string? iconClass = null, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                TempData["Error"] = "اسم الخدمة مطلوب.";
                return RedirectToAction(nameof(Index));
            }

            displayName = displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            iconClass = string.IsNullOrWhiteSpace(iconClass) ? null : iconClass.Trim();

            try
            {
                var row = await _context.SystemServices.FindAsync(id);
                if (row == null)
                {
                    return NotFound();
                }

                row.DisplayName = displayName;
                row.Description = description;
                row.IconClass = iconClass;
                row.IsActive = isActive;
                row.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم تحديث الخدمة.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update SystemService.");
                TempData["Error"] = "تعذر تحديث الخدمة.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var row = await _context.SystemServices.FindAsync(id);
                if (row == null)
                {
                    return NotFound();
                }

                _context.SystemServices.Remove(row);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الخدمة.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete SystemService.");
                TempData["Error"] = "تعذر حذف الخدمة (قد تكون مستخدمة بالتسعير/الاشتراكات).";
            }

            return RedirectToAction(nameof(Index));
        }
    }

}

