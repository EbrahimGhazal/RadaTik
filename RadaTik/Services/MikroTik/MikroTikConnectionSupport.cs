using System.Net.Sockets;
using RadaTik.Helpers;
using RadaTik.Models;
using tik4net;

namespace RadaTik.Services.MikroTik;

/// <summary>إدارة اتصالات MikroTik مع مهلات قصيرة وإعادة محاولة محدودة (لتفادي 504).</summary>
public sealed class MikroTikConnectionSupport(ILogger<MikroTikConnectionSupport> logger)
{
    /// <summary>مهلة إرسال أوامر API بالمللي ثانية.</summary>
    public const int DefaultSendTimeoutMs = 8_000;

    /// <summary>مهلة استقبال ردود API بالمللي ثانية.</summary>
    public const int DefaultReceiveTimeoutMs = 8_000;

    /// <summary>عدد محاولات فتح الاتصال للعمليات التفاعلية (إضافة/مزامنة).</summary>
    public const int DefaultConnectRetries = 2;

    private readonly ILogger<MikroTikConnectionSupport> _logger = logger;

    public ITikConnection CreateConnectionWithRetry(MikroTikServer server, int maxRetries = DefaultConnectRetries)
    {
        if (maxRetries < 1)
        {
            maxRetries = 1;
        }

        ITikConnection? connection = null;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "محاولة الاتصال بالخادم {Host}:{Port} ({Attempt}/{Max})",
                    server.Host,
                    server.Port,
                    attempt,
                    maxRetries);

                connection = OpenConnectionWithTimeouts(server);

                try
                {
                    ITikCommand testCmd = connection.CreateCommand("/system/resource/print");
                    testCmd.ExecuteList();
                }
                catch (Exception testEx) when (
                    MikroTikApiSupport.IsEmptyResponse(testEx)
                    || testEx.Message.Contains("permission", StringComparison.OrdinalIgnoreCase)
                    || testEx.Message.Contains("not enough", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        testEx,
                        "تم تسجيل الدخول إلى {Host} لكن أمر الفحص غير متاح — يُتابع الاتصال",
                        server.Host);
                }

                return connection;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "فشلت محاولة الاتصال {Attempt}/{Max}", attempt, maxRetries);

                TryDispose(connection);
                connection = null;

                // لا فائدة من إعادة المحاولة عند خطأ اسم المستخدم/كلمة المرور.
                if (MikroTikErrorFormatter.IsAuthFailure(ex))
                {
                    break;
                }

                // عند انتهاء مهلة TCP/رفض الاتصال: محاولة إضافية واحدة سريعة فقط دون انتظار طويل.
                if (attempt < maxRetries)
                {
                    int delayMs = IsHardConnectFailure(ex) ? 300 : (int)Math.Pow(2, attempt) * 400;
                    Thread.Sleep(delayMs);
                }
            }
        }

        throw new InvalidOperationException(
            $"فشل الاتصال بالخادم {server.Host}:{server.Port} بعد {maxRetries} محاولات (تحقق من API/الجدار الناري).",
            lastException);
    }

    public async Task<T> ExecuteWithRetry<T>(
        MikroTikServer server,
        Func<ITikConnection, T> operation,
        int maxRetries = 2)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            ITikConnection? connection = null;
            try
            {
                // لا نضاعف المحاولات: فتح اتصال واحد لكل محاولة عملية.
                connection = CreateConnectionWithRetry(server, maxRetries: 1);
                T result = operation(connection);
                connection.Dispose();
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "فشلت عملية MikroTik في المحاولة {Attempt}/{Max}", attempt, maxRetries);

                TryDispose(connection);

                if (MikroTikErrorFormatter.IsAuthFailure(ex))
                {
                    break;
                }

                if (attempt < maxRetries && IsTransient(ex))
                {
                    int delayMs = IsHardConnectFailure(ex) ? 300 : (int)Math.Pow(2, attempt) * 500;
                    await Task.Delay(delayMs);
                    continue;
                }

                throw;
            }
        }

        throw new InvalidOperationException($"فشلت العملية بعد {maxRetries} محاولات", lastException);
    }

    private static ITikConnection OpenConnectionWithTimeouts(MikroTikServer server)
    {
        ITikConnection connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
        connection.SendTimeout = DefaultSendTimeoutMs;
        connection.ReceiveTimeout = DefaultReceiveTimeoutMs;
        connection.Open(server.Host, server.Port, server.User, server.Pass);
        return connection;
    }

    private static void TryDispose(ITikConnection? connection)
    {
        if (connection is null)
        {
            return;
        }

        try
        {
            connection.Dispose();
        }
        catch
        {
            // ignore dispose errors on failed sockets
        }
    }

    public static bool IsHardConnectFailure(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException or IOException)
            {
                return true;
            }

            string message = current.Message ?? string.Empty;
            if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase)
                || message.Contains("فشل الاتصال بالخادم", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTransient(Exception ex) =>
        IsHardConnectFailure(ex) ||
        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("transport", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase);
}
