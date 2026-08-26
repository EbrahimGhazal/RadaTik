using Microsoft.EntityFrameworkCore;
using RadaTik.ViewModels.MikroTikServers;
using RadaTik.Models;
using tik4net;

namespace RadaTik.Services;

public partial class MikroTikService
{
    /// <summary>
    /// تحديث بيانات مستخدم من صفحة AllUsers
    /// </summary>
    public async Task<bool> UpdateUserFromAllUsers(EditMikroTikUserViewModel model)
    {
        _logger.LogInformation($"🔍 بدء تحديث بيانات مستخدم من AllUsers: {model.UserName}");

        try
        {
            MikroTikServer? server = await _context.MikroTikServers.FindAsync(model.MikroTikServerId);
            if (server == null)
            {
                throw new InvalidOperationException("الخادم غير موجود");
            }

            if (!string.IsNullOrEmpty(model.ProfileName))
            {
                bool profileExists = await CheckProfileExistsInMikroTik(model.MikroTikServerId, model.ProfileName);
                if (!profileExists)
                {
                    throw new InvalidOperationException($"البروفايل '{model.ProfileName}' غير موجود في السيرفر");
                }
            }

            Client? clientInDb = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserName == model.UserName && c.MikroTikServerId == model.MikroTikServerId);

            if (clientInDb == null && !model.IsInDatabase)
            {
                Profile? profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Name == model.ProfileName && p.MikroTikServerId == model.MikroTikServerId);

                if (profile == null)
                {
                    throw new InvalidOperationException($"البروفايل '{model.ProfileName}' غير موجود في قاعدة البيانات");
                }

                clientInDb = new Client
                {
                    UserName = model.UserName,
                    Name = model.Name,
                    PhoneNumber = model.PhoneNumber,
                    ProfileId = profile.Id,
                    ProfileName = model.ProfileName,
                    IsActive = model.IsActive,
                    ReceiverId = model.ReceiverId,
                    MikroTikServerId = model.MikroTikServerId,
                    IsImportedFromServer = true,
                    CreatedDate = DateTime.Now,
                    LastUpdated = DateTime.Now,
                    ConnectionStatus = model.IsActive ? "مفعل" : "معطل",
                    Password = GenerateDefaultPassword(),
                    SID = model.SID ?? GenerateUniqueSID(),
                    Address = model.Address,
                    Service = model.Service ?? "pppoe",
                    AccountExpirationDate = model.AccountExpirationDate ?? DateTime.Now.AddMonths(1),
                };

                _context.Clients.Add(clientInDb);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ تم إنشاء عميل جديد في قاعدة البيانات: {model.UserName}");
            }
            else if (clientInDb != null)
            {
                if (!string.IsNullOrEmpty(model.ProfileName) && clientInDb.ProfileName != model.ProfileName)
                {
                    Profile? profile = await _context.Profiles
                        .FirstOrDefaultAsync(p => p.Name == model.ProfileName && p.MikroTikServerId == model.MikroTikServerId);

                    if (profile != null)
                    {
                        clientInDb.ProfileId = profile.Id;
                    }
                }

                clientInDb.Name = model.Name;
                clientInDb.PhoneNumber = model.PhoneNumber;
                clientInDb.ProfileName = model.ProfileName;
                clientInDb.IsActive = model.IsActive;
                clientInDb.ReceiverId = model.ReceiverId;
                clientInDb.LastUpdated = DateTime.Now;
                clientInDb.ConnectionStatus = model.IsActive ? "مفعل" : "معطل";

                if (model.AccountExpirationDate.HasValue)
                {
                    clientInDb.AccountExpirationDate = model.AccountExpirationDate;
                }

                if (!string.IsNullOrEmpty(model.Address))
                    clientInDb.Address = model.Address;
                else
                    clientInDb.Address = null;

                if (!string.IsNullOrEmpty(model.Service))
                    clientInDb.Service = model.Service;
                else
                    clientInDb.Service = "pppoe";

                if (!string.IsNullOrEmpty(model.SID))
                    clientInDb.SID = model.SID;

                _context.Clients.Update(clientInDb);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ تم تحديث العميل في قاعدة البيانات: {model.UserName}");
            }

