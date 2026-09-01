using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.Clients;
using global::RadaTik.Services.NewSubscriberWizard;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
[RequirePermission("Clients.Create")]
public class NewSubscriberWizardController : Controller
{
    private sealed record WizardProfileOptionJson(int id, string? name);

    private sealed record WizardSectorOptionJson(int id, string? name);

    private sealed record WizardSharedReceiverOptionJson(int id, string? name, string? sectorName, string? serverName);

    private string CurrentArea => RouteData.Values["area"]?.ToString() ?? "CompanyAdmin";

    private bool IsEmployeeArea =>
        string.Equals(CurrentArea, "CompanyEmployee", StringComparison.OrdinalIgnoreCase);

    private string WizardRouteName =>
        IsEmployeeArea ? "employee-new-subscriber-wizard" : "networkManager-new-subscriber-wizard";

    /// <summary>
    /// Views live under CompanyAdmin; CompanyEmployee inherits this controller and must use the same paths
    /// (otherwise Razor looks only under Areas/CompanyEmployee/Views and fails with "view Start was not found").
    /// </summary>
    private const string WizardViewsRoot = "~/Areas/CompanyAdmin/Views/NewSubscriberWizard/";

    private ViewResult WizardView(string viewName, object? model = null)
    {
        ViewData["WizardRoute"] = WizardRouteName;
        ViewData["WizardArea"] = CurrentArea;
        return model is null
            ? View(WizardViewsRoot + viewName + ".cshtml")
            : View(WizardViewsRoot + viewName + ".cshtml", model);
    }

    private RedirectToRouteResult WizardRedirect(string action, object? routeValues = null)
    {
        RouteValueDictionary values = routeValues is null
            ? new RouteValueDictionary()
            : new RouteValueDictionary(routeValues);
        values["action"] = action;
        return RedirectToRoute(WizardRouteName, values);
    }

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NewSubscriberWizardOrchestrator _orchestrator;
    private readonly ISubscriberInstallationInvoiceService _invoiceService;
    private readonly SubscriberInstallationWarehouseLinkService _warehouseLinkService;
    private readonly IClientFormLookupService _formLookup;

