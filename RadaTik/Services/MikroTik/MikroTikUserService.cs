using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.ViewModels.MikroTikServers;
using tik4net;

namespace RadaTik.Services.MikroTik;

public sealed class MikroTikUserService(
    ApplicationDbContext context,
    ILogger<MikroTikUserService> logger,
    IMikroTikProfilesService profiles,
    MikroTikConnectionSupport connection) : IMikroTikPppoeUserService
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<MikroTikUserService> _logger = logger;
    private readonly IMikroTikProfilesService _profiles = profiles;
    private readonly MikroTikConnectionSupport _connection = connection;

    /// <summary>
    /// تحديث بيانات مستخدم من صفحة AllUsers
    /// </summary>
    public async Task<bool> UpdateUserFromAllUsers(EditMikroTikUserViewModel model)
    {
        _logger.LogInformation("🔍 بدء تحديث بيانات مستخدم من AllUsers: {UserName}", model.UserName);

        try
        {
            MikroTikServer? server = await _context.MikroTikServers.FindAsync(model.MikroTikServerId);
            if (server is null)
            {
                throw new InvalidOperationException("الخادم غير موجود");
            }

            if (!string.IsNullOrEmpty(model.ProfileName))
            {
                bool profileExists = await _profiles.CheckProfileExistsInMikroTik(model.MikroTikServerId, model.ProfileName);
                if (!profileExists)
                {
                    throw new InvalidOperationException($"البروفايل '{model.ProfileName}' غير موجود في السيرفر");
                }
            }

            Client? clientInDb = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserName == model.UserName && c.MikroTikServerId == model.MikroTikServerId);

            if (clientInDb is null && !model.IsInDatabase)
            {
                Profile? profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Name == model.ProfileName && p.MikroTikServerId == model.MikroTikServerId);

                if (profile is null)
                {
                    throw new InvalidOperationException($"البروفايل '{model.ProfileName}' غير موجود في قاعدة البيانات");
                }

                clientInDb = new()
                {
                    UserName = model.UserName,
                    Name = model.Name,
                    PhoneNumber = model.PhoneNumber,
                    ProfileId = profile.Id,
                    ProfileName = model.ProfileName,
                    IsActive = model.IsActive,
                    ReceiverId = model.ReceiverId,
                    MikroTikServerId = model.MikroTikServerId,
                    CreatedDate = DateTime.Now,
                    LastUpdated = DateTime.Now,
                    ConnectionStatus = model.IsActive ? "مفعل" : "معطل",
                    Password = MikroTikApiSupport.GenerateDefaultPassword(),
                    SID = model.SID ?? GenerateUniqueSid(),
                    Address = model.Address,
                    Service = model.Service ?? "pppoe",
                    AccountExpirationDate = model.AccountExpirationDate ?? DateTime.Now.AddMonths(1),
                };

                _context.Clients.Add(clientInDb);
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ تم إنشاء عميل جديد في قاعدة البيانات: {UserName}", model.UserName);
            }
            else if (clientInDb is not null)
            {
                if (!string.IsNullOrEmpty(model.ProfileName) && clientInDb.ProfileName != model.ProfileName)
                {
                    Profile? profile = await _context.Profiles
                        .FirstOrDefaultAsync(p => p.Name == model.ProfileName && p.MikroTikServerId == model.MikroTikServerId);

                    if (profile is not null)
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

                if (model.AccountExpirationDate is not null)
                {
                    clientInDb.AccountExpirationDate = model.AccountExpirationDate;
                }

                if (!string.IsNullOrEmpty(model.Address))
                {
                    clientInDb.Address = model.Address;
                }
                else
                {
                    clientInDb.Address = null;
                }

                if (!string.IsNullOrEmpty(model.Service))
                {
                    clientInDb.Service = model.Service;
                }
                else
                {
                    clientInDb.Service = "pppoe";
                }

                if (!string.IsNullOrEmpty(model.SID))
                {
                    clientInDb.SID = model.SID;
                }

                _context.Clients.Update(clientInDb);
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ تم تحديث العميل في قاعدة البيانات: {UserName}", model.UserName);
            }

            bool mikrotikResult = await UpdateMikroTikUserProfile(model);
            if (!mikrotikResult)
            {
                throw new InvalidOperationException("فشل تحديث المستخدم في المايكروتك");
            }

            _logger.LogInformation("✅ تم تحديث بيانات المستخدم {UserName} بنجاح في كلا النظامين", model.UserName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث بيانات المستخدم {UserName}: {Message}", model.UserName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// تحديث بروفايل مستخدم في المايكروتك
    /// </summary>
    public async Task<bool> UpdateMikroTikUserProfile(EditMikroTikUserViewModel model)
    {
        _logger.LogInformation("🔍 بدء تحديث مستخدم في المايكروتك: {UserName}", model.UserName);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(model.MikroTikServerId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        ITikConnection? connection = null;

        try
        {
            connection = _connection.CreateConnectionWithRetry(server);

            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
            IEnumerable<ITikReSentence> users = findCmd.ExecuteList();
            ITikReSentence? targetUser = users.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == model.UserName);

            if (targetUser is null)
            {
                throw new InvalidOperationException($"المستخدم {model.UserName} غير موجود في المايكروتك");
            }

            string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");

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

            _logger.LogInformation("✅ تم تحديث المستخدم {UserName} في المايكروتك بنجاح", model.UserName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث المستخدم في المايكروتك: {Message}", ex.Message);
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
            List<string> profiles = await _profiles.GetProfileNamesFromMikroTik(serverId);
            return profiles.Contains(profileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في التحقق من وجود البروفايل: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// جلب جميع المستخدمين مع بياناتهم الكاملة
    /// </summary>
    public async Task<List<EditMikroTikUserViewModel>> GetAllUsersWithDetails(int serverId)
    {
        _logger.LogInformation("🔍 بدء جلب جميع المستخدمين مع التفاصيل للخادم {ServerId}", serverId);

        List<EditMikroTikUserViewModel> result = [];
        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                ITikCommand secretCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> secretRows = secretCmd.ExecuteList();

                foreach (ITikReSentence? row in secretRows)
                {
                    string username = MikroTikApiSupport.GetSafeValue(row, "name");
                    Client? clientInDb = await _context.Clients
                        .FirstOrDefaultAsync(c => c.UserName == username && c.MikroTikServerId == serverId);

                    EditMikroTikUserViewModel userViewModel = new()
                    {
                        UserName = username,
                        Password = MikroTikApiSupport.GetSafeValue(row, "password"),
                        MikroTikServerId = serverId,
                        Service = MikroTikApiSupport.GetSafeValue(row, "service"),
                        Address = MikroTikApiSupport.GetSafeValue(row, "remote-address"),
                        ProfileName = MikroTikApiSupport.GetSafeValue(row, "profile"),
                        ConnectionStatus = MikroTikApiSupport.GetSafeValue(row, "disabled") == "true" ? "معطل" : "مفعل",
                        IsActive = MikroTikApiSupport.GetSafeValue(row, "disabled") != "true",
                        IsInDatabase = clientInDb is not null
                    };

                    if (clientInDb is not null)
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

                _logger.LogInformation("✅ تم جلب {Count} مستخدم مع تفاصيلهم", result.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب بيانات المستخدمين: {Message}", ex.Message);
            throw;
        }

        return result;
    }

    /// <summary>
    /// فحص وجود مستخدم في المايكروتك
    /// </summary>
    public async Task<bool> CheckUserExists(string username, int serverId)
    {
        _logger.LogInformation("🔍 فحص وجود مستخدم في المايكروتك: {Username}", username);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                ITikCommand checkCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = checkCmd.ExecuteList();

                return allUsers.Any(u => MikroTikApiSupport.GetSafeValue(u, "name") == username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في فحص وجود المستخدم: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// إضافة مستخدم جديد في المايكروتك
    /// </summary>
    public async Task<bool> AddPPPoEUser(Client client)
    {
        _logger.LogInformation("🔍 بدء إضافة مستخدم في المايكروتك: {UserName}", client.UserName);

        if (client.MikroTikServerId is null)
        {
            throw new InvalidOperationException("لم يتم تحديد خادم المايكروتك للعميل");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(client.MikroTikServerId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        ITikConnection? connection = null;

        try
        {
            connection = _connection.CreateConnectionWithRetry(server);
            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            ITikCommand checkCmd = connection.CreateCommand("/ppp/secret/print");
            IEnumerable<ITikReSentence> allUsers = checkCmd.ExecuteList();
            ITikReSentence? existingUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == client.UserName);

            if (existingUser is not null)
            {
                _logger.LogWarning("⚠️ المستخدم {UserName} موجود مسبقاً في المايكروتك", client.UserName);
                throw new InvalidOperationException($"المستخدم {client.UserName} موجود مسبقاً في الخادم");
            }

            if (string.IsNullOrEmpty(client.ProfileName))
            {
                throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
            }

            ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
            IEnumerable<ITikReSentence> allProfiles = profileCmd.ExecuteList();
            bool profileExists = allProfiles.Any(p => MikroTikApiSupport.GetSafeValue(p, "name") == client.ProfileName);

            if (!profileExists)
            {
                _logger.LogWarning("⚠️ البروفايل {ProfileName} غير موجود في المايكروتك", client.ProfileName);
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
                _logger.LogInformation("✅ تم إضافة المستخدم {UserName} في المايكروتك بنجاح", client.UserName);
            }
            catch (Exception cmdEx)
            {
                if (cmdEx.Message.Contains("!empty"))
                {
                    _logger.LogWarning("⚠️ تم تجاهل خطأ !empty، قد تكون الإضافة ناجحة للمستخدم {UserName}", client.UserName);
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
            ITikReSentence? verifyUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == client.UserName);

            if (verifyUser is null)
            {
                _logger.LogWarning("⚠️ فشل التحقق من إضافة المستخدم في المايكروتك، لكن قد تكون العملية ناجحة");
            }
            else
            {
                _logger.LogInformation("✅ تم التحقق من إضافة المستخدم {UserName} بنجاح", client.UserName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إضافة المستخدم في المايكروتك: {Message}", ex.Message);
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
        _logger.LogInformation("🔍 بدء تحديث مستخدم في المايكروتك: {UserName}", client.UserName);

        if (client.MikroTikServerId is null)
        {
            throw new InvalidOperationException("لم يتم تحديد خادم المايكروتك للعميل");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(client.MikroTikServerId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == client.UserName);

                if (targetUser is null)
                {
                    _logger.LogWarning("⚠️ المستخدم {UserName} غير موجود في المايكروتك", client.UserName);
                    throw new InvalidOperationException($"المستخدم {client.UserName} غير موجود في الخادم");
                }

                string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");

                if (string.IsNullOrEmpty(client.ProfileName))
                {
                    throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
                }

                ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
                IEnumerable<ITikReSentence> allProfiles = profileCmd.ExecuteList();
                bool profileExists = allProfiles.Any(p => MikroTikApiSupport.GetSafeValue(p, "name") == client.ProfileName);

                if (!profileExists)
                {
                    _logger.LogWarning("⚠️ البروفايل {ProfileName} غير موجود في المايكروتك", client.ProfileName);
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

                _logger.LogInformation("✅ تم تحديث المستخدم {UserName} في المايكروتك بنجاح", client.UserName);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث المستخدم في المايكروتك: {Message}", ex.Message);
            throw new InvalidOperationException("خطأ في تحديث المستخدم في المايكروتك", ex);
        }
    }

    /// <summary>
    /// تحديث مستخدم في المايكروتك مع دعم تغيير اسم المستخدم.
    /// </summary>
    public async Task<bool> UpdatePPPoEUserWithOriginalUsername(Client client, string originalUsername)
    {
        _logger.LogInformation("🔍 بدء تحديث مستخدم مع تغيير اسم المستخدم: {OriginalUsername} -> {UserName}", originalUsername, client.UserName);

        if (client.MikroTikServerId is null)
        {
            throw new InvalidOperationException("لم يتم تحديد خادم المايكروتك للعميل");
        }

        if (string.IsNullOrWhiteSpace(originalUsername))
        {
            throw new InvalidOperationException("اسم المستخدم الحالي على المايكروتك غير صالح");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(client.MikroTikServerId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == originalUsername);

                if (targetUser is null)
                {
                    throw new InvalidOperationException($"المستخدم {originalUsername} غير موجود في الخادم");
                }

                string newUserName = client.UserName ?? string.Empty;
                if (!string.Equals(originalUsername, newUserName, StringComparison.Ordinal))
                {
                    ITikReSentence? duplicate = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == newUserName);
                    if (duplicate is not null)
                    {
                        throw new InvalidOperationException($"المستخدم {newUserName} موجود مسبقاً في الخادم");
                    }
                }

                string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");

                if (string.IsNullOrEmpty(client.ProfileName))
                {
                    throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
                }

                ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
                IEnumerable<ITikReSentence> allProfiles = profileCmd.ExecuteList();
                bool profileExists = allProfiles.Any(p => MikroTikApiSupport.GetSafeValue(p, "name") == client.ProfileName);
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
                _logger.LogInformation("✅ تم تحديث المستخدم {OriginalUsername} إلى {NewUserName} بنجاح", originalUsername, newUserName);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث المستخدم مع تغيير الاسم: {Message}", ex.Message);
            throw new InvalidOperationException("خطأ في تحديث المستخدم في المايكروتك", ex);
        }
    }

    /// <summary>
    /// حذف مستخدم من المايكروتك
    /// </summary>
    public async Task<bool> DeletePPPoEUser(string username, int serverId)
    {
        _logger.LogInformation("🔍 بدء حذف مستخدم من المايكروتك: {Username}", username);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == username);

                if (targetUser is null)
                {
                    _logger.LogWarning("⚠️ المستخدم {Username} غير موجود في المايكروتك", username);
                    return true;
                }

                string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");

                ITikCommand deleteCmd = connection.CreateCommand("/ppp/secret/remove");
                deleteCmd.AddParameter(".id", userId);
                deleteCmd.ExecuteNonQuery();

                _logger.LogInformation("✅ تم حذف المستخدم {Username} من المايكروتك بنجاح", username);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في حذف المستخدم من المايكروتك: {Message}", ex.Message);
            throw new InvalidOperationException("خطأ في حذف المستخدم من المايكروتك", ex);
        }
    }

    /// <summary>
    /// جلب معلومات مستخدم من المايكروتك
    /// </summary>
    public async Task<Client?> GetPPPoEUserInfo(string username, int serverId)
    {
        _logger.LogInformation("🔍 جلب معلومات مستخدم من المايكروتك: {Username}", username);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == username);

                if (targetUser is null)
                {
                    return null;
                }

                Client client = new()
                {
                    UserName = MikroTikApiSupport.GetSafeValue(targetUser, "name"),
                    Password = MikroTikApiSupport.GetSafeValue(targetUser, "password"),
                    Service = MikroTikApiSupport.GetSafeValue(targetUser, "service"),
                    Address = MikroTikApiSupport.GetSafeValue(targetUser, "remote-address"),
                    ProfileName = MikroTikApiSupport.GetSafeValue(targetUser, "profile"),
                    ConnectionStatus = MikroTikApiSupport.GetSafeValue(targetUser, "disabled") == "true" ? "معطل" : "مفعل"
                };

                return client;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب معلومات المستخدم من المايكروتك: {Message}", ex.Message);
            throw new InvalidOperationException("خطأ في جلب معلومات المستخدم من المايكروتك", ex);
        }
    }

    /// <summary>
    /// جلب المستخدمين النشطين (المتصلين حالياً)
    /// </summary>
    public async Task<List<Client>> GetActivePPPoEUsers(int serverId)
    {
        _logger.LogInformation("🔍 بدء جلب المستخدمين النشطين للخادم {ServerId}", serverId);

        List<Client> result = [];

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            _logger.LogError("❌ الخادم غير موجود: {ServerId}", serverId);
            throw new InvalidOperationException("الخادم غير موجود");
        }

        _logger.LogInformation("🔗 محاولة الاتصال بالخادم: {Host}:{Port} باسم {User}", server.Host, server.Port, server.User);

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بنجاح");

                ITikCommand activeCmd = connection.CreateCommand("/ppp/active/print");
                IEnumerable<ITikReSentence> activeRows = activeCmd.ExecuteList();

                _logger.LogInformation("📊 تم العثور على {Count} مستخدم نشط", activeRows.Count());

                foreach (ITikReSentence? row in activeRows)
                {
                    string username = MikroTikApiSupport.GetSafeValue(row, "name");
                    _logger.LogDebug("مستخدم نشط: {Username}", username);

                    Client client = new()
                    {
                        UserName = username,
                        Address = MikroTikApiSupport.GetSafeValue(row, "address"),
                        Uptime = MikroTikApiSupport.GetSafeValue(row, "uptime"),
                        Service = MikroTikApiSupport.GetSafeValue(row, "service"),
                        ConnectionStatus = "نشط",
                        MacAddress = MikroTikApiSupport.GetSafeValue(row, "caller-id"),
                        MikroTikServerId = serverId,
                        LastUpdated = DateTime.Now
                    };

                    result.Add(client);
                }

                _logger.LogInformation("✅ تم جلب {Count} مستخدم نشط من الخادم {Host}", result.Count, server.Host);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في الاتصال بالخادم {Host}: {Message}", server.Host, ex.Message);
            throw new InvalidOperationException(
                MikroTikErrorFormatter.Format($"خطأ في الاتصال بالخادم {server.Host}", ex),
                ex);
        }

        return result;
    }

    /// <summary>
    /// جلب جميع مستخدمي PPPoE (من الإعدادات)
    /// </summary>
    public async Task<List<Client>> GetAllPPPoEUsers(int serverId)
    {
        _logger.LogInformation("🔍 بدء جلب جميع المستخدمين للخادم {ServerId}", serverId);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            _logger.LogError("❌ الخادم غير موجود: {ServerId}", serverId);
            throw new InvalidOperationException("الخادم غير موجود");
        }

        _logger.LogInformation("🔗 محاولة الاتصال بالخادم: {Host}:{Port} باسم {User}", server.Host, server.Port, server.User);

        try
        {
            List<Client> result = await _connection.ExecuteWithRetry(server, connection =>
            {
                ITikCommand secretCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> secretRows = secretCmd.ExecuteList();
                List<Client> users = [];

                _logger.LogInformation("📊 تم العثور على {Count} مستخدم في الإعدادات", secretRows.Count());

                foreach (ITikReSentence? row in secretRows)
                {
                    string username = MikroTikApiSupport.GetSafeValue(row, "name");
                    _logger.LogInformation("👤 معالجة المستخدم: {Username}", username);

                    Client client = new()
                    {
                        UserName = username,
                        Password = MikroTikApiSupport.GetSafeValue(row, "password"),
                        Service = MikroTikApiSupport.GetSafeValue(row, "service"),
                        Address = MikroTikApiSupport.GetSafeValue(row, "remote-address"),
                        ProfileName = MikroTikApiSupport.GetSafeValue(row, "profile"),
                        ConnectionStatus = MikroTikApiSupport.GetSafeValue(row, "disabled") == "true" ? "معطل" : "مفعل",
                        MikroTikServerId = serverId,
                        LastUpdated = DateTime.Now
                    };

                    users.Add(client);
                }

                return users;
            }, maxRetries: 3);

            _logger.LogInformation("✅ تم جلب {Count} مستخدم من إعدادات الخادم {Host}", result.Count, server.Host);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في الاتصال بالخادم {Host}: {Message}", server.Host, ex.Message);
            throw new InvalidOperationException(
                MikroTikErrorFormatter.Format($"خطأ في الاتصال بالخادم {server.Host}", ex),
                ex);
        }
    }

    /// <summary>
    /// قطع اتصال مستخدم نشط
    /// </summary>
    public async Task<bool> DisconnectActiveUser(int serverId, string username)
    {
        _logger.LogInformation("🔍 بدء قطع اتصال المستخدم النشط: {Username}", username);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            _logger.LogError("❌ الخادم غير موجود: {ServerId}", serverId);
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بنجاح");

                ITikCommand findCmd = connection.CreateCommand("/ppp/active/print");
                IEnumerable<ITikReSentence> allSessions = findCmd.ExecuteList();
                ITikReSentence? targetSession = allSessions.FirstOrDefault(s => MikroTikApiSupport.GetSafeValue(s, "name") == username);

                if (targetSession is null)
                {
                    _logger.LogWarning("⚠️ لا يوجد اتصال نشط للمستخدم {Username}", username);
                    throw new InvalidOperationException($"لا يوجد اتصال نشط للمستخدم {username}");
                }

                string sessionId = MikroTikApiSupport.GetSafeValue(targetSession, ".id");

                ITikCommand disconnectCmd = connection.CreateCommand("/ppp/active/remove");
                disconnectCmd.AddParameter(".id", sessionId);
                disconnectCmd.ExecuteNonQuery();

                _logger.LogInformation("✅ تم قطع اتصال المستخدم {Username} بنجاح", username);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في قطع اتصال المستخدم {Username}: {Message}", username, ex.Message);
            throw new InvalidOperationException("خطأ في قطع اتصال المستخدم", ex);
        }
    }

    /// <summary>
    /// تجميد حساب مستخدم (تعطيل)
    /// </summary>
    public async Task<bool> DisablePPPoEUser(int serverId, string username)
    {
        _logger.LogInformation("🔍 بدء تجميد حساب المستخدم: {Username}", username);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            _logger.LogError("❌ الخادم غير موجود: {ServerId}", serverId);
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == username);

                if (targetUser is null)
                {
                    _logger.LogWarning("⚠️ المستخدم {Username} غير موجود في المايكروتك", username);
                    throw new InvalidOperationException($"المستخدم {username} غير موجود في الخادم");
                }

                string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");

                ITikCommand disableCmd = connection.CreateCommand("/ppp/secret/set");
                disableCmd.AddParameter(".id", userId);
                disableCmd.AddParameter("disabled", "yes");
                disableCmd.ExecuteNonQuery();

                _logger.LogInformation("✅ تم تجميد حساب المستخدم {Username} بنجاح", username);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تجميد المستخدم {Username}: {Message}", username, ex.Message);
            throw new InvalidOperationException("خطأ في تجميد المستخدم", ex);
        }
    }

    /// <summary>
    /// تفعيل حساب مستخدم (إعادة التمكين)
    /// </summary>
    public async Task<bool> EnablePPPoEUser(int serverId, string username)
    {
        _logger.LogInformation("🔍 بدء تفعيل حساب المستخدم: {Username}", username);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            _logger.LogError("❌ الخادم غير موجود: {ServerId}", serverId);
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == username);

                if (targetUser is null)
                {
                    _logger.LogWarning("⚠️ المستخدم {Username} غير موجود في المايكروتك", username);
                    throw new InvalidOperationException($"المستخدم {username} غير موجود في الخادم");
                }

                string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");

                ITikCommand enableCmd = connection.CreateCommand("/ppp/secret/set");
                enableCmd.AddParameter(".id", userId);
                enableCmd.AddParameter("disabled", "no");
                enableCmd.ExecuteNonQuery();

                _logger.LogInformation("✅ تم تفعيل حساب المستخدم {Username} بنجاح", username);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تفعيل المستخدم {Username}: {Message}", username, ex.Message);
            throw new InvalidOperationException("خطأ في تفعيل المستخدم", ex);
        }
    }

    /// <summary>
    /// تجميد الحساب مع قطع الاتصال الحالي
    /// </summary>
    public async Task<bool> FreezeAccount(int serverId, string username)
    {
        _logger.LogInformation("🔍 بدء تجميد الحساب مع قطع الاتصال: {Username}", username);

        try
        {
            try
            {
                await DisconnectActiveUser(serverId, username);
                _logger.LogInformation("✅ تم قطع الاتصال النشط للمستخدم {Username}", username);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("⚠️ لا يوجد اتصال نشط لقطعه للمستخدم {Username}: {Message}", username, ex.Message);
            }

            await DisablePPPoEUser(serverId, username);

            _logger.LogInformation("✅ تم تجميد الحساب بنجاح للمستخدم {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تجميد الحساب: {Message}", ex.Message);
            throw new InvalidOperationException("خطأ في تجميد الحساب", ex);
        }
    }

    /// <summary>
    /// تفعيل الحساب
    /// </summary>
    public async Task<bool> UnfreezeAccount(int serverId, string username)
    {
        _logger.LogInformation("🔍 بدء تفعيل الحساب: {Username}", username);

        try
        {
            await EnablePPPoEUser(serverId, username);

            _logger.LogInformation("✅ تم تفعيل الحساب بنجاح للمستخدم {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تفعيل الحساب: {Message}", ex.Message);
            throw new InvalidOperationException("خطأ في تفعيل الحساب", ex);
        }
    }

    /// <summary>
    /// اختبار الاتصال بالخادم
    /// </summary>
    public async Task<bool> TestConnection(int serverId)
    {
        _logger.LogInformation("🔍 بدء اختبار الاتصال للخادم {ServerId}", serverId);

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            _logger.LogError("❌ الخادم غير موجود: {ServerId}", serverId);
            return false;
        }

        _logger.LogInformation("🔗 اختبار الاتصال بـ {Host}:{Port} باسم {User}", server.Host, server.Port, server.User);

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
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
            _logger.LogError(ex, "❌ فشل اختبار الاتصال بالخادم {Host}: {Message}", server.Host, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// تجديد اشتراك مستخدم PPPoE - تحديث تاريخ انتهاء الصلاحية
    /// </summary>
    public async Task<bool> RenewPPPoESubscription(string username, int serverId, DateTime? newExpirationDate)
    {
        _logger.LogInformation("🔄 بدء تجديد اشتراك المستخدم: {Username}", username);

        if (newExpirationDate is null)
        {
            throw new InvalidOperationException("يجب تحديد تاريخ انتهاء الصلاحية الجديد");
        }

        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = _connection.CreateConnectionWithRetry(server);
            {
                _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

                ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
                IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
                ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == username);

                if (targetUser is null)
                {
                    _logger.LogWarning("⚠️ المستخدم {Username} غير موجود في المايكروتك", username);
                    throw new InvalidOperationException($"المستخدم {username} غير موجود في الخادم");
                }

                string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");

                _logger.LogInformation("✅ تم تجديد اشتراك المستخدم {Username} حتى تاريخ {NewExpirationDate}", username, newExpirationDate.Value.ToString("yyyy/MM/dd"));

                Client? currentUser = await _context.Clients
                    .FirstOrDefaultAsync(c => c.UserName == username && c.MikroTikServerId == serverId);
                if (currentUser is not null && !currentUser.IsActive && newExpirationDate.Value > DateTime.Now)
                {
                    ITikCommand setCmd = connection.CreateCommand("/ppp/secret/set");
                    setCmd.AddParameter(".id", userId);
                    setCmd.AddParameter("disabled", "no");
                    setCmd.ExecuteNonQuery();
                    _logger.LogInformation("✅ تم تفعيل الحساب في MikroTik بعد التجديد");
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تجديد الاشتراك: {Message}", ex.Message);
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
        DateTime renewalDate = new(nextMonth.Year, nextMonth.Month, 8);

        return await RenewPPPoESubscription(username, serverId, renewalDate);
    }

    private async Task DisableExpiredAccount(string username, int serverId)
    {
        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server is null)
        {
            throw new InvalidOperationException("الخادم غير موجود");
        }

        try
        {
            using ITikConnection connection = ConnectionFactory.OpenConnection(
                TikConnectionType.Api,
                server.Host,
                server.Port,
                server.User,
                server.Pass);

            ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
            IEnumerable<ITikReSentence> allUsers = findCmd.ExecuteList();
            ITikReSentence? targetUser = allUsers.FirstOrDefault(u => MikroTikApiSupport.GetSafeValue(u, "name") == username);
            if (targetUser is not null)
            {
                string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");
                ITikCommand setCmd = connection.CreateCommand("/ppp/secret/set");
                setCmd.AddParameter(".id", userId);
                setCmd.AddParameter("disabled", "yes");
                setCmd.ExecuteNonQuery();
                _logger.LogInformation("تم إيقاف الحساب {Username} في MikroTik", username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في إيقاف الحساب {Username} في MikroTik", username);
            throw;
        }
    }

    /// <summary>
    /// التحقق من الحسابات المنتهية الصلاحية وإيقافها تلقائياً
    /// </summary>
    public async Task<ExpiredAccountsResult> CheckAndDisableExpiredAccounts()
    {
        _logger.LogInformation("🔍 بدء التحقق من الحسابات المنتهية الصلاحية");

        ExpiredAccountsResult result = new()
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

            foreach (Client client in clientsWithExpiration)
            {
                try
                {
                    if (client.MikroTikServerId is not null && !string.IsNullOrEmpty(client.UserName))
                    {
                        await DisableExpiredAccount(client.UserName, client.MikroTikServerId.Value);
                        result.DisabledInMikroTik++;
                    }

                    client.IsActive = false;
                    client.ConnectionStatus = "منتهي الصلاحية";
                    client.LastUpdated = DateTime.Now;

                    DateTime expirationDate = client.AccountExpirationDate ?? DateTime.Now;

                    result.DisabledAccounts.Add(new()
                    {
                        ClientId = client.Id,
                        ClientName = client.Name,
                        UserName = client.UserName,
                        ExpirationDate = expirationDate
                    });

                    _logger.LogInformation("✅ تم إيقاف حساب منتهي الصلاحية: {UserName} (انتهى في {ExpirationDate})", client.UserName, expirationDate.ToString("yyyy/MM/dd"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطأ في إيقاف حساب منتهي الصلاحية: {UserName}", client.UserName);
                    result.Errors.Add($"خطأ في إيقاف {client.UserName}: {ex.Message}");
                }
            }

            if (result.DisabledAccounts.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            result.Success = true;
            result.Message = $"تم التحقق من {result.ExpiredAccountsFound} حساب منتهي الصلاحية وتم إيقاف {result.DisabledAccounts.Count} حساب";

            _logger.LogInformation("✅ انتهى التحقق من الحسابات المنتهية: {Message}", result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ عام في التحقق من الحسابات المنتهية: {Message}", ex.Message);
            result.Success = false;
            result.Message = $"خطأ في التحقق من الحسابات المنتهية: {ex.Message}";
        }

        return result;
    }

    private static string GenerateUniqueSid() => DateTime.Now.Ticks.ToString()[^10..];
}

