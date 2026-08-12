using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services;

/// <summary>
/// يفحص المشتركين الذين تبقى على انتهاء اشتراكهم 3 أو 4 أو 5 أيام ويرسل تذكيراً عبر واتساب/تلغرام حسب إعدادات الشركة.
/// </summary>
public sealed class ClientRenewalRemindersBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClientRenewalRemindersBackgroundService> _logger;

    public ClientRenewalRemindersBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ClientRenewalRemindersBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Client renewal reminders run failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(3), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outbound = scope.ServiceProvider.GetRequiredService<RenewalReminderOutboundService>();

        var today = DateTime.Today;
        var settingsList = await db.NetworkClientRenewalReminderSettings
            .AsNoTracking()
            .Where(s => s.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var settings in settingsList)
        {
            var mainNetworkId = settings.NetworkId;
            var networkIds = await db.Networks
                .AsNoTracking()
                .Where(n => n.Id == mainNetworkId || n.ParentNetworkId == mainNetworkId)
                .Select(n => n.Id)
                .ToListAsync(cancellationToken);

            if (networkIds.Count == 0)
                continue;

            var clients = await db.Clients
                .AsNoTracking()
                .Where(c =>
                    c.NetworkId.HasValue &&
                    networkIds.Contains(c.NetworkId.Value) &&
                    c.IsActive &&
                    c.AccountExpirationDate.HasValue)
                .Include(c => c.Profile)
                .ToListAsync(cancellationToken);

            foreach (var client in clients)
            {
                var exp = client.AccountExpirationDate!.Value.Date;
                if (exp <= today)
                    continue;

                var daysLeft = (int)(exp - today).TotalDays;
                if (daysLeft is not (3 or 4 or 5))
                    continue;

                bool dayEnabled = daysLeft switch
                {
                    5 => settings.RemindDaysBefore5,
                    4 => settings.RemindDaysBefore4,
                    3 => settings.RemindDaysBefore3,
                    _ => false
                };
                if (!dayEnabled)
                    continue;

                var profileName = client.Profile?.Name ?? client.ProfileDisplayName ?? "";
                var amount = client.Profile?.PriceWithVAT ?? 0m;

                var text = RenewalReminderMessageFormatter.Format(
                    settings.MessageTemplate,
                    client.Name,
                    profileName,
                    amount,
                    daysLeft,
                    exp);

                if (settings.SendWhatsApp && settings.WhatsAppVerifiedAt.HasValue &&
                    !string.IsNullOrWhiteSpace(settings.WhatsAppApiUrl))
                {
                    var phoneDigits = DigitsOnly(client.PhoneNumber);
                    if (phoneDigits.Length > 0)
                    {
                        await TrySendAndLogAsync(
                            db,
                            client.Id,
                            mainNetworkId,
                            exp,
                            (byte)daysLeft,
                            RenewalReminderChannel.WhatsApp,
                            async ct =>
                            {
                                var r = await outbound.SendWhatsAppViaWebhookAsync(
                                    settings.WhatsAppApiUrl!,
                                    settings.WhatsAppApiAuthorizationHeader,
                                    phoneDigits,
                                    text,
                                    settings.WhatsAppApiBodyTemplate,
                                    ct);
                                return r;
                            },
                            cancellationToken);
                    }
                }

                if (settings.SendTelegram && settings.TelegramVerifiedAt.HasValue &&
                    !string.IsNullOrWhiteSpace(settings.TelegramBotToken) &&
                    !string.IsNullOrWhiteSpace(client.TelegramChatId))
                {
                    await TrySendAndLogAsync(
                        db,
                        client.Id,
                        mainNetworkId,
                        exp,
                        (byte)daysLeft,
                        RenewalReminderChannel.Telegram,
                        async ct =>
                        {
                            var r = await outbound.SendTelegramAsync(
                                settings.TelegramBotToken!,
                                client.TelegramChatId!,
                                text,
                                ct);
                            return r;
                        },
                        cancellationToken);
                }
            }
        }
    }

    private static string DigitsOnly(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        return new string(s.Where(char.IsDigit).ToArray());
    }

    private async Task TrySendAndLogAsync(
        ApplicationDbContext db,
        int clientId,
        int companyNetworkId,
        DateTime expirationDate,
        byte daysBefore,
        RenewalReminderChannel channel,
        Func<CancellationToken, Task<(bool Ok, string? Error)>> send,
        CancellationToken cancellationToken)
    {
        var expDate = expirationDate.Date;

        var already = await db.ClientRenewalReminderSendLogs
            .AsNoTracking()
            .AnyAsync(l =>
                    l.ClientId == clientId &&
                    l.ExpirationDate == expDate &&
                    l.DaysBefore == daysBefore &&
                    l.Channel == channel,
                cancellationToken);
        if (already)
            return;

        (bool ok, string? err) result;
        try
        {
            result = await send(cancellationToken);
        }
        catch (Exception ex)
        {
            result = (false, ex.Message);
        }

        if (!result.ok)
        {
            _logger.LogWarning("Renewal reminder failed client {ClientId} channel {Channel}: {Err}", clientId, channel, result.err);
            return;
        }

        db.ClientRenewalReminderSendLogs.Add(new ClientRenewalReminderSendLog
        {
            ClientId = clientId,
            CompanyNetworkId = companyNetworkId,
            ExpirationDate = expDate,
            DaysBefore = daysBefore,
            Channel = channel,
            SentAtUtc = DateTime.UtcNow,
            Success = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
