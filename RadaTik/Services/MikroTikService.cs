using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Dtos.MikroTik;
using RadaTik.Models;
using RadaTik.ViewModels.MikroTikServers;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using RadaTik.Services.MikroTik;
using tik4net;

namespace RadaTik.Services
{
    public partial class MikroTikService : IMikroTikProfilesService, Services.MikroTik.IMikroTikSectorService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MikroTikService> _logger;
        private readonly MikroTikConnectionSupport _connection;

        public MikroTikService(
            ApplicationDbContext context,
            ILogger<MikroTikService> logger,
            MikroTikConnectionSupport connection)
        {
            _context = context;
            _logger = logger;
            _connection = connection;
        }

        // ===== دوال البروفايلات =====

        /// <summary>
        /// جلب جميع البروفايلات من MikroTik مع تفاصيلها
        /// </summary>
        public async Task<List<MikroTikProfileInfo>> GetProfilesFromMikroTik(int serverId)
        {
            _logger.LogInformation($"🔍 جلب البروفايلات من المايكروتك للخادم {serverId}");

            MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
            if (server == null)
            {
                throw new InvalidOperationException("الخادم غير موجود");
            }

            List<MikroTikProfileInfo> profiles = new List<MikroTikProfileInfo>();

            try
            {
                using (ITikConnection connection = ConnectionFactory.OpenConnection(
                    TikConnectionType.Api,
                    server.Host,
                    server.Port,
                    server.User,
                    server.Pass))
                {
                    // جلب جميع البروفايلات من قسم PPP
                    ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
                    IEnumerable<ITikReSentence> profileRows = profileCmd.ExecuteList();

                    foreach (ITikReSentence? row in profileRows)
                    {
                        MikroTikProfileInfo profile = new MikroTikProfileInfo
                        {
                            Id = MikroTikApiSupport.GetSafeValue(row, ".id"),
                            Name = MikroTikApiSupport.GetSafeValue(row, "name"),
                            LocalAddress = MikroTikApiSupport.GetSafeValue(row, "local-address"),
                            RemoteAddress = MikroTikApiSupport.GetSafeValue(row, "remote-address"),
                            RateLimit = MikroTikApiSupport.GetSafeValue(row, "rate-limit"),
                            OnlyOne = MikroTikApiSupport.GetSafeValue(row, "only-one") == "yes",
                            Service = MikroTikApiSupport.GetSafeValue(row, "service"),
                            IsDisabled = MikroTikApiSupport.GetSafeValue(row, "disabled") == "true"
                        };

                        // التحقق مما إذا كان البروفايل موجوداً في قاعدة البيانات
                        Profile? dbProfile = await _context.Profiles
                            .FirstOrDefaultAsync(p => p.Name == profile.Name && p.MikroTikServerId == serverId);

                        profile.ExistsInDatabase = dbProfile != null;
                        profile.DatabaseProfileId = dbProfile?.Id;
                        profile.DatabaseProfileName = dbProfile?.Name;

                        profiles.Add(profile);
                    }

                    _logger.LogInformation($"✅ تم جلب {profiles.Count} بروفايل من المايكروتك");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ خطأ في جلب البروفايلات من المايكروتك: {ex.Message}");
                throw;
            }

            return profiles.OrderBy(p => p.Name).ToList();
        }

        // ===== دوال المزامنة =====

        // (Moved to MikroTikService.Sync.Partial.cs)

        // ===== دوال المستخدمين =====
        // (Moved to MikroTikService.Sync.Partial.cs)

        // (Moved to MikroTikService.Helpers.Partial.cs)
    }
}
