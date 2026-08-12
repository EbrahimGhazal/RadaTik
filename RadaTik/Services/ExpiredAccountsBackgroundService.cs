using Microsoft.Extensions.Hosting;
using RadaTik.Data;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services
{
    /// <summary>
    /// خدمة خلفية للتحقق الدوري من الحسابات المنتهية الصلاحية
    /// </summary>
    public class ExpiredAccountsBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiredAccountsBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // التحقق مرة كل 24 ساعة

        public ExpiredAccountsBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<ExpiredAccountsBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 تم بدء خدمة التحقق من الحسابات المنتهية الصلاحية");

            try
            {
                // الانتظار قليلاً قبل البدء للتأكد من أن التطبيق جاهز
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // التطبيق تم إيقافه قبل البدء
                _logger.LogInformation("🛑 تم إيقاف خدمة التحقق من الحسابات المنتهية الصلاحية قبل البدء");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("🔄 بدء التحقق الدوري من الحسابات المنتهية الصلاحية");

                    // إنشاء scope جديد لكل عملية تحقق
                    using (IServiceScope scope = _scopeFactory.CreateScope())
                    {
                        IMikroTikPppoeUserService mikroTikService = scope.ServiceProvider.GetRequiredService<IMikroTikPppoeUserService>();
                        ExpiredAccountsResult result = await mikroTikService.CheckAndDisableExpiredAccounts();

                        if (result.Success)
                        {
                            _logger.LogInformation($"✅ {result.Message}");
                            if (result.DisabledAccounts.Count > 0)
                            {
                                _logger.LogWarning($"⚠️ تم إيقاف {result.DisabledAccounts.Count} حساب منتهي الصلاحية");
                            }
                        }
                        else
                        {
                            _logger.LogError($"❌ {result.Message}");
                        }
                    }

                    // الانتظار حتى الفحص التالي
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // التطبيق تم إيقافه
                    _logger.LogInformation("🛑 تم إيقاف خدمة التحقق من الحسابات المنتهية الصلاحية");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطأ في خدمة التحقق من الحسابات المنتهية");

                    try
                    {
                        // في حالة الخطأ، ننتظر 6 ساعات قبل المحاولة مرة أخرى
                        await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // التطبيق تم إيقافه أثناء الانتظار
                        _logger.LogInformation("🛑 تم إيقاف خدمة التحقق من الحسابات المنتهية الصلاحية");
                        break;
                    }
                }
            }

            _logger.LogInformation("🛑 تم إيقاف خدمة التحقق من الحسابات المنتهية الصلاحية");
        }
    }
}