    public NewSubscriberWizardController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        NewSubscriberWizardOrchestrator orchestrator,
        ISubscriberInstallationInvoiceService invoiceService,
        SubscriberInstallationWarehouseLinkService warehouseLinkService,
        IClientFormLookupService formLookup)
    {
        _context = context;
        _userManager = userManager;
        _orchestrator = orchestrator;
        _invoiceService = invoiceService;
        _warehouseLinkService = warehouseLinkService;
        _formLookup = formLookup;
    }

    /// <summary>Canonical entry URL: /networkManager/Clients/wizard and /wizard/Index.</summary>
    [HttpGet]
    public Task<IActionResult> Index(int? receiverId) => Start(receiverId);

    [HttpGet]
    public async Task<IActionResult> Start(int? receiverId)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = CurrentArea });
        }

        if (receiverId.HasValue && receiverId.Value > 0)
        {
            var receiverMeta = await _context.Receivers
                .AsNoTracking()
                .Where(r => r.Id == receiverId.Value && r.NetworkId == networkId.Value)
                .Select(r => new { r.IsActive, r.SectorId, ServerId = r.Sector.MikroTikServerId })
                .FirstOrDefaultAsync();
            if (receiverMeta == null)
            {
                TempData["Error"] = "اللاقط غير موجود في هذه الشبكة.";
                return WizardRedirect(nameof(Index));
            }

            if (!receiverMeta.IsActive)
            {
                TempData["Info"] = "اللاقط بانتظار موافقة المدير. يمكنك متابعة بيانات المشترك الآن؛ التفعيل بعد الاعتماد.";
            }

            HttpContext.Session.SetWizardState(new NewSubscriberWizardState
            {
                Path = NewSubscriberWizardPath.PrivateNewReceiver,
                ReceiverId = receiverId.Value,
                SectorId = receiverMeta.SectorId,
                MikroTikServerId = receiverMeta.ServerId
            });
            return WizardRedirect(nameof(Subscriber));
        }

        NewSubscriberWizardStartViewModel vm = new()
        {
            Receivers = await LoadReceiverOptionsAsync(networkId.Value)
        };
        ViewData["Title"] = "إضافة مشترك جديد";
        return WizardView("Start", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Resume(int clientId)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return WizardRedirect(nameof(Start));
        }

        Client? client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId.Value);
        if (client == null)
        {
            return NotFound();
        }

        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .AsNoTracking()
            .Where(i => i.ClientId == clientId && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync();

        NewSubscriberWizardPath path = await InferWizardPathAsync(client, networkId.Value);

        NewSubscriberWizardState state = new()
        {
            Path = path,
            ClientId = client.Id,
            InvoiceId = invoice?.Id,
            ReceiverId = client.ReceiverId,
            MikroTikServerId = client.MikroTikServerId
        };
        HttpContext.Session.SetWizardState(state);

        if (invoice == null)
        {
            TempData["Info"] = "لا توجد فاتورة تركيب — أكمل بيانات المشترك أولاً.";
            return WizardRedirect(nameof(Subscriber));
        }

        if (invoice.Status == SubscriberInstallationInvoiceStatus.Draft)
        {
            return WizardRedirect(nameof(Invoice));
        }

        if (invoice.Status is SubscriberInstallationInvoiceStatus.Finalized
            or SubscriberInstallationInvoiceStatus.PartiallyPaid
            or SubscriberInstallationInvoiceStatus.PendingWalletPayment)
        {
            return WizardRedirect(nameof(CollectPayment), new { id = invoice.Id });
        }

        return WizardRedirect(nameof(Complete), new { invoiceId = invoice.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(NewSubscriberWizardPath path, int? existingReceiverId)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = CurrentArea });
        }

        NewSubscriberWizardState state = new() { Path = path };

        switch (path)
        {
            case NewSubscriberWizardPath.TowerDirect:
                HttpContext.Session.SetWizardState(state);
                return WizardRedirect(nameof(Subscriber));

            case NewSubscriberWizardPath.PrivateNewReceiver:
                HttpContext.Session.SetWizardState(state);
                string returnUrl = Url.Action(nameof(Start), "NewSubscriberWizard", new { area = CurrentArea })!;
                return RedirectToAction("Create", "Receiver", new { area = GetReceiverArea(), returnUrl });

            case NewSubscriberWizardPath.SharedSelectReceiver:
                HttpContext.Session.SetWizardState(state);
                return WizardRedirect(nameof(SharedReceiver));

            case NewSubscriberWizardPath.ExistingReceiverFromList:
                if (!existingReceiverId.HasValue || existingReceiverId.Value <= 0)
                {
                    TempData["Error"] = "اختر اللاقط من القائمة.";
                    return WizardRedirect(nameof(Start));
                }

                state.ReceiverId = existingReceiverId.Value;
                state.MikroTikServerId = await _context.Receivers
                    .AsNoTracking()
                    .Where(r => r.Id == existingReceiverId.Value)
                    .Select(r => r.Sector.MikroTikServerId)
                    .FirstOrDefaultAsync();
                HttpContext.Session.SetWizardState(state);
                return WizardRedirect(nameof(Subscriber));

            default:
                TempData["Error"] = "اختر نوع الاتصال.";
                return WizardRedirect(nameof(Start));
        }
    }

    [HttpGet]
    public async Task<IActionResult> SharedReceiver()
    {
        NewSubscriberWizardState? state = HttpContext.Session.GetWizardState();
        if (state?.Path != NewSubscriberWizardPath.SharedSelectReceiver)
        {
            return WizardRedirect(nameof(Start));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return RedirectToAction("Index", "Network", new { area = CurrentArea });
        }

        NewSubscriberWizardSharedReceiverViewModel vm = await BuildSharedReceiverViewModelAsync(networkId.Value, state.MikroTikServerId, state.SectorId);
        ViewData["Title"] = "تحديد لاقط مشترك";
        return WizardView("SharedReceiver", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SharedReceiver(int? mikroTikServerId, int? sectorId, int receiverId)
    {
        NewSubscriberWizardState? state = HttpContext.Session.GetWizardState();
        if (state?.Path != NewSubscriberWizardPath.SharedSelectReceiver)
        {
            return WizardRedirect(nameof(Start));
        }

        if (receiverId <= 0)
        {
            TempData["Error"] = "اختر اللاقط المشترك.";
            return WizardRedirect(nameof(SharedReceiver));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return WizardRedirect(nameof(Start));
        }

        bool receiverOk = await SharedReceiverQuery(networkId.Value, mikroTikServerId, sectorId)
            .AnyAsync(r => r.Id == receiverId);
        if (!receiverOk)
        {
            TempData["Error"] = "اللاقط المحدد غير متاح ضمن السيرفر/المرسل الحالي.";
            return WizardRedirect(nameof(SharedReceiver));
        }

        state.ReceiverId = receiverId;
        state.MikroTikServerId = mikroTikServerId;
        state.SectorId = sectorId;
        HttpContext.Session.SetWizardState(state);
        return WizardRedirect(nameof(Subscriber));
    }

    [HttpGet]
    public async Task<IActionResult> Subscriber()
    {
        NewSubscriberWizardState? state = HttpContext.Session.GetWizardState();
        if (state == null)
        {
            return WizardRedirect(nameof(Start));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return WizardRedirect(nameof(Start));
        }

        NewSubscriberWizardSubscriberFormModel model = new()
        {
            Path = state.Path,
            ReceiverId = state.ReceiverId,
            MikroTikServerId = state.MikroTikServerId,
            ServiceStartDate = DateTime.Today,
            AccountExpirationDate = DateTime.Today.AddMonths(1),
            IsActive = true
        };
        await LoadSubscriberViewDataAsync(networkId.Value, state, model.MikroTikServerId);
        ViewData["Title"] = "بيانات المشترك الجديد";
        return WizardView("Subscriber", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscriber(NewSubscriberWizardSubscriberFormModel model)
    {
        NewSubscriberWizardState? state = HttpContext.Session.GetWizardState();
        if (state == null)
        {
            return WizardRedirect(nameof(Start));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return WizardRedirect(nameof(Start));
        }

        model.Path = state.Path;
        if (state.Path != NewSubscriberWizardPath.TowerDirect)
        {
            model.ReceiverId = state.ReceiverId ?? model.ReceiverId;
        }

        model.MikroTikServerId = state.MikroTikServerId ?? model.MikroTikServerId;

        if (state.Path == NewSubscriberWizardPath.TowerDirect && !model.MikroTikServerId.HasValue)
        {
            ModelState.AddModelError(nameof(model.MikroTikServerId), "خادم MikroTik مطلوب عند الاتصال من البرج مباشرة.");
        }

        if (!model.MikroTikServerId.HasValue)
        {
            ModelState.AddModelError(nameof(model.ProfileId), "اختر خادم MikroTik أولاً لعرض بروفايلاته.");
        }
        else if (model.ProfileId is not > 0)
        {
            ModelState.AddModelError(nameof(model.ProfileId), "البروفايل مطلوب.");
        }
        else
        {
            bool profileBelongsToSelectedServer = await _formLookup.ProfileBelongsToServerAsync(
                model.ProfileId.Value,
                model.MikroTikServerId.Value,
                networkId.Value);
            if (!profileBelongsToSelectedServer)
            {
                ModelState.AddModelError(nameof(model.ProfileId), "البروفايل المحدد لا يتبع خادم MikroTik المختار.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadSubscriberViewDataAsync(networkId.Value, state, model.MikroTikServerId);
            ViewData["Title"] = "بيانات المشترك الجديد";
            return WizardView("Subscriber", model);
        }

        Client client = new()
        {
            Name = model.Name,
            SID = model.SID,
            UserName = model.UserName,
            Password = model.Password,
            ProfileId = model.ProfileId!.Value,
            PhoneNumber = model.PhoneNumber,
            ResidenceAddress = model.ResidenceAddress,
            Occupation = string.IsNullOrWhiteSpace(model.Occupation) ? null : model.Occupation.Trim(),
            Workplace = string.IsNullOrWhiteSpace(model.Workplace) ? null : model.Workplace.Trim(),
            ReceiverId = state.Path == NewSubscriberWizardPath.TowerDirect ? null : model.ReceiverId,
            MikroTikServerId = model.MikroTikServerId,
            IsActive = model.IsActive,
            IsVip = model.IsVip,
            VipNote = model.VipNote,
            VipBenefitKind = model.IsVip ? model.VipBenefitKind : ClientVipBenefitKind.None,
            VipDiscountPercent = model.IsVip && model.VipBenefitKind == ClientVipBenefitKind.Discount
                ? model.VipDiscountPercent
                : 0m,
            ServiceStartDate = model.ServiceStartDate,
            AccountExpirationDate = model.AccountExpirationDate
        };

        NewSubscriberWizardOrchestrator.CreateSubscriberResult result = await _orchestrator.CreateSubscriberAsync(
            client,
            user,
            networkId.Value,
            state.Path,
            model.DbUserName,
            model.DbPassword);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            await LoadSubscriberViewDataAsync(networkId.Value, state, model.MikroTikServerId);
            ViewData["Title"] = "بيانات المشترك الجديد";
            return WizardView("Subscriber", model);
        }

        state.ClientId = result.ClientId;
        state.InvoiceId = result.InvoiceId;
        if (model.MikroTikServerId.HasValue)
        {
            state.MikroTikServerId = model.MikroTikServerId;
        }

        HttpContext.Session.SetWizardState(state);

        if (!string.IsNullOrWhiteSpace(result.MikroTikWarning))
        {
            TempData["Info"] = result.MikroTikWarning;
        }
        else
        {
            TempData[result.RequiresManagerApproval ? "Info" : "Success"] = result.RequiresManagerApproval
                ? "تم تسجيل المشترك كطلب موافقة لمدير الشركة. بعد الاعتماد يُنشأ الحساب على سيرفر MikroTik مباشرة."
                : "تم إنشاء المشترك. حدّد كميات المواد لإصدار الفاتورة.";
        }

        return WizardRedirect(nameof(Invoice));
    }

    [HttpGet]
    public async Task<IActionResult> Invoice()
    {
        NewSubscriberWizardState? state = HttpContext.Session.GetWizardState();
        if (state?.InvoiceId == null)
        {
            return WizardRedirect(nameof(Start));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return WizardRedirect(nameof(Start));
        }

        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == state.InvoiceId && i.NetworkId == networkId.Value);
        if (invoice == null)
        {
            return NotFound();
        }

        bool requiresApproval = state.ClientId.HasValue && await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.Id == state.ClientId && c.ConnectionStatus == EmployeeApprovalStates.PendingClientConnectionStatus);

        PricingWarehouseReadiness warehouseReadiness = await _warehouseLinkService.GetReadinessAsync(networkId.Value);

        List<NewSubscriberWizardInvoiceLineViewModel> lines = [];
        foreach (SubscriberInstallationInvoiceItem item in invoice.Items)
        {
            IReadOnlyList<WarehouseModelOption> models = [];
            if (item.IsStockItem && !string.IsNullOrWhiteSpace(item.MaterialKey))
            {
                models = await _warehouseLinkService.GetModelsForMaterialAsync(
                    networkId.Value, item.MaterialKey);
            }

            lines.Add(new NewSubscriberWizardInvoiceLineViewModel
            {
                ItemId = item.Id,
                ItemName = item.ItemName,
                MaterialKey = item.MaterialKey,
                IsStockItem = item.IsStockItem,
                WarehouseItemId = item.WarehouseItemId,
                AvailableModels = models.Select(m => new InvoiceWarehouseModelOptionViewModel
                {
                    WarehouseItemId = m.WarehouseItemId,
                    DisplayLabel = m.DisplayLabel,
                    IsDefault = m.IsDefault
                }).ToList(),
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal
            });
        }

        NewSubscriberWizardInvoiceViewModel vm = new()
        {
            InvoiceId = invoice.Id,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName,
            Path = state.Path,
            RequiresManagerApproval = requiresApproval,
            WarehousePricingReady = warehouseReadiness.IsReadyForWarehouseFinalize,
            UnlinkedStockLineCount = warehouseReadiness.UnlinkedStockLineCount,
            TotalAmount = invoice.TotalAmount,
            Lines = lines
        };

        ViewData["Title"] = "فاتورة تجهيز المشترك";
        return WizardView("Invoice", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invoice(int invoiceId, List<NewSubscriberWizardInvoiceLineViewModel> lines, bool finalize)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue || user == null)
        {
            return WizardRedirect(nameof(Start));
        }

        IReadOnlyList<DraftInvoiceLineUpdate> updates = lines
            .Select(l => new DraftInvoiceLineUpdate
            {
                ItemId = l.ItemId,
                Quantity = l.Quantity,
                WarehouseItemId = l.WarehouseItemId
            })
            .ToList();

        FinalizeInvoiceResult updateResult = await _invoiceService.UpdateDraftInvoiceItemsAsync(
            invoiceId, networkId.Value, updates);
        if (!updateResult.Success)
        {
            TempData["Error"] = updateResult.ErrorMessage;
            return WizardRedirect(nameof(Invoice));
        }

        if (finalize)
        {
            PricingWarehouseReadiness readinessCheck = await _warehouseLinkService.GetReadinessAsync(networkId.Value);
            if (!readinessCheck.IsReadyForWarehouseFinalize)
            {
                TempData["Error"] = "أكمل ربط مواد التركيب بأصناف المستودع من صفحة تسعير التركيب قبل الإصدار.";
                return WizardRedirect(nameof(Invoice));
            }

            NewSubscriberWizardState? state = HttpContext.Session.GetWizardState();
            bool requiresApproval = state?.ClientId is int clientId && await _context.Clients
                .AsNoTracking()
                .AnyAsync(c => c.Id == clientId && c.ConnectionStatus == EmployeeApprovalStates.PendingClientConnectionStatus);
            if (requiresApproval)
            {
                TempData["Error"] = "لا يمكن إصدار الفاتورة وخصم المستودع قبل موافقة المدير على المشترك.";
                return WizardRedirect(nameof(Invoice));
            }

            FinalizeInvoiceResult finalizeResult = await _invoiceService.FinalizeInvoiceAsync(
                invoiceId, networkId.Value, user.Id);
            if (!finalizeResult.Success)
            {
                TempData["Error"] = finalizeResult.ErrorMessage;
                return WizardRedirect(nameof(Invoice));
            }

            if (state != null)
            {
                state.InvoiceId = invoiceId;
                HttpContext.Session.SetWizardState(state);
            }

            return WizardRedirect(nameof(CollectPayment), new { id = invoiceId });
        }

        TempData["Success"] = "تم حفظ الكميات.";
        return WizardRedirect(nameof(Invoice));
    }

    [HttpGet]
    public async Task<IActionResult> CollectPayment(int id)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return WizardRedirect(nameof(Start));
        }

        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.NetworkId == networkId.Value);
        if (invoice == null || invoice.Status != SubscriberInstallationInvoiceStatus.Finalized)
        {
            TempData["Error"] = "الفاتورة غير جاهزة للتحصيل.";
            return WizardRedirect(nameof(Invoice));
        }

        Client? client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == invoice.ClientId);
        if (client == null)
        {
            return NotFound();
        }

        if (client.ConnectionStatus == EmployeeApprovalStates.PendingClientConnectionStatus)
        {
            return WizardRedirect(nameof(Complete), new { invoiceId = id, paymentRecorded = false });
        }

        if (invoice.RemainingAmount <= 0m)
        {
            return WizardRedirect(nameof(Complete), new { invoiceId = id, paymentRecorded = true });
        }

        NewSubscriberWizardCollectPaymentViewModel vm = new()
        {
            InvoiceId = invoice.Id,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName,
            TotalAmount = invoice.TotalAmount,
            RemainingAmount = invoice.RemainingAmount,
            ClientWalletBalance = client.Balance
        };
        ViewData["Title"] = "تحصيل فاتورة التجهيز";
        return WizardView("CollectPayment", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CollectPayment(
        int invoiceId,
        decimal amount,
        SubscriberInstallationPaymentMethod paymentMethod,
        string? notes)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue || user == null)
        {
            return WizardRedirect(nameof(Start));
        }

        RegisterInstallationPaymentResult result = await _invoiceService.RegisterPaymentAsync(
            invoiceId, networkId.Value, user.Id, amount, paymentMethod, notes);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return WizardRedirect(nameof(CollectPayment), new { id = invoiceId });
        }

        TempData["Success"] = result.NewStatus == SubscriberInstallationInvoiceStatus.Paid
            ? "تم تسديد الفاتورة بالكامل."
            : "تم تسجيل الدفعة.";
        return WizardRedirect(nameof(Complete), new { invoiceId, paymentRecorded = true });
    }

    [HttpGet]
    public async Task<IActionResult> Complete(int invoiceId, bool paymentRecorded = false)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return WizardRedirect(nameof(Start));
        }

        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.NetworkId == networkId.Value);
        if (invoice == null)
        {
            return NotFound();
        }

        bool requiresApproval = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.Id == invoice.ClientId && c.ConnectionStatus == EmployeeApprovalStates.PendingClientConnectionStatus);

        HttpContext.Session.ClearWizardState();

        NewSubscriberWizardCompleteViewModel vm = new()
        {
            ClientId = invoice.ClientId,
            InvoiceId = invoice.Id,
            ClientName = invoice.ClientName,
            RequiresManagerApproval = requiresApproval,
            InvoiceFinalized = invoice.Status is SubscriberInstallationInvoiceStatus.Finalized
                or SubscriberInstallationInvoiceStatus.Paid
                or SubscriberInstallationInvoiceStatus.PartiallyPaid,
            PaymentRecorded = paymentRecorded || invoice.Status == SubscriberInstallationInvoiceStatus.Paid
        };
        ViewData["Title"] = "اكتمال إضافة المشترك";
        return WizardView("Complete", vm);
    }

    [HttpGet]
    public IActionResult Cancel()
    {
        HttpContext.Session.ClearWizardState();
        return RedirectToAction("Index", "Clients", new { area = CurrentArea });
    }

    [HttpGet]
    public async Task<IActionResult> SearchSharedReceivers(int? serverId, int? sectorId)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return Json(Array.Empty<object>());
        }

        IQueryable<Receiver> query = SharedReceiverQuery(networkId.Value, serverId, sectorId);

        List<WizardSharedReceiverOptionJson> rows = await query
            .OrderBy(r => r.Name)
            .Select(r => new WizardSharedReceiverOptionJson(
                r.Id,
                r.Name ?? ("#" + r.Id),
                r.Sector.Name ?? "—",
                r.Sector.MikroTikServer != null ? r.Sector.MikroTikServer.Name : "—"))
            .Take(200)
            .ToListAsync();

        return Json(rows);
    }

    [HttpGet]
    public async Task<IActionResult> GetSectorsByServer(int? serverId)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return Json(Array.Empty<object>());
        }

        IQueryable<Sector> query = SharedSectorQuery(networkId.Value, serverId);

        List<WizardSectorOptionJson> sectors = await query
            .OrderBy(s => s.Name)
            .Select(s => new WizardSectorOptionJson(s.Id, s.Name))
            .ToListAsync();

        return Json(sectors);
    }

    [HttpGet]
    public async Task<IActionResult> GetProfilesByServer(int serverId)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return Json(Array.Empty<object>());
        }

        IReadOnlyList<ClientFormProfileOption> profiles =
            await _formLookup.GetProfilesByServerAsync(serverId, networkId.Value);

        return Json(profiles.Select(p => new WizardProfileOptionJson(p.Id, p.Name)));
    }

    private string GetReceiverArea()
    {
        if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
        {
            return "CompanyEmployee";
        }

        return "CompanyAdmin";
    }

    private async Task<NewSubscriberWizardPath> InferWizardPathAsync(Client client, int networkId)
    {
        if (!client.ReceiverId.HasValue)
        {
            return NewSubscriberWizardPath.TowerDirect;
        }

        bool hasPeers = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.ReceiverId == client.ReceiverId && c.Id != client.Id && c.NetworkId == networkId);

        return hasPeers
            ? NewSubscriberWizardPath.SharedSelectReceiver
            : NewSubscriberWizardPath.ExistingReceiverFromList;
    }

    private static string GetPathLabel(NewSubscriberWizardPath path) => path switch
    {
        NewSubscriberWizardPath.TowerDirect => "من البرج مباشرة (بدون لاقط)",
        NewSubscriberWizardPath.PrivateNewReceiver => "لاقط خاص (جديد)",
        NewSubscriberWizardPath.SharedSelectReceiver => "لاقط مشترك",
        NewSubscriberWizardPath.ExistingReceiverFromList => "لاقط من القائمة",
        _ => "—"
    };

    private async Task<List<ReceiverPickOption>> LoadReceiverOptionsAsync(int networkId)
    {
        var receivers = await _context.Receivers
            .AsNoTracking()
            .Where(r => r.NetworkId == networkId && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                Name = r.Name ?? $"#{r.Id}",
                SectorName = r.Sector.Name ?? "—",
                ServerName = r.Sector.MikroTikServer != null ? r.Sector.MikroTikServer.Name! : "—",
                SubscriberCount = r.Clients.Count
            })
            .ToListAsync();

        return receivers.Select(r => new ReceiverPickOption
        {
            Id = r.Id,
            Name = r.Name,
            SectorName = r.SectorName,
            ServerName = r.ServerName,
            IsShared = r.SubscriberCount > 1
        }).ToList();
    }

    private IQueryable<Sector> SharedSectorQuery(int networkId, int? serverId)
    {
        IQueryable<Sector> query = _context.Sectors
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Where(s => s.NetworkId == networkId || (s.MikroTikServer != null && s.MikroTikServer.NetworkId == networkId));

        if (serverId is > 0)
        {
            query = query.Where(s => s.MikroTikServerId == serverId.Value);
        }

        return query;
    }

    private IQueryable<Receiver> SharedReceiverQuery(int networkId, int? serverId, int? sectorId)
    {
        IQueryable<Receiver> query = _context.Receivers
            .AsNoTracking()
            .Where(r =>
                r.NetworkId == networkId
                || r.Sector.NetworkId == networkId
                || (r.Sector.MikroTikServer != null && r.Sector.MikroTikServer.NetworkId == networkId));

        if (serverId is > 0)
        {
            query = query.Where(r => r.Sector.MikroTikServerId == serverId.Value);
        }

        if (sectorId is > 0)
        {
            query = query.Where(r => r.SectorId == sectorId.Value);
        }

        return query;
    }

    private async Task<NewSubscriberWizardSharedReceiverViewModel> BuildSharedReceiverViewModelAsync(
        int networkId,
        int? serverId,
        int? sectorId)
    {
        List<SelectListItem> servers = await _context.MikroTikServers
            .AsNoTracking()
            .Where(s => s.NetworkId == networkId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
            .ToListAsync();

        List<WizardSectorLookup> sectors = await SharedSectorQuery(networkId, null)
            .OrderBy(s => s.Name)
            .Select(s => new WizardSectorLookup
            {
                Id = s.Id,
                Name = s.Name ?? ("#" + s.Id),
                MikroTikServerId = s.MikroTikServerId
            })
            .ToListAsync();

        List<ReceiverPickOption> receivers = await SharedReceiverQuery(networkId, null, null)
            .OrderBy(r => r.Name)
            .Select(r => new ReceiverPickOption
            {
                Id = r.Id,
                Name = r.Name ?? $"#{r.Id}",
                SectorName = r.Sector.Name ?? "—",
                ServerName = r.Sector.MikroTikServer != null ? r.Sector.MikroTikServer.Name! : "—",
                MikroTikServerId = r.Sector.MikroTikServerId,
                SectorId = r.SectorId,
                IsShared = true,
                IsActive = r.IsActive
            })
            .ToListAsync();

        return new NewSubscriberWizardSharedReceiverViewModel
        {
            MikroTikServerId = serverId,
            SectorId = sectorId,
            Servers = servers,
            Sectors = sectors,
            Receivers = receivers
        };
    }

    private async Task LoadSubscriberViewDataAsync(
        int networkId,
        NewSubscriberWizardState state,
        int? selectedServerId)
    {
        int? serverId = selectedServerId ?? state.MikroTikServerId;
        IReadOnlyList<ClientFormProfileOption> profiles = serverId.HasValue
            ? await _formLookup.GetProfilesByServerAsync(serverId.Value, networkId)
            : Array.Empty<ClientFormProfileOption>();

        ViewBag.Profiles = new SelectList(profiles, nameof(ClientFormProfileOption.Id), nameof(ClientFormProfileOption.Name));
        ViewBag.ProfileSelectEnabled = serverId.HasValue;
        ViewBag.WizardPathLabel = GetPathLabel(state.Path);
        ViewBag.RequireMikroTikServer = state.Path == NewSubscriberWizardPath.TowerDirect;
        ViewBag.LockMikroTikServer = state.Path != NewSubscriberWizardPath.TowerDirect && state.MikroTikServerId.HasValue;
        ViewBag.SelectedSectorName = null;
        ViewBag.SelectedReceiverName = null;
        ViewBag.CanEditVipBenefits = !IsEmployeeArea;
        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId);
        int companyId = selectedNetwork?.ParentNetworkId ?? networkId;
        ViewBag.CompanyVipDefaultPercent = await _context.Networks.AsNoTracking()
            .Where(n => n.Id == companyId)
            .Select(n => n.VipDiscountPercent)
            .FirstOrDefaultAsync();

        if (state.ReceiverId is > 0)
        {
            var linkMeta = await _context.Receivers
                .AsNoTracking()
                .Where(r => r.Id == state.ReceiverId.Value && r.NetworkId == networkId)
                .Select(r => new { ReceiverName = r.Name, SectorName = r.Sector.Name })
                .FirstOrDefaultAsync();
            if (linkMeta != null)
            {
                ViewBag.SelectedReceiverName = linkMeta.ReceiverName;
                ViewBag.SelectedSectorName = linkMeta.SectorName;
            }
        }

        if (state.Path != NewSubscriberWizardPath.TowerDirect && state.MikroTikServerId.HasValue)
        {
            ViewBag.MikroTikServers = new SelectList(
                await _context.MikroTikServers.AsNoTracking()
                    .Where(s => s.Id == state.MikroTikServerId)
                    .Select(s => new { s.Id, s.Name })
                    .ToListAsync(),
                "Id",
                "Name",
                state.MikroTikServerId);
            return;
        }

        ViewBag.MikroTikServers = new SelectList(
            await _context.MikroTikServers.AsNoTracking()
                .Where(s => s.NetworkId == networkId && s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(),
            "Id",
            "Name",
            serverId);
    }
}
