using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.ViewModels.CompanyAdmin;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class RenewalRemindersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RenewalReminderOutboundService _outbound;

    public RenewalRemindersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RenewalReminderOutboundService outbound)
    {
        _context = context;
        _userManager = userManager;
        _outbound = outbound;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "تذكير تجديد المشتركين";

        var user = await _userManager.GetUserAsync(User);
        var selectedId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToRoute("networkManager-network", new { action = "Index" });
        }

        var effectiveId = await ResolveEffectiveCompanyNetworkIdAsync(selectedId.Value);
        var net = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveId);
        if (net == null)
            return NotFound();

        var row = await _context.NetworkClientRenewalReminderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.NetworkId == effectiveId);

        var vm = row == null
            ? new RenewalRemindersPageViewModel { EffectiveCompanyNetworkId = effectiveId, EffectiveCompanyNetworkName = net.Name }
            : new RenewalRemindersPageViewModel
            {
                EffectiveCompanyNetworkId = effectiveId,
                EffectiveCompanyNetworkName = net.Name,
                IsEnabled = row.IsEnabled,
                RemindDaysBefore5 = row.RemindDaysBefore5,
                RemindDaysBefore4 = row.RemindDaysBefore4,
                RemindDaysBefore3 = row.RemindDaysBefore3,
                MessageTemplate = row.MessageTemplate,
                SendWhatsApp = row.SendWhatsApp,
                WhatsAppDisplayNumber = row.WhatsAppDisplayNumber,
                WhatsAppVerifiedAt = row.WhatsAppVerifiedAt,
                WhatsAppApiUrl = row.WhatsAppApiUrl,
                WhatsAppApiAuthorizationHeader = row.WhatsAppApiAuthorizationHeader,
                WhatsAppApiBodyTemplate = row.WhatsAppApiBodyTemplate,
                WhatsAppTestPhone = row.WhatsAppTestPhone,
                SendTelegram = row.SendTelegram,
                TelegramBotToken = row.TelegramBotToken,
                TelegramVerifiedAt = row.TelegramVerifiedAt,
                TelegramTestChatId = row.TelegramTestChatId
            };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RenewalRemindersPageViewModel vm)
    {
        ViewData["Title"] = "تذكير تجديد المشتركين";

        var user = await _userManager.GetUserAsync(User);
        var selectedId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToRoute("networkManager-network", new { action = "Index" });
        }

        var effectiveId = await ResolveEffectiveCompanyNetworkIdAsync(selectedId.Value);
        if (effectiveId != vm.EffectiveCompanyNetworkId)
            return BadRequest();

        if (!await _context.Networks.AnyAsync(n => n.Id == effectiveId))
            return NotFound();

        if (!ModelState.IsValid)
        {
            vm.EffectiveCompanyNetworkName = await _context.Networks.AsNoTracking()
                .Where(n => n.Id == effectiveId).Select(n => n.Name).FirstOrDefaultAsync();
            return View(vm);
        }

        var row = await _context.NetworkClientRenewalReminderSettings
            .FirstOrDefaultAsync(s => s.NetworkId == effectiveId);

        if (row == null)
        {
            row = new NetworkClientRenewalReminderSettings { NetworkId = effectiveId };
            _context.NetworkClientRenewalReminderSettings.Add(row);
        }

        MapVmToRow(vm, row);
        row.UpdatedAtUtc = DateTime.UtcNow;
        if (row.CreatedAtUtc == default)
            row.CreatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حفظ إعدادات التذكير.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>تأكيد رقم واتساب يدوياً بعد مراجعته.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmWhatsAppNumber(int EffectiveCompanyNetworkId, string? WhatsAppDisplayNumber)
    {
        var user = await _userManager.GetUserAsync(User);
        var selectedId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedId.HasValue)
            return RedirectToRoute("networkManager-network", new { action = "Index" });

        var effectiveId = await ResolveEffectiveCompanyNetworkIdAsync(selectedId.Value);
        if (effectiveId != EffectiveCompanyNetworkId)
            return BadRequest();

        if (string.IsNullOrWhiteSpace(WhatsAppDisplayNumber))
        {
            TempData["Error"] = "أدخل رقم واتساب أولاً.";
            return RedirectToAction(nameof(Index));
        }

        var row = await GetOrCreateSettingsRowAsync(effectiveId);
        row.WhatsAppDisplayNumber = WhatsAppDisplayNumber.Trim();
        row.WhatsAppVerifiedAt = DateTime.UtcNow;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تأكيد رقم واتساب. الإرسال الآلي يتطلب ضبط واجهة HTTP إن وُجدت.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTelegramBot(int EffectiveCompanyNetworkId, string? TelegramBotToken)
    {
        var user = await _userManager.GetUserAsync(User);
        var selectedId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedId.HasValue)
            return RedirectToRoute("networkManager-network", new { action = "Index" });

        var effectiveId = await ResolveEffectiveCompanyNetworkIdAsync(selectedId.Value);
        if (effectiveId != EffectiveCompanyNetworkId)
            return BadRequest();

        var existing = await _context.NetworkClientRenewalReminderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.NetworkId == effectiveId);
        var token = CoalesceTrim(TelegramBotToken, existing?.TelegramBotToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "أدخل رمز بوت تلغرام أو احفظه أولاً.";
            return RedirectToAction(nameof(Index));
        }

        var (ok, err) = await _outbound.VerifyTelegramBotTokenAsync(token, HttpContext.RequestAborted);
        if (!ok)
        {
            TempData["Error"] = "فشل التحقق من البوت: " + (err ?? "");
            return RedirectToAction(nameof(Index));
        }

        var row = await GetOrCreateSettingsRowAsync(effectiveId);
        row.TelegramBotToken = token.Trim();
        row.TelegramVerifiedAt = DateTime.UtcNow;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم التحقق من بوت تلغرام بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestTelegram(int EffectiveCompanyNetworkId, string? TelegramBotToken, string? TelegramTestChatId, string? MessageTemplate)
    {
        var user = await _userManager.GetUserAsync(User);
        var selectedId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedId.HasValue)
            return RedirectToRoute("networkManager-network", new { action = "Index" });

        var effectiveId = await ResolveEffectiveCompanyNetworkIdAsync(selectedId.Value);
        if (effectiveId != EffectiveCompanyNetworkId)
            return BadRequest();

        var row = await _context.NetworkClientRenewalReminderSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.NetworkId == effectiveId);
        var token = CoalesceTrim(TelegramBotToken, row?.TelegramBotToken);
        var chatId = CoalesceTrim(TelegramTestChatId, row?.TelegramTestChatId);
        var template = CoalesceTrim(MessageTemplate, row?.MessageTemplate) ?? new RenewalRemindersPageViewModel().MessageTemplate;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            TempData["Error"] = "أدخل رمز البوت ومعرّف المحادثة للاختبار.";
            return RedirectToAction(nameof(Index));
        }

        var sample = RenewalReminderMessageFormatter.Format(
            template,
            "اسم تجريبي",
            "باقة تجريبية",
            150000m,
            3,
            DateTime.Today.AddDays(3));

        var (ok, err) = await _outbound.SendTelegramAsync(token, chatId, sample, HttpContext.RequestAborted);
        TempData[ok ? "Success" : "Error"] = ok ? "تم إرسال رسالة الاختبار عبر تلغرام." : ("فشل الإرسال: " + (err ?? ""));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestWhatsApp(
        int EffectiveCompanyNetworkId,
        string? WhatsAppApiUrl,
        string? WhatsAppApiAuthorizationHeader,
        string? WhatsAppApiBodyTemplate,
        string? WhatsAppTestPhone,
        string? MessageTemplate)
    {
        var user = await _userManager.GetUserAsync(User);
        var selectedId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedId.HasValue)
            return RedirectToRoute("networkManager-network", new { action = "Index" });

        var effectiveId = await ResolveEffectiveCompanyNetworkIdAsync(selectedId.Value);
        if (effectiveId != EffectiveCompanyNetworkId)
            return BadRequest();

        var row = await _context.NetworkClientRenewalReminderSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.NetworkId == effectiveId);
        var apiUrl = CoalesceTrim(WhatsAppApiUrl, row?.WhatsAppApiUrl);
        var auth = CoalesceTrim(WhatsAppApiAuthorizationHeader, row?.WhatsAppApiAuthorizationHeader);
        var bodyTpl = CoalesceTrim(WhatsAppApiBodyTemplate, row?.WhatsAppApiBodyTemplate);
        var template = CoalesceTrim(MessageTemplate, row?.MessageTemplate) ?? new RenewalRemindersPageViewModel().MessageTemplate;

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            TempData["Error"] = "أدخل عنوان واجهة إرسال واتساب أولاً.";
            return RedirectToAction(nameof(Index));
        }

        var phone = new string((WhatsAppTestPhone ?? row?.WhatsAppTestPhone ?? "").Where(char.IsDigit).ToArray());
        if (phone.Length < 8)
        {
            TempData["Error"] = "أدخل رقم اختبار واتساب صالحاً (أرقام فقط).";
            return RedirectToAction(nameof(Index));
        }

        var sample = RenewalReminderMessageFormatter.Format(
            template,
            "اسم تجريبي",
            "باقة تجريبية",
            150000m,
            3,
            DateTime.Today.AddDays(3));

        var (ok, err) = await _outbound.SendWhatsAppViaWebhookAsync(
            apiUrl,
            auth,
            phone,
            sample,
            bodyTpl,
            HttpContext.RequestAborted);

        TempData[ok ? "Success" : "Error"] = ok ? "تم طلب إرسال رسالة الاختبار عبر واجهة واتساب." : ("فشل الإرسال: " + (err ?? ""));
        return RedirectToAction(nameof(Index));
    }

    private static string? CoalesceTrim(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a))
            return a.Trim();
        return string.IsNullOrWhiteSpace(b) ? null : b.Trim();
    }

    private async Task<int> ResolveEffectiveCompanyNetworkIdAsync(int selectedNetworkId)
    {
        var n = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == selectedNetworkId);
        return n?.ParentNetworkId ?? selectedNetworkId;
    }

    private async Task<NetworkClientRenewalReminderSettings> GetOrCreateSettingsRowAsync(int effectiveCompanyNetworkId)
    {
        var row = await _context.NetworkClientRenewalReminderSettings
            .FirstOrDefaultAsync(s => s.NetworkId == effectiveCompanyNetworkId);
        if (row != null)
            return row;

        row = new NetworkClientRenewalReminderSettings
        {
            NetworkId = effectiveCompanyNetworkId,
            MessageTemplate = new RenewalRemindersPageViewModel().MessageTemplate,
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.NetworkClientRenewalReminderSettings.Add(row);
        return row;
    }

    private static void MapVmToRow(RenewalRemindersPageViewModel vm, NetworkClientRenewalReminderSettings row)
    {
        row.IsEnabled = vm.IsEnabled;
        row.RemindDaysBefore5 = vm.RemindDaysBefore5;
        row.RemindDaysBefore4 = vm.RemindDaysBefore4;
        row.RemindDaysBefore3 = vm.RemindDaysBefore3;
        row.MessageTemplate = string.IsNullOrWhiteSpace(vm.MessageTemplate)
            ? (string.IsNullOrWhiteSpace(row.MessageTemplate) ? new RenewalRemindersPageViewModel().MessageTemplate : row.MessageTemplate)
            : vm.MessageTemplate.Trim();
        row.SendWhatsApp = vm.SendWhatsApp;
        row.WhatsAppDisplayNumber = string.IsNullOrWhiteSpace(vm.WhatsAppDisplayNumber) ? null : vm.WhatsAppDisplayNumber.Trim();
        row.WhatsAppApiUrl = string.IsNullOrWhiteSpace(vm.WhatsAppApiUrl) ? null : vm.WhatsAppApiUrl.Trim();
        row.WhatsAppApiAuthorizationHeader = string.IsNullOrWhiteSpace(vm.WhatsAppApiAuthorizationHeader) ? null : vm.WhatsAppApiAuthorizationHeader.Trim();
        row.WhatsAppApiBodyTemplate = string.IsNullOrWhiteSpace(vm.WhatsAppApiBodyTemplate) ? null : vm.WhatsAppApiBodyTemplate.Trim();
        row.WhatsAppTestPhone = string.IsNullOrWhiteSpace(vm.WhatsAppTestPhone) ? null : vm.WhatsAppTestPhone.Trim();
        row.SendTelegram = vm.SendTelegram;
        if (!string.IsNullOrWhiteSpace(vm.TelegramBotToken))
            row.TelegramBotToken = vm.TelegramBotToken.Trim();
        row.TelegramTestChatId = string.IsNullOrWhiteSpace(vm.TelegramTestChatId) ? null : vm.TelegramTestChatId.Trim();
    }
}
