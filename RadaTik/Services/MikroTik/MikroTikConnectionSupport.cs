using RadaTik.Helpers;
using RadaTik.Models;
using tik4net;

namespace RadaTik.Services.MikroTik;

/// <summary>إدارة اتصالات MikroTik مع إعادة المحاولة.</summary>
public sealed class MikroTikConnectionSupport(ILogger<MikroTikConnectionSupport> logger)
{
    private readonly ILogger<MikroTikConnectionSupport> _logger = logger;

    public ITikConnection CreateConnectionWithRetry(MikroTikServer server, int maxRetries = 3)
    {
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

                connection = ConnectionFactory.OpenConnection(
                    TikConnectionType.Api,
                    server.Host,
                    server.Port,
                    server.User,
                    server.Pass);

                ITikCommand testCmd = connection.CreateCommand("/system/resource/print");
                testCmd.ExecuteList();

                return connection;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "فشلت محاولة الاتصال {Attempt}/{Max}", attempt, maxRetries);

                try
                {
                    connection?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogDebug(disposeEx, "تعذر التخلص من اتصال MikroTik الفاشل.");
                }

                connection = null;

                // لا فائدة من إعادة المحاولة عند خطأ اسم المستخدم/كلمة المرور.
                if (MikroTikErrorFormatter.IsAuthFailure(ex))
                {
                    break;
                }

                if (attempt < maxRetries)
                {
                    int delay = (int)Math.Pow(2, attempt) * 500;
                    Thread.Sleep(delay);
                }
            }
        }

        throw new InvalidOperationException(
            $"فشل الاتصال بالخادم {server.Host} بعد {maxRetries} محاولات",
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
                connection = CreateConnectionWithRetry(server, maxRetries: 3);
                T result = operation(connection);
                connection.Dispose();
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "فشلت عملية MikroTik في المحاولة {Attempt}/{Max}", attempt, maxRetries);

                try
                {
                    connection?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogDebug(disposeEx, "تعذر التخلص من اتصال MikroTik بعد فشل العملية.");
                }

                if (attempt < maxRetries && IsTransient(ex))
                {
                    int delay = (int)Math.Pow(2, attempt) * 1000;
                    await Task.Delay(delay);
                    continue;
                }

                throw;
            }
        }

        throw new InvalidOperationException($"فشلت العملية بعد {maxRetries} محاولات", lastException);
    }

    private static bool IsTransient(Exception ex) =>
        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("transport", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
}
