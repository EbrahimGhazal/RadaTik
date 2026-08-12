using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.SystemAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.SystemAdminPricing;

namespace RadaTik.Areas.SystemAdmin.Controllers
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
        private readonly ISystemAdminPricingReadinessService _pricingReadiness;

        public ServiceCatalogController(
            ApplicationDbContext context,
            ILogger<ServiceCatalogController> logger,
            IRecurringServicePricingHandlerResolver recurringPricingHandlerResolver,
            IStandaloneServicePricingHandlerResolver standalonePricingHandlerResolver,
            IServiceCatalogSnapshotProvider serviceCatalogSnapshotProvider,
            ISystemAdminPricingReadinessService pricingReadiness)
        {
            _context = context;
            _logger = logger;
            _recurringPricingHandlerResolver = recurringPricingHandlerResolver;
            _standalonePricingHandlerResolver = standalonePricingHandlerResolver;
            _serviceCatalogSnapshotProvider = serviceCatalogSnapshotProvider;
            _pricingReadiness = pricingReadiness;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "كتالوج الخدمات";

            SystemAdminPricingReadiness readiness = await _pricingReadiness.EvaluateAsync();
            ViewBag.PricingSetupIncomplete = !readiness.IsComplete;
            ViewBag.PricingSetupMissingCount = readiness.MissingItems.Count;
            ViewBag.PricingReadinessConfigured = readiness.ConfiguredCount;
            ViewBag.PricingReadinessTotal = readiness.TotalRequired;

            ServiceCatalogSnapshot snapshot = await _serviceCatalogSnapshotProvider.BuildAsync();
            ServiceCatalogPageViewModel model = new ServiceCatalogPageViewModel
            {
                DocumentationItems = snapshot.Documentation
                    .Select(d => new ServiceCatalogDocumentationItemViewModel
                    {
                        FeatureKey = d.FeatureKey,
                        DisplayName = d.DisplayName,
                        Category = d.Category,
                        Description = d.Description,
                        DetailHtml = d.DetailHtml,
                        PricingPolicyHtml = d.PricingPolicyHtml,
                        RenewalPolicyHtml = d.RenewalPolicyHtml,
                        PricingPlansSummaryHtml = d.PricingPlansSummaryHtml,
                        SuggestedRenewalPolicyHtml = d.SuggestedRenewalPolicyHtml
                    })
                    .ToList(),
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
                    Price = snapshot.ProfilePriceTax,
                    HasPricing = snapshot.HasProfilePriceTax
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
            if (!_recurringPricingHandlerResolver.TryResolve(handlerKey, out IRecurringServicePricingHandler? handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج التسعير لهذه الخدمة.";
                return RedirectToAction(nameof(Index));
            }

            RecurringPricingUpdateResult result = await handler.UpdateAsync(new RecurringPricingUpdateInput
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
            if (!_standalonePricingHandlerResolver.TryResolveReport(out IReportPricingHandler? handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج تسعير التقارير.";
                return RedirectToAction(nameof(Index));
            }

            StandalonePricingUpdateResult result = await handler.UpdateAsync(initialPriceSyp);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> UpdateProfileTaxPricingAsync(decimal taxPercentage)
        {
            if (!_standalonePricingHandlerResolver.TryResolveProfileTax(out IProfileTaxPricingHandler? handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج ضريبة سعر البروفايل.";
                return RedirectToAction(nameof(Index));
            }

            StandalonePricingUpdateResult result = await handler.UpdateAsync(taxPercentage);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> UpdateMaintenanceCommissionPricingAsync(
            MaintenanceCommissionMode commissionMode,
            decimal commissionValue)
        {
            if (!_standalonePricingHandlerResolver.TryResolveMaintenanceCommission(out IMaintenanceCommissionPricingHandler? handler) || handler == null)
            {
                TempData["Error"] = "تعذر تحميل معالج عمولة الصيانة.";
                return RedirectToAction(nameof(Index));
            }

            StandalonePricingUpdateResult result = await handler.UpdateAsync(commissionMode, commissionValue);
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
        public async Task<IActionResult> SaveDocumentation()
        {
            DateTime now = DateTime.UtcNow;
            foreach (FeatureCatalog.FeatureDefinition def in FeatureCatalog.All)
            {
                string safeKey = SanitizeFormKey(def.Key);
                string? detail = Request.Form[$"DocDetail_{safeKey}"].ToString();
                string? pricingPolicy = Request.Form[$"DocPricing_{safeKey}"].ToString();
                string? renewalPolicy = Request.Form[$"DocRenewal_{safeKey}"].ToString();

                FeaturePublicInfo? row = await _context.FeaturePublicInfos
                    .FirstOrDefaultAsync(f => f.FeatureKey == def.Key);

                if (row == null)
                {
                    row = new FeaturePublicInfo { FeatureKey = def.Key };
                    _context.FeaturePublicInfos.Add(row);
                }

                row.DetailHtml = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
                row.PricingPolicyHtml = string.IsNullOrWhiteSpace(pricingPolicy) ? null : pricingPolicy.Trim();
                row.RenewalPolicyHtml = string.IsNullOrWhiteSpace(renewalPolicy) ? null : renewalPolicy.Trim();
                row.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حفظ توثيق الخدمات (الشرح وسياسات التسعير والتجديد).";
            return RedirectToAction(nameof(Index), new { docSaved = 1 });
        }

        private static string SanitizeFormKey(string featureKey) =>
            new string(featureKey.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAllPricing()
        {
            ServiceCatalogSaveViewModel model = ServiceCatalogFormParser.Parse(Request.Form);
            List<string> errors = [];

            (string HandlerKey, RecurringPricingFormSection Section)[] recurringSections =
            [
                (RecurringPricingHandlerKeys.Networks, model.Network),
                (RecurringPricingHandlerKeys.Servers, model.Server),
                (RecurringPricingHandlerKeys.Sectors, model.Sector),
                (RecurringPricingHandlerKeys.Receivers, model.Receiver),
                (RecurringPricingHandlerKeys.Clients, model.Client),
                (RecurringPricingHandlerKeys.Users, model.User),
                (RecurringPricingHandlerKeys.SpeedProfiles, model.SpeedProfile)
            ];

            foreach ((string handlerKey, RecurringPricingFormSection section) in recurringSections)
            {
                if (!_recurringPricingHandlerResolver.TryResolve(handlerKey, out IRecurringServicePricingHandler? handler) || handler == null)
                {
                    errors.Add($"تعذر تحميل معالج التسعير للخدمة: {handlerKey}.");
                    continue;
                }

                RecurringPricingUpdateResult result = await handler.UpdateAsync(new RecurringPricingUpdateInput
                {
                    InitialPriceSyp = section.InitialPriceSyp,
                    RenewalBillingPeriod = section.RenewalBillingPeriod,
                    RenewalPricePerUnitSyp = section.RenewalPricePerUnitSyp,
                    FreeInitialUnits = section.FreeInitialUnits,
                    FreeRenewalUnits = section.FreeRenewalUnits
                });

                if (!result.Success)
                {
                    errors.Add(result.Message);
                }
            }

            if (!_standalonePricingHandlerResolver.TryResolveReport(out IReportPricingHandler? reportHandler) || reportHandler == null)
            {
                errors.Add("تعذر تحميل معالج تسعير التقارير.");
            }
            else
            {
                StandalonePricingUpdateResult reportResult = await reportHandler.UpdateAsync(model.ReportInitialPriceSyp);
                if (!reportResult.Success)
                {
                    errors.Add(reportResult.Message);
                }
            }

            if (!_standalonePricingHandlerResolver.TryResolveMaintenanceCommission(out IMaintenanceCommissionPricingHandler? maintenanceHandler) || maintenanceHandler == null)
            {
                errors.Add("تعذر تحميل معالج عمولة الصيانة.");
            }
            else
            {
                StandalonePricingUpdateResult maintenanceResult = await maintenanceHandler.UpdateAsync(
                    model.MaintenanceCommissionMode,
                    model.MaintenanceCommissionValue);
                if (!maintenanceResult.Success)
                {
                    errors.Add(maintenanceResult.Message);
                }
            }

            if (!_standalonePricingHandlerResolver.TryResolveProfileTax(out IProfileTaxPricingHandler? taxHandler) || taxHandler == null)
            {
                errors.Add("تعذر تحميل معالج ضريبة سعر البروفايل.");
            }
            else
            {
                StandalonePricingUpdateResult taxResult = await taxHandler.UpdateAsync(model.ProfileTaxPercentage);
                if (!taxResult.Success)
                {
                    errors.Add(taxResult.Message);
                }
            }

            if (errors.Count > 0)
            {
                TempData["Error"] = string.Join(" ", errors.Distinct());
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToAction(nameof(Index), new { saved = 1 });
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

                bool exists = await _context.SystemServices.AnyAsync(s => s.Key == key);
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
                TempData["Success"] = AppMessages.OperationSuccess;
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
                SystemService? row = await _context.SystemServices.FindAsync(id);
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

                TempData["Success"] = AppMessages.OperationSuccess;
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
                SystemService? row = await _context.SystemServices.FindAsync(id);
                if (row == null)
                {
                    return NotFound();
                }

                _context.SystemServices.Remove(row);
                await _context.SaveChangesAsync();
                TempData["Success"] = AppMessages.OperationSuccess;
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

