using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RadaTik.Services;

/// <summary>إرسال واتساب عبر واجهة HTTP قابلة للتهيئة، وتلغرام عبر Bot API.</summary>
public sealed class RenewalReminderOutboundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RenewalReminderOutboundService> _logger;

    public RenewalReminderOutboundService(IHttpClientFactory httpClientFactory, ILogger<RenewalReminderOutboundService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error)> SendTelegramAsync(string botToken, string chatId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            return (false, "بوت تلغرام أو معرّف المحادثة غير مضبوط.");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var url = $"https://api.telegram.org/bot{Uri.EscapeDataString(botToken.Trim())}/sendMessage";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["chat_id"] = chatId.Trim(),
            ["text"] = text,
            ["disable_web_page_preview"] = "true"
        });

        try
        {
            using var resp = await client.PostAsync(url, content, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram send failed: {Status} {Body}", (int)resp.StatusCode, body);
                return (false, $"Telegram HTTP {(int)resp.StatusCode}");
            }

            try
            {
                var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                    return (true, null);

                var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : body;
                return (false, desc ?? "فشل إرسال تلغرام");
            }
            catch
            {
                return resp.IsSuccessStatusCode ? (true, null) : (false, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram send exception");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// POST JSON. الافتراضي: <c>{"phone":"...","message":"..."}</c>.
    /// إن وُجد <paramref name="bodyTemplate"/> يُبنى الجسم منه: <c>{phone}</c> = الرقم، <c>{message}</c> = قيمة JSON للنص (مُقتبسة ومهربّة)، مثال:
    /// <c>{"to":"{phone}","text":{message}}</c>
    /// </summary>
    public async Task<(bool Ok, string? Error)> SendWhatsAppViaWebhookAsync(
        string apiUrl,
        string? authorizationHeaderValue,
        string phoneDigits,
        string message,
        string? bodyTemplate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return (false, "لم يُضبط عنوان واجهة واتساب.");

        string payload;
        try
        {
            payload = BuildWhatsAppJsonBody(bodyTemplate, phoneDigits, message);
        }
        catch (Exception ex)
        {
            return (false, "قالب JSON غير صالح: " + ex.Message);
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(45);

        using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl.Trim());
        req.Headers.TryAddWithoutValidation("User-Agent", "RadaTik-RenewalReminder/1.0");
        // نوع الوسائط يجب أن يكون بدون معاملات؛ الترميز يُمرَّر عبر Encoding.UTF8.
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        if (!string.IsNullOrWhiteSpace(authorizationHeaderValue))
        {
            // إن بدأ بـ Bearer أو Basic نمرّره كاملاً، وإلا نفترض Bearer
            var v = authorizationHeaderValue.Trim();
            if (v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                req.Headers.TryAddWithoutValidation("Authorization", v);
            else
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", v);
        }

        try
        {
            using var resp = await client.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("WhatsApp webhook failed: {Status} {Body}", (int)resp.StatusCode, body);
                return (false, FormatWhatsAppProviderError((int)resp.StatusCode, body));
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp webhook exception");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, string? Error)> VerifyTelegramBotTokenAsync(string botToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken))
            return (false, "رمز البوت فارغ.");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        var url = $"https://api.telegram.org/bot{Uri.EscapeDataString(botToken.Trim())}/getMe";

        try
        {
            using var resp = await client.GetAsync(url, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                return (true, null);

            var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : body;
            return (false, desc ?? "فشل التحقق");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string BuildWhatsAppJsonBody(string? bodyTemplate, string phoneDigits, string message)
    {
        if (string.IsNullOrWhiteSpace(bodyTemplate))
        {
            return JsonSerializer.Serialize(new WhatsAppWebhookPayload
            {
                Phone = phoneDigits,
                Message = message
            });
        }

        var messageJson = JsonSerializer.Serialize(message);
        var json = bodyTemplate.Trim()
            .Replace("{phone}", phoneDigits, StringComparison.Ordinal)
            .Replace("{message}", messageJson, StringComparison.Ordinal);

        using (JsonDocument.Parse(json))
        {
        }

        return json;
    }

    /// <summary>يعرض للمستخدم سبب الرفض من المزوّد (JSON أو نص مختصر).</summary>
    private static string FormatWhatsAppProviderError(int statusCode, string? responseBody)
    {
        if (LooksLikeHtmlPage(responseBody))
        {
            return $"واتساب HTTP {statusCode}: الخادم أرجع صفحة HTML (مثل صفحة واتساب أو المتصفح)، وليس واجهة API تستقبل JSON. " +
                   "استخدم عنوان HTTPS يزودك به **مزوّد إرسال واتساب** (Business API، بوابة محلية، إلخ)، وليس رابط wa.me أو web.whatsapp.com أو أي صفحة تُفتح في المتصفح.";
        }

        var detail = TryReadCommonJsonErrorFields(responseBody);
        if (!string.IsNullOrEmpty(detail))
            return $"واتساب HTTP {statusCode}: {detail}";

        var raw = (responseBody ?? "").Trim();
        if (raw.Length == 0)
            return $"واتساب HTTP {statusCode} (لا يوجد نص في الاستجابة).";

        const int max = 450;
        if (raw.Length > max)
            raw = raw[..max] + "…";
        return $"واتساب HTTP {statusCode}: {raw}";
    }

    private static bool LooksLikeHtmlPage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        var s = responseBody.TrimStart();
        if (s.Length >= 9 && s.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            return true;
        if (s.Length >= 5 && s.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            return true;
        // استجابة مختلطة أو مسافة قبل العلامة
        var head = s.Length > 800 ? s.AsSpan(0, 800) : s.AsSpan();
        return head.Contains("<html", StringComparison.OrdinalIgnoreCase)
               || head.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadCommonJsonErrorFields(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            foreach (var name in new[] { "message", "error", "error_message", "detail", "description", "msg" })
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var el))
                {
                    if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("message", out var inner))
                    {
                        var s2 = inner.ValueKind == JsonValueKind.String ? inner.GetString() : inner.ToString();
                        if (!string.IsNullOrWhiteSpace(s2))
                            return s2.Trim();
                    }

                    var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s.Trim();
                }
            }
        }
        catch
        {
            // ليس JSON
        }

        return null;
    }

    private sealed class WhatsAppWebhookPayload
    {
        [JsonPropertyName("phone")]
        public string Phone { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}
