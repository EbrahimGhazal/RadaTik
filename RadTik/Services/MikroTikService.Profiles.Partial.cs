using tik4net;
using RadTik.Dtos.MikroTik;
using RadTik.Models;

namespace RadTik.Services;

public partial class MikroTikService
{
    /// <summary>
    /// جلب أسماء البروفايلات من سيرفر MikroTik محدد
    /// </summary>
    public async Task<List<string>> GetProfileNamesFromMikroTik(int serverId)
    {
        _logger.LogInformation($"🔍 جلب أسماء البروفايلات من المايكروتك للخادم {serverId}");

        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        var profileNames = new List<string>();

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
                    var profileName = GetSafeValue(row, "name");
                    if (!string.IsNullOrEmpty(profileName))
                    {
                        profileNames.Add(profileName);
                    }
                }

                _logger.LogInformation($"✅ تم جلب {profileNames.Count} بروفايل من السيرفر {server.Host}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في جلب البروفايلات من المايكروتك: {ex.Message}");
            throw;
        }

        return profileNames.OrderBy(p => p).ToList();
    }

    /// <summary>
    /// جلب معلومات بروفايل محدد من MikroTik
    /// </summary>
    public async Task<MikroTikProfileInfo> GetProfileFromMikroTik(int serverId, string profileIdOrName)
    {
        _logger.LogInformation($"🔍 جلب معلومات بروفايل من المايكروتك: {profileIdOrName}");

        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (var connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass))
            {
                var profileCmd = connection.CreateCommand("/ppp/profile/print");

                if (profileIdOrName.StartsWith("*"))
                {
                    profileCmd.AddParameter(".id", profileIdOrName);
                }
                else
                {
                    profileCmd.AddParameter("?name", profileIdOrName);
                }

                var profileRows = profileCmd.ExecuteList();
                var row = profileRows.FirstOrDefault();

                if (row == null)
                {
                    throw new InvalidOperationException($"البروفايل {profileIdOrName} غير موجود في المايكروتك");
                }

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

                _logger.LogInformation($"✅ تم جلب معلومات البروفايل {profile.Name}");
                return profile;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في جلب معلومات البروفايل: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// إضافة بروفايل إلى MikroTik مع التأكد من الإضافة
    /// </summary>
    public async Task<string> AddProfileToMikroTik(int serverId, Profile profile)
    {
        _logger.LogInformation($"🔍 إضافة بروفايل جديد إلى المايكروتك: {profile.Name}");

        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        ITikConnection? connection = null;

        try
        {
            connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass);

            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            var checkCmd = connection.CreateCommand("/ppp/profile/print");
            var allProfiles = checkCmd.ExecuteList();
            var existingProfile = allProfiles.FirstOrDefault(p => GetSafeValue(p, "name") == profile.Name);

            if (existingProfile != null)
            {
                throw new InvalidOperationException($"البروفايل {profile.Name} موجود مسبقاً في المايكروتك");
            }

            string? rateLimit = ConvertToMikroTikRateLimit(profile);

            var addCmd = connection.CreateCommand("/ppp/profile/add");
            addCmd.AddParameter("name", profile.Name);

            if (!string.IsNullOrEmpty(profile.MikroTikLocalAddress))
            {
                addCmd.AddParameter("local-address", profile.MikroTikLocalAddress);
            }

            if (!string.IsNullOrEmpty(profile.MikroTikRemoteAddress))
            {
                addCmd.AddParameter("remote-address", profile.MikroTikRemoteAddress);
            }

            if (!string.IsNullOrEmpty(rateLimit))
            {
                addCmd.AddParameter("rate-limit", rateLimit);
            }
            else if (!string.IsNullOrEmpty(profile.MikroTikRateLimit))
            {
                addCmd.AddParameter("rate-limit", profile.MikroTikRateLimit);
            }

            addCmd.AddParameter("only-one", profile.MikroTikOnlyOne ? "yes" : "no");

            addCmd.ExecuteNonQuery();
            _logger.LogInformation($"✅ تم إضافة البروفايل {profile.Name} إلى المايكروتك");

            await Task.Delay(1500);

            checkCmd = connection.CreateCommand("/ppp/profile/print");
            allProfiles = checkCmd.ExecuteList();
            var verifyProfile = allProfiles.FirstOrDefault(p => GetSafeValue(p, "name") == profile.Name);

            if (verifyProfile == null)
            {
                throw new InvalidOperationException("❌ فشل التحقق من إضافة البروفايل إلى المايكروتك");
            }

            var mikrotikId = GetSafeValue(verifyProfile, ".id");
            _logger.LogInformation($"✅ تم التحقق من إضافة البروفايل، المعرف: {mikrotikId}");

            return mikrotikId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في إضافة البروفايل إلى المايكروتك: {ex.Message}");
            throw new InvalidOperationException("خطأ في إضافة البروفايل إلى المايكروتك", ex);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// تحديث بروفايل في MikroTik
    /// </summary>
    public async Task<bool> UpdateProfileInMikroTik(int serverId, Profile profile, string? oldName = null)
    {
        _logger.LogInformation($"🔍 تحديث بروفايل في المايكروتك: {profile.Name}");

        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        ITikConnection? connection = null;

        try
        {
            connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass);

            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            var findCmd = connection.CreateCommand("/ppp/profile/print");
            var allProfiles = findCmd.ExecuteList();

            var profileName = oldName ?? profile.Name;
            var targetProfile = allProfiles.FirstOrDefault(p => GetSafeValue(p, "name") == profileName);

            if (targetProfile == null)
            {
                throw new InvalidOperationException($"البروفايل {profileName} غير موجود في المايكروتك");
            }

            var profileId = GetSafeValue(targetProfile, ".id");
            string? rateLimit = ConvertToMikroTikRateLimit(profile);

            var updateCmd = connection.CreateCommand("/ppp/profile/set");
            updateCmd.AddParameter(".id", profileId);

            if (!string.IsNullOrEmpty(profile.Name) && profile.Name != oldName)
            {
                updateCmd.AddParameter("name", profile.Name);
            }

            if (!string.IsNullOrEmpty(profile.MikroTikLocalAddress))
            {
                updateCmd.AddParameter("local-address", profile.MikroTikLocalAddress);
            }
            else
            {
                updateCmd.AddParameter("local-address", "");
            }

            if (!string.IsNullOrEmpty(profile.MikroTikRemoteAddress))
            {
                updateCmd.AddParameter("remote-address", profile.MikroTikRemoteAddress);
            }
            else
            {
                updateCmd.AddParameter("remote-address", "");
            }

            if (!string.IsNullOrEmpty(rateLimit))
            {
                updateCmd.AddParameter("rate-limit", rateLimit);
            }
            else if (!string.IsNullOrEmpty(profile.MikroTikRateLimit))
            {
                updateCmd.AddParameter("rate-limit", profile.MikroTikRateLimit);
            }
            else
            {
                updateCmd.AddParameter("rate-limit", "");
            }

            updateCmd.AddParameter("only-one", profile.MikroTikOnlyOne ? "yes" : "no");

            updateCmd.ExecuteNonQuery();

            _logger.LogInformation($"✅ تم تحديث البروفايل {profile.Name} في المايكروتك");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تحديث البروفايل في المايكروتك: {ex.Message}");
            throw new InvalidOperationException("خطأ في تحديث البروفايل في المايكروتك", ex);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// حذف بروفايل من MikroTik
    /// </summary>
    public async Task<bool> DeleteProfileFromMikroTik(int serverId, string profileName)
    {
        _logger.LogInformation($"🔍 حذف بروفايل من المايكروتك: {profileName}");

        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        ITikConnection? connection = null;

        try
        {
            connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass);

            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            var findCmd = connection.CreateCommand("/ppp/profile/print");
            var allProfiles = findCmd.ExecuteList();
            var targetProfile = allProfiles.FirstOrDefault(p => GetSafeValue(p, "name") == profileName);

            if (targetProfile == null)
            {
                _logger.LogWarning($"⚠️ البروفايل {profileName} غير موجود في المايكروتك");
                return true;
            }

            var profileId = GetSafeValue(targetProfile, ".id");

            var usersCmd = connection.CreateCommand("/ppp/secret/print");
            usersCmd.AddParameter("?profile", profileName);
            var users = usersCmd.ExecuteList();
            var usersList = users.ToList();

            if (usersList.Count > 0)
            {
                throw new InvalidOperationException($"لا يمكن حذف البروفايل {profileName} لأنه مرتبط بـ {usersList.Count} مستخدم. قم بنقل المستخدمين أولاً.");
            }

            var deleteCmd = connection.CreateCommand("/ppp/profile/remove");
            deleteCmd.AddParameter(".id", profileId);
            deleteCmd.ExecuteNonQuery();

            _logger.LogInformation($"✅ تم حذف البروفايل {profileName} من المايكروتك");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في حذف البروفايل من المايكروتك: {ex.Message}");
            throw new InvalidOperationException("خطأ في حذف البروفايل من المايكروتك", ex);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// التحقق من وجود بروفايل في MikroTik
    /// </summary>
    public async Task<bool> CheckProfileExistsInMikroTik(int serverId, string profileName)
    {
        _logger.LogInformation($"🔍 التحقق من وجود بروفايل في المايكروتك: {profileName}");

        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (var connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass))
            {
                var checkCmd = connection.CreateCommand("/ppp/profile/print");
                checkCmd.AddParameter("?name", profileName);
                var profiles = checkCmd.ExecuteList();

                return profiles.Any();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في التحقق من وجود البروفايل: {ex.Message}");
            return false;
        }
    }
}
