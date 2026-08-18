using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.MikroTikSync;

/// <summary>
/// خدمة خلفية تستهلك طابور مزامنة MikroTik وتنفذ المهام بأسرع وقت
/// </summary>
public sealed class MikroTikSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMikroTikSyncQueue _queue;
    private readonly ILogger<MikroTikSyncBackgroundService> _logger;

    public MikroTikSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IMikroTikSyncQueue queue,
        ILogger<MikroTikSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 بدء خدمة مزامنة MikroTik التلقائية");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                MikroTikSyncJob job = await _queue.DequeueAsync(stoppingToken);
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 إيقاف خدمة مزامنة MikroTik");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في معالجة مهمة مزامنة MikroTik");
                // الاستمرار في الاستهلاك دون إعادة رمي الاستثناء
            }
        }
    }

    private async Task ProcessJobAsync(MikroTikSyncJob job, CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IMikroTikPppoeUserService mikroTikPppoe = scope.ServiceProvider.GetRequiredService<IMikroTikPppoeUserService>();
        IMikroTikProfilesService mikroTikProfilesService = scope.ServiceProvider.GetRequiredService<IMikroTikProfilesService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            if (job.EntityType == nameof(Client))
            {
                await ProcessClientJobAsync(mikroTikPppoe, context, job, cancellationToken);
            }
            else if (job.EntityType == nameof(Profile))
            {
                await ProcessProfileJobAsync(mikroTikProfilesService, context, job, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ فشل مزامنة {Entity} Id={Id} Action={Action}", job.EntityType, job.EntityId, job.Action);
        }
    }

    private async Task ProcessClientJobAsync(IMikroTikPppoeUserService mikroTikService, ApplicationDbContext context, MikroTikSyncJob job, CancellationToken cancellationToken)
    {
        if (!job.ServerId.HasValue)
        {
            _logger.LogDebug("⏭️ تجاهل Client {Id} - غير مرتبط بسيرفر MikroTik", job.EntityId);
            return;
        }

        if (job.Action == MikroTikSyncAction.Delete)
        {
            if (string.IsNullOrEmpty(job.UserName))
            {
                _logger.LogWarning("⚠️ تجاهل حذف Client {Id} - اسم المستخدم غير متوفر", job.EntityId);
                return;
            }
            await mikroTikService.DeletePPPoEUser(job.UserName, job.ServerId.Value);
            _logger.LogInformation("✅ تم حذف مستخدم MikroTik: {UserName}", job.UserName);
            return;
        }

        Client? client = await context.Clients
            .Include(c => c.Profile)
            .Include(c => c.MikroTikServer)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == job.EntityId, cancellationToken);

        if (client == null)
        {
            _logger.LogWarning("⚠️ العميل {Id} غير موجود - ربما تم حذفه", job.EntityId);
            return;
        }

        if (client.MikroTikServerId == null)
        {
            _logger.LogDebug("⏭️ تجاهل Client {Id} - غير مرتبط بسيرفر MikroTik", job.EntityId);
            return;
        }

        // ضمان تعيين ProfileName من Profile إن لم يكن محدداً
        if (string.IsNullOrEmpty(client.ProfileName) && client.Profile != null)
        {
            client.ProfileName = client.Profile.Name;
        }

        if (string.IsNullOrEmpty(client.ProfileName))
        {
            _logger.LogWarning("⚠️ تجاهل Client {Id} - اسم البروفايل غير متوفر", job.EntityId);
            return;
        }

        try
        {
            if (job.Action == MikroTikSyncAction.Add)
            {
                await mikroTikService.AddPPPoEUser(client);
                _logger.LogInformation("✅ تم إضافة مستخدم MikroTik: {UserName}", client.UserName);
            }
            else
            {
                await mikroTikService.UpdatePPPoEUser(client);
                _logger.LogInformation("✅ تم تحديث مستخدم MikroTik: {UserName}", client.UserName);
            }
        }
        catch (Exception ex) when (MikroTikApiSupport.IsAlreadyExistsMessage(ex))
        {
            // العميل مُزامن مسبقاً (مثلاً من الواجهة مباشرة) - تُعتبر ناجحة
            _logger.LogDebug("⏭️ العميل {UserName} مُزامن مسبقاً في MikroTik", client.UserName);
        }
    }

    private async Task ProcessProfileJobAsync(IMikroTikProfilesService mikroTikService, ApplicationDbContext context, MikroTikSyncJob job, CancellationToken cancellationToken)
    {
        if (job.Action == MikroTikSyncAction.Delete)
        {
            if (!job.ServerId.HasValue || string.IsNullOrEmpty(job.ProfileName))
            {
                _logger.LogWarning("⚠️ تجاهل حذف Profile {Id} - بيانات غير كافية", job.EntityId);
                return;
            }
            await mikroTikService.DeleteProfileFromMikroTik(job.ServerId.Value, job.ProfileName);
            _logger.LogInformation("✅ تم حذف بروفايل MikroTik: {ProfileName}", job.ProfileName);
            return;
        }

        Profile? profile = await context.Profiles
            .Include(p => p.MikroTikServer)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == job.EntityId, cancellationToken);

        if (profile == null)
        {
            _logger.LogWarning("⚠️ البروفايل {Id} غير موجود - ربما تم حذفه", job.EntityId);
            return;
        }

        try
        {
            if (job.Action == MikroTikSyncAction.Add)
            {
                await mikroTikService.AddProfileToMikroTik(profile.MikroTikServerId, profile);
                _logger.LogInformation("✅ تم إضافة بروفايل MikroTik: {Name}", profile.Name);
            }
            else
            {
                await mikroTikService.UpdateProfileInMikroTik(profile.MikroTikServerId, profile);
                _logger.LogInformation("✅ تم تحديث بروفايل MikroTik: {Name}", profile.Name);
            }
        }
        catch (Exception ex) when (ex.Message.Contains("موجود مسبقاً", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("⏭️ البروفايل {Name} مُزامن مسبقاً في MikroTik", profile.Name);
        }
    }
}
