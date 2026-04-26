using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Dtos.MikroTik;
using RadTik.Models;
using RadTik.ViewModels.MikroTikServers;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using tik4net;

namespace RadTik.Services
{
    public partial class MikroTikService : IMikroTikProfilesService, IMikroTikUsersService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MikroTikService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public MikroTikService(
            ApplicationDbContext context,
            ILogger<MikroTikService> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // ===== دوال البروفايلات =====

        /// <summary>
        /// جلب جميع البروفايلات من MikroTik مع تفاصيلها
        /// </summary>
        public async Task<List<MikroTikProfileInfo>> GetProfilesFromMikroTik(int serverId)
        {
            _logger.LogInformation($"🔍 جلب البروفايلات من المايكروتك للخادم {serverId}");

            var server = await _context.MikroTikServers.FindAsync(serverId);
            if (server == null)
            {
                throw new InvalidOperationException("الخادم غير موجود");
            }

            var profiles = new List<MikroTikProfileInfo>();

            try
            {
                using (var connection = ConnectionFactory.OpenConnection(
                    TikConnectionType.Api,
                    server.Host,
                    server.Port,
                    server.User,
                    server.Pass))
                {
                    // جلب جميع البروفايلات من قسم PPP
                    var profileCmd = connection.CreateCommand("/ppp/profile/print");
                    var profileRows = profileCmd.ExecuteList();

                    foreach (var row in profileRows)
                    {
                        var profile = new MikroTikProfileInfo
                        {
                            Id = GetSafeValue(row, ".id"),
                            Name = GetSafeValue(row, "name"),
                            LocalAddress = GetSafeValue(row, "local-address"),
                            RemoteAddress = GetSafeValue(row, "remote-address"),
                            RateLimit = GetSafeValue(row, "rate-limit"),
                            OnlyOne = GetSafeValue(row, "only-one") == "yes",
                            Service = GetSafeValue(row, "service"),
                            IsDisabled = GetSafeValue(row, "disabled") == "true"
                        };

                        // التحقق مما إذا كان البروفايل موجوداً في قاعدة البيانات
                        var dbProfile = await _context.Profiles
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