            bool mikrotikResult = await UpdateMikroTikUserProfile(model);
            if (!mikrotikResult)
            {
                throw new InvalidOperationException("فشل تحديث المستخدم في المايكروتك");
            }

            _logger.LogInformation($"✅ تم تحديث بيانات المستخدم {model.UserName} بنجاح في كلا النظامين");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تحديث بيانات المستخدم {model.UserName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// تحديث بروفايل مستخدم في المايكروتك
    /// </summary>
    public async Task<bool> UpdateMikroTikUserProfile(EditMikroTikUserViewModel model)
    {
        _logger.LogInformation($"🔍 بدء تحديث مستخدم في المايكروتك: {model.UserName}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(model.MikroTikServerId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        ITikConnection? connection = null;

        try
        {
            connection = CreateConnectionWithRetry(server);

            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
            IEnumerable<ITikReSentence> users = findCmd.ExecuteList();
            ITikReSentence? targetUser = users.FirstOrDefault(u => GetSafeValue(u, "name") == model.UserName);

            if (targetUser == null)
            {
                throw new InvalidOperationException($"المستخدم {model.UserName} غير موجود في المايكروتك");
            }

            string userId = GetSafeValue(targetUser, ".id");

            ITikCommand updateCmd = connection.CreateCommand("/ppp/secret/set");
            updateCmd.AddParameter(".id", userId);

            if (!string.IsNullOrEmpty(model.ProfileName))
            {
                updateCmd.AddParameter("profile", model.ProfileName);
            }

            if (!string.IsNullOrEmpty(model.Address))
            {
                updateCmd.AddParameter("remote-address", model.Address);
            }

            updateCmd.AddParameter("disabled", model.IsActive ? "no" : "yes");
            updateCmd.ExecuteNonQuery();

            _logger.LogInformation($"✅ تم تحديث المستخدم {model.UserName} في المايكروتك بنجاح");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تحديث المستخدم في المايكروتك: {ex.Message}");
            throw;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// التحقق من وجود بروفايل محدد في سيرفر MikroTik
    /// </summary>
    public async Task<bool> CheckProfileExistsInServer(int serverId, string profileName)
    {
        try
        {
            List<string> profiles = await GetProfileNamesFromMikroTik(serverId);
            return profiles.Contains(profileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في التحقق من وجود البروفايل: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// جلب جميع المستخدمين مع بياناتهم الكاملة
    /// </summary>
    public async Task<List<EditMikroTikUserViewModel>> GetAllUsersWithDetails(int serverId)
    {
        _logger.LogInformation($"🔍 بدء جلب جميع المستخدمين مع التفاصيل للخادم {serverId}");

        List<EditMikroTikUserViewModel> result = new List<EditMikroTikUserViewModel>();
        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                ITikCommand secretCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> secretRows = secretCmd.ExecuteList();

                foreach (ITikReSentence? row in secretRows)
                {
                    string username = GetSafeValue(row, "name");
                    Client? clientInDb = await _context.Clients
                        .FirstOrDefaultAsync(c => c.UserName == username && c.MikroTikServerId == serverId);

                    EditMikroTikUserViewModel userViewModel = new EditMikroTikUserViewModel
                    {
                        UserName = username,
                        Password = GetSafeValue(row, "password"),
                        MikroTikServerId = serverId,
                        Service = GetSafeValue(row, "service"),
                        Address = GetSafeValue(row, "remote-address"),
                        ProfileName = GetSafeValue(row, "profile"),
                        ConnectionStatus = GetSafeValue(row, "disabled") == "true" ? "معطل" : "مفعل",
                        IsActive = GetSafeValue(row, "disabled") != "true",
                        IsInDatabase = clientInDb != null
                    };

                    if (clientInDb != null)
                    {
                        userViewModel.ClientId = clientInDb.Id;
                        userViewModel.Name = clientInDb.Name ?? string.Empty;
                        userViewModel.PhoneNumber = clientInDb.PhoneNumber;
                        userViewModel.SID = clientInDb.SID;
                        userViewModel.ReceiverId = clientInDb.ReceiverId;
                        userViewModel.ProfileName = clientInDb.ProfileName ?? userViewModel.ProfileName;
                        userViewModel.IsActive = clientInDb.IsActive;
                        userViewModel.AccountExpirationDate = clientInDb.AccountExpirationDate;
                    }

                    result.Add(userViewModel);
                }

                _logger.LogInformation($"✅ تم جلب {result.Count} مستخدم مع تفاصيلهم");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في جلب بيانات المستخدمين: {ex.Message}");
            throw;
        }

        return result;
    }

    /// <summary>
    /// فحص وجود مستخدم في المايكروتك
    /// </summary>
    public async Task<bool> CheckUserExists(string username, int serverId)
    {
        _logger.LogInformation($"🔍 فحص وجود مستخدم في المايكروتك: {username}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                ITikCommand checkCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = checkCmd.ExecuteList();

                return allUsers.Any(u => GetSafeValue(u, "name") == username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في فحص وجود المستخدم: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// إضافة مستخدم جديد في المايكروتك
    /// </summary>
    public async Task<bool> AddPPPoEUser(Client client)
    {
        _logger.LogInformation($"🔍 بدء إضافة مستخدم في المايكروتك: {client.UserName}");

        if (client.MikroTikServerId == null)
        {
            throw new InvalidOperationException("لم يتم تحديد خادم المايكروتك للعميل");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(client.MikroTikServerId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        ITikConnection? connection = null;

        try
        {
            connection = CreateConnectionWithRetry(server);
            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            ITikCommand checkCmd = connection.CreateCommand("/ppp/secret/print");
            IEnumerable<ITikReSentence> allUsers = checkCmd.ExecuteList();
            ITikReSentence? existingUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == client.UserName);

            if (existingUser != null)
            {
                _logger.LogWarning($"⚠️ المستخدم {client.UserName} موجود مسبقاً في المايكروتك");
                throw new InvalidOperationException($"المستخدم {client.UserName} موجود مسبقاً في الخادم");
            }

            if (string.IsNullOrEmpty(client.ProfileName))
            {
                throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
            }

            ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
            IEnumerable<ITikReSentence> allProfiles = profileCmd.ExecuteList();
            bool profileExists = allProfiles.Any(p => GetSafeValue(p, "name") == client.ProfileName);

            if (!profileExists)
            {
                _logger.LogWarning($"⚠️ البروفايل {client.ProfileName} غير موجود في المايكروتك");
                throw new InvalidOperationException($"البروفايل {client.ProfileName} غير موجود في الخادم");
            }

            try
            {
                ITikCommand addCmd = connection.CreateCommand("/ppp/secret/add");
                addCmd.AddParameter("name", client.UserName);
                addCmd.AddParameter("password", client.Password);
                addCmd.AddParameter("service", "pppoe");
                addCmd.AddParameter("profile", client.ProfileName);

                if (!string.IsNullOrEmpty(client.Address))
                {
                    addCmd.AddParameter("remote-address", client.Address);
                }

                if (!client.IsActive)
                {
                    addCmd.AddParameter("disabled", "yes");
                }

                addCmd.ExecuteNonQuery();
                _logger.LogInformation($"✅ تم إضافة المستخدم {client.UserName} في المايكروتك بنجاح");
            }
            catch (Exception cmdEx)
            {
                if (cmdEx.Message.Contains("!empty"))
                {
                    _logger.LogWarning($"⚠️ تم تجاهل خطأ !empty، قد تكون الإضافة ناجحة للمستخدم {client.UserName}");
                    await Task.Delay(1000);
                }
                else
                {
                    throw;
                }
            }

            await Task.Delay(1000);

            checkCmd = connection.CreateCommand("/ppp/secret/print");
            allUsers = checkCmd.ExecuteList();
            ITikReSentence? verifyUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == client.UserName);

            if (verifyUser == null)
            {
                _logger.LogWarning("⚠️ فشل التحقق من إضافة المستخدم في المايكروتك، لكن قد تكون العملية ناجحة");
            }
            else
            {
                _logger.LogInformation($"✅ تم التحقق من إضافة المستخدم {client.UserName} بنجاح");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في إضافة المستخدم في المايكروتك: {ex.Message}");
            throw new InvalidOperationException("خطأ في إضافة المستخدم في المايكروتك", ex);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// تحديث مستخدم في المايكروتك
    /// </summary>
    public async Task<bool> UpdatePPPoEUser(Client client)
    {
        _logger.LogInformation($"🔍 بدء تحديث مستخدم في المايكروتك: {client.UserName}");

        if (client.MikroTikServerId == null)
        {
            throw new InvalidOperationException("لم يتم تحديد خادم المايكروتك للعميل");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(client.MikroTikServerId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == client.UserName);

                if (targetUser == null)
                {
                    _logger.LogWarning($"⚠️ المستخدم {client.UserName} غير موجود في المايكروتك");
                    throw new InvalidOperationException($"المستخدم {client.UserName} غير موجود في الخادم");
                }

                string userId = GetSafeValue(targetUser, ".id");

                if (string.IsNullOrEmpty(client.ProfileName))
                {
                    throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
                }

                ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
                IEnumerable<ITikReSentence> allProfiles = profileCmd.ExecuteList();
                bool profileExists = allProfiles.Any(p => GetSafeValue(p, "name") == client.ProfileName);

                if (!profileExists)
                {
                    _logger.LogWarning($"⚠️ البروفايل {client.ProfileName} غير موجود في المايكروتك");
                    throw new InvalidOperationException($"البروفايل {client.ProfileName} غير موجود في الخادم");
                }

                ITikCommand updateCmd = connection.CreateCommand("/ppp/secret/set");
                updateCmd.AddParameter(".id", userId);
                if (!string.IsNullOrEmpty(client.Password))
                {
                    updateCmd.AddParameter("password", client.Password);
                }
                updateCmd.AddParameter("profile", client.ProfileName);
                updateCmd.AddParameter("disabled", client.IsActive ? "no" : "yes");

                if (!string.IsNullOrEmpty(client.Address))
                {
                    updateCmd.AddParameter("remote-address", client.Address);
                }

                updateCmd.ExecuteNonQuery();

                _logger.LogInformation($"✅ تم تحديث المستخدم {client.UserName} في المايكروتك بنجاح");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تحديث المستخدم في المايكروتك: {ex.Message}");
            throw new InvalidOperationException("خطأ في تحديث المستخدم في المايكروتك", ex);
        }
    }

    /// <summary>
    /// تحديث مستخدم في المايكروتك مع دعم تغيير اسم المستخدم.
    /// </summary>
    public async Task<bool> UpdatePPPoEUserWithOriginalUsername(Client client, string originalUsername)
    {
        _logger.LogInformation($"🔍 بدء تحديث مستخدم مع تغيير اسم المستخدم: {originalUsername} -> {client.UserName}");

        if (client.MikroTikServerId == null)
        {
            throw new InvalidOperationException("لم يتم تحديد خادم المايكروتك للعميل");
        }

        if (string.IsNullOrWhiteSpace(originalUsername))
        {
            throw new InvalidOperationException("اسم المستخدم الحالي على المايكروتك غير صالح");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(client.MikroTikServerId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == originalUsername);

                if (targetUser == null)
                {
                    throw new InvalidOperationException($"المستخدم {originalUsername} غير موجود في الخادم");
                }

                string newUserName = client.UserName ?? string.Empty;
                if (!string.Equals(originalUsername, newUserName, StringComparison.Ordinal))
                {
                    ITikReSentence? duplicate = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == newUserName);
                    if (duplicate != null)
                    {
                        throw new InvalidOperationException($"المستخدم {newUserName} موجود مسبقاً في الخادم");
                    }
                }

                string userId = GetSafeValue(targetUser, ".id");

                if (string.IsNullOrEmpty(client.ProfileName))
                {
                    throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
                }

                ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
                IEnumerable<ITikReSentence> allProfiles = profileCmd.ExecuteList();
                bool profileExists = allProfiles.Any(p => GetSafeValue(p, "name") == client.ProfileName);
                if (!profileExists)
                {
                    throw new InvalidOperationException($"البروفايل {client.ProfileName} غير موجود في الخادم");
                }

                ITikCommand updateCmd = connection.CreateCommand("/ppp/secret/set");
                updateCmd.AddParameter(".id", userId);
                updateCmd.AddParameter("name", newUserName);
                updateCmd.AddParameter("profile", client.ProfileName);
                updateCmd.AddParameter("disabled", client.IsActive ? "no" : "yes");

                if (!string.IsNullOrEmpty(client.Password))
                {
                    updateCmd.AddParameter("password", client.Password);
                }

                if (!string.IsNullOrEmpty(client.Address))
                {
                    updateCmd.AddParameter("remote-address", client.Address);
                }

                updateCmd.ExecuteNonQuery();
                _logger.LogInformation($"✅ تم تحديث المستخدم {originalUsername} إلى {newUserName} بنجاح");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تحديث المستخدم مع تغيير الاسم: {ex.Message}");
            throw new InvalidOperationException("خطأ في تحديث المستخدم في المايكروتك", ex);
        }
    }

    /// <summary>
    /// حذف مستخدم من المايكروتك
    /// </summary>
    public async Task<bool> DeletePPPoEUser(string username, int serverId)
    {
        _logger.LogInformation($"🔍 بدء حذف مستخدم من المايكروتك: {username}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == username);

                if (targetUser == null)
                {
                    _logger.LogWarning($"⚠️ المستخدم {username} غير موجود في المايكروتك");
                    return true;
                }

                string userId = GetSafeValue(targetUser, ".id");

                ITikCommand deleteCmd = connection.CreateCommand("/ppp/secret/remove");
                deleteCmd.AddParameter(".id", userId);
                deleteCmd.ExecuteNonQuery();

                _logger.LogInformation($"✅ تم حذف المستخدم {username} من المايكروتك بنجاح");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في حذف المستخدم من المايكروتك: {ex.Message}");
            throw new InvalidOperationException("خطأ في حذف المستخدم من المايكروتك", ex);
        }
    }

    /// <summary>
    /// جلب معلومات مستخدم من المايكروتك
    /// </summary>
    public async Task<Client?> GetPPPoEUserInfo(string username, int serverId)
    {
        _logger.LogInformation($"🔍 جلب معلومات مستخدم من المايكروتك: {username}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == username);

                if (targetUser == null)
                {
                    return null;
                }

                Client client = new Client
                {
                    UserName = GetSafeValue(targetUser, "name"),
                    Password = GetSafeValue(targetUser, "password"),
                    Service = GetSafeValue(targetUser, "service"),
                    Address = GetSafeValue(targetUser, "remote-address"),
                    ProfileName = GetSafeValue(targetUser, "profile"),
                    ConnectionStatus = GetSafeValue(targetUser, "disabled") == "true" ? "معطل" : "مفعل"
                };

                return client;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في جلب معلومات المستخدم من المايكروتك: {ex.Message}");
            throw new InvalidOperationException("خطأ في جلب معلومات المستخدم من المايكروتك", ex);
        }
    }

    /// <summary>
    /// جلب المستخدمين النشطين (المتصلين حالياً)
    /// </summary>
    public async Task<List<Client>> GetActivePPPoEUsers(int serverId)
    {
        _logger.LogInformation($"🔍 بدء جلب المستخدمين النشطين للخادم {serverId}");

        List<Client> result = new List<Client>();

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            _logger.LogError($"❌ الخادم غير موجود: {serverId}");
            throw new InvalidOperationException("الخادم غير موجود");
        }

        _logger.LogInformation($"🔗 محاولة الاتصال بالخادم: {server.Host}:{server.Port} باسم {server.User}");

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بنجاح");

                ITikCommand activeCmd = connection.CreateCommand("/ppp/active/print");
                IEnumerable<ITikReSentence> activeRows = activeCmd.ExecuteList();

                _logger.LogInformation($"📊 تم العثور على {activeRows.Count()} مستخدم نشط");

                foreach (ITikReSentence? row in activeRows)
                {
                    string username = GetSafeValue(row, "name");
                    _logger.LogInformation($"👤 معالجة المستخدم: {username}");

                    Client client = new Client
                    {
                        UserName = username,
                        Address = GetSafeValue(row, "address"),
                        Uptime = GetSafeValue(row, "uptime"),
                        Service = GetSafeValue(row, "service"),
                        ConnectionStatus = "نشط",
                        MacAddress = GetSafeValue(row, "caller-id"),
                        MikroTikServerId = serverId,
                        LastUpdated = DateTime.Now
                    };

                    result.Add(client);
                }

                _logger.LogInformation($"✅ تم جلب {result.Count} مستخدم نشط من الخادم {server.Host}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في الاتصال بالخادم {server.Host}: {ex.Message}");
            throw new InvalidOperationException("خطأ في الاتصال بالخادم", ex);
        }

        return result;
    }

    /// <summary>
    /// جلب جميع مستخدمي PPPoE (من الإعدادات)
    /// </summary>
    public async Task<List<Client>> GetAllPPPoEUsers(int serverId)
    {
        _logger.LogInformation($"🔍 بدء جلب جميع المستخدمين للخادم {serverId}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            _logger.LogError($"❌ الخادم غير موجود: {serverId}");
            throw new InvalidOperationException("الخادم غير موجود");
        }

        _logger.LogInformation($"🔗 محاولة الاتصال بالخادم: {server.Host}:{server.Port} باسم {server.User}");

        try
        {
            List<Client> result = await ExecuteWithRetry(server, connection =>
            {
                ITikCommand secretCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> secretRows = secretCmd.ExecuteList();
                List<Client> users = new List<Client>();

                _logger.LogInformation($"📊 تم العثور على {secretRows.Count()} مستخدم في الإعدادات");

                foreach (ITikReSentence? row in secretRows)
                {
                    string username = GetSafeValue(row, "name");
                    _logger.LogInformation($"👤 معالجة المستخدم: {username}");

                    Client client = new Client
                    {
                        UserName = username,
                        Password = GetSafeValue(row, "password"),
                        Service = GetSafeValue(row, "service"),
                        Address = GetSafeValue(row, "remote-address"),
                        ProfileName = GetSafeValue(row, "profile"),
                        ConnectionStatus = GetSafeValue(row, "disabled") == "true" ? "معطل" : "مفعل",
                        MikroTikServerId = serverId,
                        LastUpdated = DateTime.Now
                    };

                    users.Add(client);
                }

                return users;
            }, maxRetries: 3);

            _logger.LogInformation($"✅ تم جلب {result.Count} مستخدم من إعدادات الخادم {server.Host}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في الاتصال بالخادم {server.Host}: {ex.Message}");
            throw new InvalidOperationException("خطأ في الاتصال بالخادم", ex);
        }
    }

    /// <summary>
    /// قطع اتصال مستخدم نشط
    /// </summary>
    public async Task<bool> DisconnectActiveUser(int serverId, string username)
    {
        _logger.LogInformation($"🔍 بدء قطع اتصال المستخدم النشط: {username}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            _logger.LogError($"❌ الخادم غير موجود: {serverId}");
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بنجاح");

                ITikCommand findCmd = connection.CreateCommand("/ppp/active/print");
                IEnumerable<ITikReSentence> allSessions = findCmd.ExecuteList();
                ITikReSentence? targetSession = allSessions.FirstOrDefault(s => GetSafeValue(s, "name") == username);

                if (targetSession == null)
                {
                    _logger.LogWarning($"⚠️ لا يوجد اتصال نشط للمستخدم {username}");
                    throw new InvalidOperationException($"لا يوجد اتصال نشط للمستخدم {username}");
                }

                string sessionId = GetSafeValue(targetSession, ".id");

                ITikCommand disconnectCmd = connection.CreateCommand("/ppp/active/remove");
                disconnectCmd.AddParameter(".id", sessionId);
                disconnectCmd.ExecuteNonQuery();

                _logger.LogInformation($"✅ تم قطع اتصال المستخدم {username} بنجاح");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في قطع اتصال المستخدم {username}: {ex.Message}");
            throw new InvalidOperationException("خطأ في قطع اتصال المستخدم", ex);
        }
    }

    /// <summary>
    /// تجميد حساب مستخدم (تعطيل)
    /// </summary>
    public async Task<bool> DisablePPPoEUser(int serverId, string username)
    {
        _logger.LogInformation($"🔍 بدء تجميد حساب المستخدم: {username}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            _logger.LogError($"❌ الخادم غير موجود: {serverId}");
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == username);

                if (targetUser == null)
                {
                    _logger.LogWarning($"⚠️ المستخدم {username} غير موجود في المايكروتك");
                    throw new InvalidOperationException($"المستخدم {username} غير موجود في الخادم");
                }

                string userId = GetSafeValue(targetUser, ".id");

                ITikCommand disableCmd = connection.CreateCommand("/ppp/secret/set");
                disableCmd.AddParameter(".id", userId);
                disableCmd.AddParameter("disabled", "yes");
                disableCmd.ExecuteNonQuery();

                _logger.LogInformation($"✅ تم تجميد حساب المستخدم {username} بنجاح");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تجميد المستخدم {username}: {ex.Message}");
            throw new InvalidOperationException("خطأ في تجميد المستخدم", ex);
        }
    }

    /// <summary>
    /// تفعيل حساب مستخدم (إعادة التمكين)
    /// </summary>
    public async Task<bool> EnablePPPoEUser(int serverId, string username)
    {
        _logger.LogInformation($"🔍 بدء تفعيل حساب المستخدم: {username}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            _logger.LogError($"❌ الخادم غير موجود: {serverId}");
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == username);

                if (targetUser == null)
                {
                    _logger.LogWarning($"⚠️ المستخدم {username} غير موجود في المايكروتك");
                    throw new InvalidOperationException($"المستخدم {username} غير موجود في الخادم");
                }

                string userId = GetSafeValue(targetUser, ".id");

                ITikCommand enableCmd = connection.CreateCommand("/ppp/secret/set");
                enableCmd.AddParameter(".id", userId);
                enableCmd.AddParameter("disabled", "no");
                enableCmd.ExecuteNonQuery();

                _logger.LogInformation($"✅ تم تفعيل حساب المستخدم {username} بنجاح");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تفعيل المستخدم {username}: {ex.Message}");
            throw new InvalidOperationException("خطأ في تفعيل المستخدم", ex);
        }
    }

    /// <summary>
    /// تجميد الحساب مع قطع الاتصال الحالي
    /// </summary>
    public async Task<bool> FreezeAccount(int serverId, string username)
    {
        _logger.LogInformation($"🔍 بدء تجميد الحساب مع قطع الاتصال: {username}");

        try
        {
            try
            {
                await DisconnectActiveUser(serverId, username);
                _logger.LogInformation($"✅ تم قطع الاتصال النشط للمستخدم {username}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ لا يوجد اتصال نشط لقطعه للمستخدم {username}: {ex.Message}");
            }

            await DisablePPPoEUser(serverId, username);

            _logger.LogInformation($"✅ تم تجميد الحساب بنجاح للمستخدم {username}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تجميد الحساب: {ex.Message}");
            throw new InvalidOperationException("خطأ في تجميد الحساب", ex);
        }
    }

    /// <summary>
    /// تفعيل الحساب
    /// </summary>
    public async Task<bool> UnfreezeAccount(int serverId, string username)
    {
        _logger.LogInformation($"🔍 بدء تفعيل الحساب: {username}");

        try
        {
            await EnablePPPoEUser(serverId, username);

            _logger.LogInformation($"✅ تم تفعيل الحساب بنجاح للمستخدم {username}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تفعيل الحساب: {ex.Message}");
            throw new InvalidOperationException("خطأ في تفعيل الحساب", ex);
        }
    }

    /// <summary>
    /// اختبار الاتصال بالخادم
    /// </summary>
    public async Task<bool> TestConnection(int serverId)
    {
        _logger.LogInformation($"🔍 بدء اختبار الاتصال للخادم {serverId}");

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            _logger.LogError($"❌ الخادم غير موجود: {serverId}");
            return false;
        }

        _logger.LogInformation($"🔗 اختبار الاتصال بـ {server.Host}:{server.Port} باسم {server.User}");

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                ITikCommand cmd = connection.CreateCommand("/system/resource/print");
                IEnumerable<ITikReSentence> result = cmd.ExecuteList();

                bool success = result.Any();
                _logger.LogInformation("نتيجة اختبار الاتصال: {Success}", success ? "نجح" : "فشل");

                return success;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ فشل اختبار الاتصال بالخادم {server.Host}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// تجديد اشتراك مستخدم PPPoE - تحديث تاريخ انتهاء الصلاحية
    /// </summary>
    public async Task<bool> RenewPPPoESubscription(string username, int serverId, DateTime? newExpirationDate)
    {
        _logger.LogInformation($"🔄 بدء تجديد اشتراك المستخدم: {username}");

        if (newExpirationDate == null)
        {
            throw new InvalidOperationException("يجب تحديد تاريخ انتهاء الصلاحية الجديد");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using (ITikConnection connection = CreateConnectionWithRetry(server))
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => GetSafeValue(u, "name") == username);

                if (targetUser == null)
                {
                    _logger.LogWarning($"⚠️ المستخدم {username} غير موجود في المايكروتك");
                    throw new InvalidOperationException($"المستخدم {username} غير موجود في الخادم");
                }

                string userId = GetSafeValue(targetUser, ".id");

                _logger.LogInformation($"✅ تم تجديد اشتراك المستخدم {username} حتى تاريخ {newExpirationDate.Value:yyyy/MM/dd}");

                Client? currentUser = await _context.Clients
                    .FirstOrDefaultAsync(c => c.UserName == username && c.MikroTikServerId == serverId);
                if (currentUser != null && !currentUser.IsActive && newExpirationDate.Value > DateTime.Now)
                {
                    ITikCommand setCmd = connection.CreateCommand("/ppp/secret/set");
                    setCmd.AddParameter(".id", userId);
                    setCmd.AddParameter("disabled", "no");
                    setCmd.ExecuteNonQuery();
                    _logger.LogInformation($"✅ تم تفعيل الحساب في MikroTik بعد التجديد");
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ في تجديد الاشتراك: {ex.Message}");
            throw new InvalidOperationException("خطأ في تجديد الاشتراك", ex);
        }
    }

    /// <summary>
    /// تجديد اشتراك حتى تاريخ 8 من الشهر القادم
    /// </summary>
    public async Task<bool> RenewSubscriptionTo8thNextMonth(string username, int serverId)
    {
        DateTime today = DateTime.Now;
        DateTime nextMonth = today.AddMonths(1);
        DateTime renewalDate = new DateTime(nextMonth.Year, nextMonth.Month, 8);

        return await RenewPPPoESubscription(username, serverId, renewalDate);
    }

    /// <summary>
    /// التحقق من الحسابات المنتهية الصلاحية وإيقافها تلقائياً
    /// </summary>
    public async Task<ExpiredAccountsResult> CheckAndDisableExpiredAccounts()
    {
        _logger.LogInformation("🔍 بدء التحقق من الحسابات المنتهية الصلاحية");

        ExpiredAccountsResult result = new ExpiredAccountsResult
        {
            CheckDate = DateTime.Now
        };

        try
        {
            List<Client> clientsWithExpiration = await _context.Clients
                .Where(c => c.AccountExpirationDate.HasValue
                    && c.AccountExpirationDate.Value <= DateTime.Now
                    && c.IsActive)
                .Include(c => c.MikroTikServer)
                .ToListAsync();

            result.ExpiredAccountsFound = clientsWithExpiration.Count;

            foreach (Client? client in clientsWithExpiration)
            {
                try
                {
                    if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                    {
                        await DisablePPPoEUser(client.MikroTikServerId.Value, client.UserName);
                        result.DisabledInMikroTik++;
                    }

                    client.IsActive = false;
                    client.ConnectionStatus = "منتهي الصلاحية";
                    client.LastUpdated = DateTime.Now;

                    DateTime expirationDate = client.AccountExpirationDate ?? DateTime.Now;

                    result.DisabledAccounts.Add(new ExpiredAccountInfo
                    {
                        ClientId = client.Id,
                        ClientName = client.Name,
                        UserName = client.UserName,
                        ExpirationDate = expirationDate
                    });

                    _logger.LogInformation($"✅ تم إيقاف حساب منتهي الصلاحية: {client.UserName} (انتهى في {expirationDate:yyyy/MM/dd})");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ خطأ في إيقاف حساب منتهي الصلاحية: {client.UserName}");
                    result.Errors.Add($"خطأ في إيقاف {client.UserName}: {ex.Message}");
                }
            }

            if (result.DisabledAccounts.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            result.Success = true;
            result.Message = $"تم التحقق من {result.ExpiredAccountsFound} حساب منتهي الصلاحية وتم إيقاف {result.DisabledAccounts.Count} حساب";

            _logger.LogInformation($"✅ انتهى التحقق من الحسابات المنتهية: {result.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ خطأ عام في التحقق من الحسابات المنتهية: {ex.Message}");
            result.Success = false;
            result.Message = $"خطأ في التحقق من الحسابات المنتهية: {ex.Message}";
        }

        return result;
    }
}

