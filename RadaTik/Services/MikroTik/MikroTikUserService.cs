using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.ViewModels.MikroTikServers;
using System.Globalization;
using tik4net;

namespace RadaTik.Services.MikroTik;

public sealed class MikroTikUserService(
    ApplicationDbContext context,
    ILogger<MikroTikUserService> logger,
    IMikroTikProfilesService profiles,
    MikroTikConnectionSupport connection,
    IClientVipPolicyService vipPolicy) : IMikroTikPppoeUserService
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<MikroTikUserService> _logger = logger;
    private readonly IMikroTikProfilesService _profiles = profiles;
    private readonly MikroTikConnectionSupport _connection = connection;
    private readonly IClientVipPolicyService _vipPolicy = vipPolicy;

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
                    IsImportedFromServer = true,
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
            return MikroTikApiSupport.FindByName(connection, "/ppp/secret/print", username) is not null;
        }
        catch (Exception ex) when (MikroTikApiSupport.IsEmptyResponse(ex))
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في فحص وجود المستخدم: {Message}", ex.Message);
            throw;
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

            if (string.IsNullOrWhiteSpace(client.UserName))
            {
                throw new InvalidOperationException("اسم المستخدم مطلوب لإضافته على المايكروتك");
            }

            ITikReSentence? existingUser = MikroTikApiSupport.FindByName(
                connection,
                "/ppp/secret/print",
                client.UserName);

            string? profileName = ResolveProfileName(client);
            if (string.IsNullOrEmpty(profileName))
            {
                throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
            }

            string mikrotikProfileName = EnsurePppProfileName(connection, client, profileName);
            client.ProfileName = mikrotikProfileName;

            if (existingUser is not null)
            {
                ApplySecretSet(connection, existingUser, client, mikrotikProfileName);
                _logger.LogInformation(
                    "المستخدم {UserName} موجود مسبقاً — تم تحديث كلمة المرور والبروفايل من قاعدة البيانات",
                    client.UserName);
                return true;
            }

            try
            {
                ITikCommand addCmd = connection.CreateCommand("/ppp/secret/add");
                addCmd.AddParameter("name", client.UserName);
                addCmd.AddParameter("password", client.Password);
                addCmd.AddParameter("service", "pppoe");
                addCmd.AddParameter("profile", mikrotikProfileName);

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
            catch (Exception cmdEx) when (
                MikroTikApiSupport.IsEmptyResponse(cmdEx)
                || MikroTikApiSupport.IsAlreadyExistsMessage(cmdEx))
            {
                _logger.LogWarning(
                    "تعذر تأكيد أمر الإضافة للمستخدم {UserName}، سيتم التحقق بالاسم فقط",
                    client.UserName);
            }

            ITikReSentence? verifyUser = MikroTikApiSupport.FindByName(
                connection,
                "/ppp/secret/print",
                client.UserName);
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
        catch (Exception ex) when (MikroTikApiSupport.IsEmptyResponse(ex))
        {
            _logger.LogWarning(ex, "رد !empty أثناء إضافة {UserName} — يُعاد التحقق بالاسم", client.UserName);
            ITikReSentence? verifyUser = connection is null
                ? null
                : MikroTikApiSupport.FindByName(connection, "/ppp/secret/print", client.UserName ?? string.Empty);
            if (verifyUser is not null)
            {
                return true;
            }

            throw new InvalidOperationException("خطأ في إضافة المستخدم في المايكروتك", ex);
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

    public async Task<BulkAddPppoeUsersResult> AddPPPoEUsersToServerAsync(
        int serverId,
        IReadOnlyList<Client> clients,
        CancellationToken ct = default)
    {
        if (clients == null || clients.Count == 0)
        {
            return BulkAddPppoeUsersResult.Fail("لا توجد حسابات لنسخها.");
        }

        MikroTikServer? server = await _context.MikroTikServers.FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null)
        {
            return BulkAddPppoeUsersResult.Fail("الخادم غير موجود");
        }

        ITikConnection? connection = null;
        try
        {
            connection = _connection.CreateConnectionWithRetry(server);

            ITikCommand secretsCmd = connection.CreateCommand("/ppp/secret/print");
            HashSet<string> existingNames = secretsCmd.ExecuteList()
                .Select(u => MikroTikApiSupport.GetSafeValue(u, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ITikCommand profileCmd = connection.CreateCommand("/ppp/profile/print");
            HashSet<string> existingProfiles = profileCmd.ExecuteList()
                .Select(p => MikroTikApiSupport.GetSafeValue(p, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int added = 0;
            int skippedExisting = 0;
            int skippedInvalid = 0;
            int failed = 0;
            List<int> placedIds = [];
            List<string> errors = [];

            foreach (Client client in clients)
            {
                ct.ThrowIfCancellationRequested();

                string? userName = client.UserName?.Trim();
                string? password = client.Password;
                string? profileName = ResolveProfileName(client);
                string label = !string.IsNullOrWhiteSpace(client.Name) ? client.Name! : (userName ?? $"#{client.Id}");

                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
                {
                    skippedInvalid++;
                    errors.Add($"{label}: اسم المستخدم أو كلمة المرور غير مكتمل في قاعدة البيانات.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(profileName))
                {
                    skippedInvalid++;
                    errors.Add($"{label}: لا يوجد بروفايل للحساب.");
                    continue;
                }

                if (!existingProfiles.Contains(profileName))
                {
                    failed++;
                    errors.Add($"{label}: البروفايل «{profileName}» غير موجود في السيرفر.");
                    continue;
                }

                if (existingNames.Contains(userName))
                {
                    skippedExisting++;
                    placedIds.Add(client.Id);
                    continue;
                }

                try
                {
                    ITikCommand addCmd = connection.CreateCommand("/ppp/secret/add");
                    addCmd.AddParameter("name", userName);
                    addCmd.AddParameter("password", password);
                    addCmd.AddParameter("service", "pppoe");
                    addCmd.AddParameter("profile", profileName);

                    if (!string.IsNullOrWhiteSpace(client.Address))
                    {
                        addCmd.AddParameter("remote-address", client.Address);
                    }

                    if (!string.IsNullOrWhiteSpace(client.Name))
                    {
                        string comment = client.Name.Trim();
                        if (comment.Length > 60)
                        {
                            comment = comment[..60];
                        }

                        addCmd.AddParameter("comment", comment);
                    }

                    if (!client.IsActive)
                    {
                        addCmd.AddParameter("disabled", "yes");
                    }

                    addCmd.ExecuteNonQuery();
                    existingNames.Add(userName);
                    added++;
                    placedIds.Add(client.Id);
                }
                catch (Exception cmdEx) when (cmdEx.Message.Contains("!empty", StringComparison.Ordinal))
                {
                    existingNames.Add(userName);
                    added++;
                    placedIds.Add(client.Id);
                }
                catch (Exception cmdEx)
                {
                    failed++;
                    errors.Add($"{label}: {cmdEx.Message}");
                    _logger.LogWarning(cmdEx, "فشل نسخ الحساب {UserName} إلى السيرفر {ServerId}", userName, serverId);
                }
            }

            return new BulkAddPppoeUsersResult
            {
                Success = true,
                AddedCount = added,
                SkippedExistingCount = skippedExisting,
                SkippedInvalidCount = skippedInvalid,
                FailedCount = failed,
                PlacedClientIds = placedIds,
                Errors = errors,
                Message = $"تمت إضافة {added} حساباً إلى السيرفر."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل نسخ الحسابات إلى سيرفر MikroTik {ServerId}", serverId);
            return BulkAddPppoeUsersResult.Fail(
                MikroTikErrorFormatter.Format("تعذر الاتصال بالسيرفر أو تنفيذ النسخ", ex.Message));
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private static string? ResolveProfileName(Client client)
    {
        if (!string.IsNullOrWhiteSpace(client.Profile?.Name))
        {
            return client.Profile.Name.Trim();
        }

        return string.IsNullOrWhiteSpace(client.ProfileName) ? null : client.ProfileName.Trim();
    }

    private string EnsurePppProfileName(ITikConnection connection, Client client, string profileName)
    {
        IReadOnlyList<ITikReSentence> rows = MikroTikApiSupport.PrintPppProfiles(connection);
        ITikReSentence? match = MikroTikApiSupport.FindInPrint(rows, profileName);
        if (match is not null)
        {
            return MikroTikApiSupport.ActualName(match) ?? profileName;
        }

        string available = string.Join("، ", rows
            .Select(MikroTikApiSupport.ActualName)
            .Where(name => !string.IsNullOrWhiteSpace(name)));
        _logger.LogWarning(
            "البروفايل {ProfileName} غير ظاهر في قائمة PPP ({Count}): {Available}. سيتم إنشاؤه من قاعدة البيانات.",
            profileName,
            rows.Count,
            string.IsNullOrWhiteSpace(available) ? "(فارغة)" : available);

        CreatePppProfile(connection, client, profileName);

        rows = MikroTikApiSupport.PrintPppProfiles(connection);
        match = MikroTikApiSupport.FindInPrint(rows, profileName);
        if (match is not null)
        {
            return MikroTikApiSupport.ActualName(match) ?? profileName;
        }

        _logger.LogWarning(
            "تعذر التحقق من البروفايل {ProfileName} بعد إنشائه — سيُستخدم الاسم كما في قاعدة البيانات",
            profileName);
        return profileName;
    }

    private void CreatePppProfile(ITikConnection connection, Client client, string profileName)
    {
        ITikCommand addCmd = connection.CreateCommand("/ppp/profile/add");
        addCmd.AddParameter("name", profileName);

        Profile? profile = client.Profile;
        string? rateLimit = BuildRateLimit(profile, profileName);
        if (!string.IsNullOrWhiteSpace(rateLimit))
        {
            addCmd.AddParameter("rate-limit", rateLimit);
        }

        if (!string.IsNullOrWhiteSpace(profile?.MikroTikLocalAddress))
        {
            addCmd.AddParameter("local-address", profile.MikroTikLocalAddress);
        }

        if (!string.IsNullOrWhiteSpace(profile?.MikroTikRemoteAddress))
        {
            addCmd.AddParameter("remote-address", profile.MikroTikRemoteAddress);
        }

        addCmd.AddParameter("only-one", profile is null || profile.MikroTikOnlyOne ? "yes" : "no");

        try
        {
            addCmd.ExecuteNonQuery();
            _logger.LogInformation("تم إنشاء بروفايل PPP {ProfileName} على المايكروتك", profileName);
        }
        catch (Exception ex) when (
            MikroTikApiSupport.IsEmptyResponse(ex) || MikroTikApiSupport.IsAlreadyExistsMessage(ex))
        {
            _logger.LogInformation(
                ex,
                "البروفايل {ProfileName} موجود مسبقاً أو أرجع الجهاز !empty — يُتابع الاستخدام",
                profileName);
        }
    }

    private static string? BuildRateLimit(Profile? profile, string profileName)
    {
        if (!string.IsNullOrWhiteSpace(profile?.MikroTikRateLimit))
        {
            return profile.MikroTikRateLimit.Trim();
        }

        if (profile is { DownloadSpeed: > 0 })
        {
            string download = FormatMikroTikSpeed(profile.DownloadSpeed, profile.DownloadSpeedUnit);
            string upload = profile.UploadSpeed is > 0
                ? FormatMikroTikSpeed(profile.UploadSpeed.Value, profile.UploadSpeedUnit ?? profile.DownloadSpeedUnit)
                : download;
            return $"{download}/{upload}";
        }

        decimal? mbps = MikroTikApiSupport.ParseSpeedMbps(profileName);
        if (mbps is null or <= 0)
        {
            return null;
        }

        string formatted = mbps.Value.ToString("0.##", CultureInfo.InvariantCulture) + "M";
        return $"{formatted}/{formatted}";
    }

    private static string FormatMikroTikSpeed(int value, SpeedUnit unit) => unit switch
    {
        SpeedUnit.Kbps => $"{value}k",
        SpeedUnit.Gbps => $"{value}G",
        _ => $"{value}M"
    };

    private static void ApplySecretSet(
        ITikConnection connection,
        ITikReSentence existingUser,
        Client client,
        string mikrotikProfileName)
    {
        string userId = MikroTikApiSupport.GetSafeValue(existingUser, ".id");
        ITikCommand updateCmd = connection.CreateCommand("/ppp/secret/set");
        updateCmd.AddParameter(".id", userId);
        if (!string.IsNullOrEmpty(client.Password))
        {
            updateCmd.AddParameter("password", client.Password);
        }

        updateCmd.AddParameter("profile", mikrotikProfileName);
        updateCmd.AddParameter("disabled", client.IsActive ? "no" : "yes");
        if (!string.IsNullOrEmpty(client.Address))
        {
            updateCmd.AddParameter("remote-address", client.Address);
        }

        try
        {
            updateCmd.ExecuteNonQuery();
        }
        catch (Exception cmdEx) when (MikroTikApiSupport.IsEmptyResponse(cmdEx))
        {
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
            _logger.LogInformation("✅ تم إنشاء الاتصال بالخادم");

            string? profileName = ResolveProfileName(client);
            if (string.IsNullOrEmpty(profileName))
            {
                throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
            }

            ITikReSentence? targetUser = MikroTikApiSupport.FindByName(
                connection,
                "/ppp/secret/print",
                client.UserName ?? string.Empty);
            if (targetUser is null)
            {
                _logger.LogWarning("⚠️ المستخدم {UserName} غير موجود في المايكروتك — ستتم إضافته", client.UserName);
                await AddPPPoEUser(client);
                return true;
            }

            string mikrotikProfileName = EnsurePppProfileName(connection, client, profileName);
            ApplySecretSet(connection, targetUser, client, mikrotikProfileName);

            _logger.LogInformation("✅ تم تحديث المستخدم {UserName} في المايكروتك بنجاح", client.UserName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث المستخدم في المايكروتك: {Message}", ex.Message);
            throw;
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

                string? profileName = ResolveProfileName(client);
                if (string.IsNullOrEmpty(profileName))
                {
                    throw new InvalidOperationException("لم يتم تحديد بروفايل للعميل");
                }

                string mikrotikProfileName = EnsurePppProfileName(connection, client, profileName);

                ITikCommand updateCmd = connection.CreateCommand("/ppp/secret/set");
                updateCmd.AddParameter(".id", userId);
                updateCmd.AddParameter("name", newUserName);
                updateCmd.AddParameter("profile", mikrotikProfileName);
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
            ITikReSentence? targetUser = MikroTikApiSupport.FindByName(
                connection,
                "/ppp/secret/print",
                username);

            if (targetUser is null)
            {
                _logger.LogWarning("⚠️ المستخدم {Username} غير موجود في المايكروتك", username);
                return true;
            }

            string userId = MikroTikApiSupport.GetSafeValue(targetUser, ".id");
            ITikCommand deleteCmd = connection.CreateCommand("/ppp/secret/remove");
            deleteCmd.AddParameter(".id", userId);
            try
            {
                deleteCmd.ExecuteNonQuery();
            }
            catch (Exception cmdEx) when (cmdEx.Message.Contains("!empty", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("تم تجاهل !empty أثناء حذف {Username} — يُعتبر الحذف ناجحاً", username);
            }

            _logger.LogInformation("✅ تم حذف المستخدم {Username} من المايكروتك بنجاح", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في حذف المستخدم من المايكروتك: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<BulkDeletePppoeUsersResult> DeletePPPoEUsersFromServerAsync(
        int serverId,
        IReadOnlyList<string> usernames,
        CancellationToken ct = default)
    {
        string[] names = (usernames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (names.Length == 0)
        {
            return new BulkDeletePppoeUsersResult { Success = true, Message = "لا توجد حسابات للحذف." };
        }

        MikroTikServer? server = await _context.MikroTikServers.FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null)
        {
            return BulkDeletePppoeUsersResult.Fail("الخادم غير موجود");
        }

        ITikConnection? connection = null;
        try
        {
            connection = _connection.CreateConnectionWithRetry(server);
            ITikCommand findCmd = connection.CreateCommand("/ppp/secret/print");
            Dictionary<string, string> idsByName = findCmd.ExecuteList()
                .Select(row => (
                    Name: MikroTikApiSupport.GetSafeValue(row, "name"),
                    Id: MikroTikApiSupport.GetSafeValue(row, ".id")))
                .Where(row => !string.IsNullOrWhiteSpace(row.Name) && !string.IsNullOrWhiteSpace(row.Id))
                .GroupBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            int deleted = 0;
            int notFound = 0;
            int failed = 0;
            List<string> errors = [];

            foreach (string userName in names)
            {
                ct.ThrowIfCancellationRequested();
                if (!idsByName.TryGetValue(userName, out string? secretId))
                {
                    notFound++;
                    continue;
                }

                try
                {
                    ITikCommand deleteCmd = connection.CreateCommand("/ppp/secret/remove");
                    deleteCmd.AddParameter(".id", secretId);
                    deleteCmd.ExecuteNonQuery();
                    deleted++;
                }
                catch (Exception cmdEx) when (cmdEx.Message.Contains("!empty", StringComparison.Ordinal))
                {
                    deleted++;
                }
                catch (Exception cmdEx)
                {
                    failed++;
                    errors.Add($"{userName}: {cmdEx.Message}");
                    _logger.LogWarning(cmdEx, "فشل حذف الحساب {UserName} من السيرفر {ServerId}", userName, serverId);
                }
            }

            return new BulkDeletePppoeUsersResult
            {
                Success = true,
                DeletedCount = deleted,
                NotFoundCount = notFound,
                FailedCount = failed,
                Errors = errors,
                Message = $"تم حذف {deleted} حساباً من السيرفر."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل حذف الحسابات من سيرفر MikroTik {ServerId}", serverId);
            return BulkDeletePppoeUsersResult.Fail(
                MikroTikErrorFormatter.Format("تعذر حذف الحسابات من البرج القديم", ex.Message));
        }
        finally
        {
            connection?.Dispose();
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
                    if (await _vipPolicy.IsProtectedFromAutoDisableAsync(client, DateTime.Now))
                    {
                        _logger.LogInformation(
                            "تخطي الفصل التلقائي لمشترك VIP: {UserName} (انتهى في {ExpirationDate})",
                            client.UserName,
                            client.AccountExpirationDate);
                        continue;
                    }

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

