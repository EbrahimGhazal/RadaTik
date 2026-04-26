using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadTik.Dtos.MikroTik;
using RadTik.Models;

namespace RadTik.Services;

public partial class MikroTikService
{
    /// <summary>
    /// يطابق صف بروفايل من RouterOS مع سجل RadTik: أولاً بمعرف MikroTik الثابت (.id) ثم بالاسم.
    /// يمنع تكرار السجلات عندما يُغيّر المستخدم الاسم في التطبيق دون أن يتطابق بعد مع اسم السجل على الراوتر.
    /// </summary>
    private async Task<Profile?> FindProfileForMikroTikImportAsync(int serverId, int? networkId, MikroTikProfileInfo mt)
    {
        if (!string.IsNullOrEmpty(mt.Id))
        {
            var qById = _context.Profiles.Where(p => p.MikroTikServerId == serverId && p.MikroTikProfileId == mt.Id);
            if (networkId.HasValue)
            {
                qById = qById.Where(p => p.NetworkId == networkId.Value);
            }

            var byId = await qById.FirstOrDefaultAsync();
            if (byId != null)
            {
                return byId;
            }
        }

        var qByName = _context.Profiles.Where(p => p.MikroTikServerId == serverId && p.Name == mt.Name);
        if (networkId.HasValue)
        {
            qByName = qByName.Where(p => p.NetworkId == networkId.Value);
        }

        return await qByName.FirstOrDefaultAsync();
    }

    /// <summary>
    /// مزامنة من MikroTik إلى قاعدة البيانات (استيراد)
    /// </summary>
    public async Task<SyncResult> SyncFromMikroTikToDatabase(int serverId, bool importAsInactive = false, int? networkId = null, decimal defaultPrice = 100)
    {
        _logger.LogInformation($"🔍 مزامنة البروفايلات من المايكروتك إلى قاعدة البيانات للخادم {serverId}");

        var result = new SyncResult();
        var server = await _context.MikroTikServers.FindAsync(serverId);

        if (server == null)
        {
            result.Success = false;
            result.Message = "الخادم غير موجود";
            return result;
        }

        if (!networkId.HasValue && server.NetworkId.HasValue)
        {
            networkId = server.NetworkId.Value;
        }

        try
        {
            var mikrotikProfiles = await GetProfilesFromMikroTik(serverId);

            foreach (var mtProfile in mikrotikProfiles)
            {
                try
                {
                    var existingProfile = await FindProfileForMikroTikImportAsync(serverId, networkId, mtProfile);

                    if (existingProfile == null)
                    {
                        var (downloadValue, downloadUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, true);
                        var (uploadValue, uploadUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, false);

                        var newProfile = new Profile
                        {
                            Name = mtProfile.Name,
                            Description = $"مستورد من MikroTik - {DateTime.Now:yyyy-MM-dd}",
                            Type = GetProfileTypeFromService(mtProfile.Service),
                            BillingCycle = BillingCycle.Monthly,
                            Price = defaultPrice,
                            VATPercentage = 15,
                            DownloadSpeed = downloadValue,
                            DownloadSpeedUnit = downloadUnit,
                            UploadSpeed = uploadValue,
                            UploadSpeedUnit = uploadUnit,
                            MikroTikLocalAddress = mtProfile.LocalAddress,
                            MikroTikRemoteAddress = mtProfile.RemoteAddress,
                            MikroTikRateLimit = mtProfile.RateLimit,
                            MikroTikOnlyOne = mtProfile.OnlyOne,
                            MikroTikService = mtProfile.Service,
                            MikroTikServerId = serverId,
                            MikroTikProfileId = mtProfile.Id,
                            NetworkId = networkId,
                            IsSyncedWithMikroTik = true,
                            IsActive = !importAsInactive,
                            CreatedDate = DateTime.Now,
                            UpdatedDate = DateTime.Now,
                            LastSyncDate = DateTime.Now
                        };

                        _context.Profiles.Add(newProfile);
                        result.AddedCount++;
                        result.AddedProfiles.Add(mtProfile.Name);
                    }
                    else
                    {
                        var matchedByStableId = !string.IsNullOrEmpty(mtProfile.Id)
                            && string.Equals(existingProfile.MikroTikProfileId, mtProfile.Id, StringComparison.Ordinal);
                        var nameDiffersFromMt = !string.Equals(existingProfile.Name, mtProfile.Name, StringComparison.Ordinal);

                        existingProfile.MikroTikLocalAddress = mtProfile.LocalAddress ?? existingProfile.MikroTikLocalAddress;
                        existingProfile.MikroTikRemoteAddress = mtProfile.RemoteAddress ?? existingProfile.MikroTikRemoteAddress;
                        existingProfile.MikroTikRateLimit = mtProfile.RateLimit ?? existingProfile.MikroTikRateLimit;
                        existingProfile.MikroTikOnlyOne = mtProfile.OnlyOne;
                        existingProfile.MikroTikService = mtProfile.Service ?? existingProfile.MikroTikService;
                        existingProfile.MikroTikProfileId = mtProfile.Id;
                        existingProfile.IsSyncedWithMikroTik = true;
                        existingProfile.UpdatedDate = DateTime.Now;
                        existingProfile.LastSyncDate = DateTime.Now;

                        if (!matchedByStableId || !nameDiffersFromMt)
                        {
                            existingProfile.Name = mtProfile.Name;
                        }

                        if (!string.IsNullOrEmpty(mtProfile.RateLimit))
                        {
                            var (dlVal, dlUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, true);
                            var (ulVal, ulUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, false);
                            existingProfile.DownloadSpeed = dlVal;
                            existingProfile.DownloadSpeedUnit = dlUnit;
                            existingProfile.UploadSpeed = ulVal;
                            existingProfile.UploadSpeedUnit = ulUnit;
                        }

                        _context.Profiles.Update(existingProfile);
                        result.UpdatedCount++;
                        result.UpdatedProfiles.Add(existingProfile.Name);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.FailedProfiles.Add($"{mtProfile.Name}: {ex.Message}");
                    _logger.LogError(ex, "❌ فشل معالجة بروفايل مستورد {ProfileName}", mtProfile.Name);
                }
            }

            await _context.SaveChangesAsync();
            result.Success = true;
            result.Message = $"تمت المزامنة بنجاح: {result.AddedCount} مضافة، {result.UpdatedCount} محدثة، {result.FailedCount} فاشلة";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"فشلت المزامنة: {ex.Message}";
            _logger.LogError(ex, "❌ فشل مزامنة MikroTik -> Database للخادم {ServerId}", serverId);
        }

        return result;
    }

    /// <summary>
    /// مزامنة من قاعدة البيانات إلى MikroTik (تصدير)
    /// </summary>
    public async Task<SyncResult> SyncFromDatabaseToMikroTik(int serverId, int? networkId = null)
    {
        var result = new SyncResult();
        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            result.Success = false;
            result.Message = "الخادم غير موجود";
            return result;
        }

        if (!networkId.HasValue && server.NetworkId.HasValue)
        {
            networkId = server.NetworkId.Value;
        }

        try
        {
            var dbProfilesQuery = _context.Profiles.Where(p => p.MikroTikServerId == serverId && p.IsActive);
            if (networkId.HasValue)
            {
                dbProfilesQuery = dbProfilesQuery.Where(p => p.NetworkId == networkId.Value);
            }

            var dbProfiles = await dbProfilesQuery.ToListAsync();
            foreach (var dbProfile in dbProfiles)
            {
                try
                {
                    if (string.IsNullOrEmpty(dbProfile.MikroTikProfileId))
                    {
                        var mikrotikId = await AddProfileToMikroTik(serverId, dbProfile);
                        dbProfile.MikroTikProfileId = mikrotikId;
                        dbProfile.IsSyncedWithMikroTik = true;
                        dbProfile.LastSyncDate = DateTime.Now;
                        result.AddedCount++;
                        result.AddedProfiles.Add(dbProfile.Name);
                    }
                    else
                    {
                        await UpdateProfileInMikroTik(serverId, dbProfile);
                        dbProfile.IsSyncedWithMikroTik = true;
                        dbProfile.LastSyncDate = DateTime.Now;
                        result.UpdatedCount++;
                        result.UpdatedProfiles.Add(dbProfile.Name);
                    }
                }
                catch (Exception ex)
                {
                    dbProfile.IsSyncedWithMikroTik = false;
                    result.FailedCount++;
                    result.FailedProfiles.Add($"{dbProfile.Name}: {ex.Message}");
                    _logger.LogError(ex, "❌ فشل تصدير بروفايل {ProfileName} إلى MikroTik", dbProfile.Name);
                }

                _context.Profiles.Update(dbProfile);
            }

            await _context.SaveChangesAsync();
            result.Success = true;
            result.Message = $"تمت المزامنة بنجاح: {result.AddedCount} مضافة، {result.UpdatedCount} محدثة، {result.FailedCount} فاشلة";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"فشلت المزامنة: {ex.Message}";
            _logger.LogError(ex, "❌ فشل مزامنة Database -> MikroTik للخادم {ServerId}", serverId);
        }

        return result;
    }

    /// <summary>
    /// مزامنة ثنائية الاتجاه
    /// </summary>
    public async Task<SyncResult> TwoWaySync(int serverId, int? networkId = null, decimal defaultImportPrice = 100)
    {
        var result = new SyncResult();
        try
        {
            if (!networkId.HasValue)
            {
                var server = await _context.MikroTikServers.FindAsync(serverId);
                if (server?.NetworkId.HasValue == true)
                {
                    networkId = server.NetworkId.Value;
                }
            }

            var exportResult = await SyncFromDatabaseToMikroTik(serverId, networkId);
            var importResult = await SyncFromMikroTikToDatabase(serverId, false, networkId, defaultImportPrice);

            result.Success = importResult.Success && exportResult.Success;
            result.AddedCount = importResult.AddedCount + exportResult.AddedCount;
            result.UpdatedCount = importResult.UpdatedCount + exportResult.UpdatedCount;
            result.FailedCount = importResult.FailedCount + exportResult.FailedCount;
            result.AddedProfiles.AddRange(importResult.AddedProfiles);
            result.AddedProfiles.AddRange(exportResult.AddedProfiles);
            result.UpdatedProfiles.AddRange(importResult.UpdatedProfiles);
            result.UpdatedProfiles.AddRange(exportResult.UpdatedProfiles);
            result.FailedProfiles.AddRange(importResult.FailedProfiles);
            result.FailedProfiles.AddRange(exportResult.FailedProfiles);
            result.Message = $"تمت المزامنة الثنائية: {result.AddedCount} مضافة، {result.UpdatedCount} محدثة، {result.FailedCount} فاشلة";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"فشلت المزامنة الثنائية: {ex.Message}";
            _logger.LogError(ex, "❌ فشل المزامنة الثنائية للخادم {ServerId}", serverId);
        }

        return result;
    }

    /// <summary>
    /// استيراد جميع مستخدمي PPPoE من خادم محدد إلى قاعدة البيانات
    /// </summary>
    public async Task<ImportUsersResult> ImportAllUsersToDatabase(int serverId, int networkId)
    {
        _logger.LogInformation("🔍 بدء استيراد جميع مستخدمي PPPoE من الخادم {ServerId} إلى قاعدة البيانات للشبكة {NetworkId}", serverId, networkId);

        var result = new ImportUsersResult();

        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            result.Success = false;
            result.Message = "الخادم غير موجود";
            return result;
        }

        var addedClients = new List<Client>();

        try
        {
            var allUsers = await GetAllUsersWithDetails(serverId);

            foreach (var userVm in allUsers)
            {
                if (string.IsNullOrWhiteSpace(userVm.UserName))
                {
                    result.FailedCount++;
                    result.Errors.Add("تم تجاهل سجل بدون اسم مستخدم في المايكروتك");
                    _logger.LogWarning("⚠️ تم تجاهل مستخدم بدون اسم في الخادم {ServerId}", serverId);
                    continue;
                }

                try
                {
                    var existingClient = await _context.Clients
                        .FirstOrDefaultAsync(c =>
                            c.UserName == userVm.UserName &&
                            c.MikroTikServerId == serverId &&
                            c.NetworkId == networkId);

                    if (existingClient != null)
                    {
                        result.ExistingCount++;
                        continue;
                    }

                    Profile? profile = null;
                    if (!string.IsNullOrEmpty(userVm.ProfileName))
                    {
                        profile = await _context.Profiles
                            .FirstOrDefaultAsync(p =>
                                p.Name == userVm.ProfileName &&
                                p.MikroTikServerId == serverId &&
                                p.NetworkId == networkId);
                    }

                    if (profile == null)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"المستخدم {userVm.UserName}: لم يتم العثور على بروفايل مناسب ({userVm.ProfileName}) في قاعدة البيانات");
                        _logger.LogWarning("⚠️ تعذر استيراد المستخدم {UserName} لأن البروفايل {ProfileName} غير موجود في قاعدة البيانات", userVm.UserName, userVm.ProfileName);
                        continue;
                    }

                    string sid;
                    if (!string.IsNullOrWhiteSpace(userVm.SID) &&
                        System.Text.RegularExpressions.Regex.IsMatch(userVm.SID, @"^\d+$") &&
                        userVm.SID.Length <= 20)
                    {
                        sid = userVm.SID;
                    }
                    else
                    {
                        sid = GenerateUniqueSID();
                    }

                    string phoneNumber = userVm.PhoneNumber ?? "";
                    if (!string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        var cleaned = new string(phoneNumber.Where(ch => char.IsDigit(ch) || ch == '+' || ch == '-' || ch == ' ').ToArray());
                        phoneNumber = string.IsNullOrWhiteSpace(cleaned) ? "" : cleaned;
                    }
                    if (string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        phoneNumber = "0";
                    }
                    if (phoneNumber.Length > 15)
                    {
                        phoneNumber = phoneNumber.Substring(0, 15);
                    }

                    var client = new Client
                    {
                        Name = string.IsNullOrWhiteSpace(userVm.Name) ? userVm.UserName : userVm.Name,
                        SID = sid,
                        UserName = userVm.UserName,
                        Password = string.IsNullOrWhiteSpace(userVm.Password) ? GenerateDefaultPassword() : userVm.Password,
                        ProfileId = profile.Id,
                        ProfileName = profile.Name,
                        PhoneNumber = phoneNumber,
                        IsActive = userVm.IsActive,
                        ReceiverId = userVm.ReceiverId,
                        Service = string.IsNullOrEmpty(userVm.Service) ? "pppoe" : userVm.Service,
                        Address = userVm.Address,
                        ConnectionStatus = userVm.IsActive ? "مفعل" : "معطل",
                        MikroTikServerId = serverId,
                        CreatedDate = DateTime.Now,
                        LastUpdated = DateTime.Now,
                        AccountExpirationDate = userVm.AccountExpirationDate ?? DateTime.Now.AddMonths(1),
                        NetworkId = networkId
                    };

                    _context.Clients.Add(client);
                    addedClients.Add(client);
                    result.AddedCount++;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"المستخدم {userVm.UserName}: {ex.Message}");
                    _logger.LogError(ex, "❌ خطأ في استيراد المستخدم {UserName} إلى قاعدة البيانات", userVm.UserName);
                }
            }

            await _context.SaveChangesAsync();

            foreach (var client in addedClients)
            {
                try
                {
                    var existingUser = await _userManager.FindByNameAsync(client.UserName!);
                    if (existingUser != null)
                    {
                        if (existingUser.ClientId != null && existingUser.ClientId != client.Id)
                        {
                            result.UsersFailedCount++;
                            result.Errors.Add($"المستخدم {client.UserName}: اسم مستخدم مستخدم لحساب آخر");
                            continue;
                        }

                        if (existingUser.ClientId == null)
                        {
                            existingUser.ClientId = client.Id;
                            existingUser.NetworkId = client.NetworkId;
                            existingUser.IsActive = client.IsActive;
                            existingUser.FullName = string.IsNullOrWhiteSpace(client.Name) ? existingUser.FullName : client.Name;
                            if (!string.IsNullOrWhiteSpace(client.PhoneNumber))
                            {
                                existingUser.PhoneNumber = client.PhoneNumber;
                            }
                            await _userManager.UpdateAsync(existingUser);
                        }

                        if (existingUser.ClientId == client.Id)
                        {
                            if (!await _userManager.IsInRoleAsync(existingUser, "Client"))
                            {
                                await _userManager.AddToRoleAsync(existingUser, "Client");
                            }

                            if (!string.IsNullOrWhiteSpace(client.Password))
                            {
                                var token = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                                var resetResult = await _userManager.ResetPasswordAsync(existingUser, token, client.Password);
                                if (!resetResult.Succeeded)
                                {
                                    result.UsersFailedCount++;
                                    result.Errors.Add($"المستخدم {client.UserName}: تعذر مطابقة كلمة مرور المنصة مع كلمة مرور المايكروتك ({string.Join(", ", resetResult.Errors.Select(e => e.Description))})");
                                    _logger.LogWarning("⚠️ فشل تحديث كلمة مرور حساب المنصة للمشترك {UserName}: {Errors}", client.UserName, string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                                }
                            }
                            continue;
                        }
                    }

                    string userEmail = !string.IsNullOrWhiteSpace(client.UserName) && client.UserName!.Contains("@")
                        ? client.UserName
                        : $"{client.UserName}@radtik.local";

                    var appUser = new ApplicationUser
                    {
                        UserName = client.UserName,
                        Email = userEmail,
                        FullName = client.Name ?? client.UserName,
                        PhoneNumber = client.PhoneNumber ?? "0",
                        CreatedDate = DateTime.Now,
                        IsActive = client.IsActive,
                        ClientId = client.Id,
                        NetworkId = client.NetworkId
                    };

                    var createResult = await _userManager.CreateAsync(appUser, client.Password!);
                    if (!createResult.Succeeded)
                    {
                        result.UsersFailedCount++;
                        result.Errors.Add($"المستخدم {client.UserName}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                        _logger.LogWarning("⚠️ فشل إنشاء حساب نظام للمشترك {UserName}: {Errors}", client.UserName, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        continue;
                    }

                    await _userManager.AddToRoleAsync(appUser, "Client");
                    result.UsersCreatedCount++;
                    _logger.LogInformation("✅ تم إنشاء حساب نظام (دور عميل) للمشترك المستورد {UserName}", client.UserName);
                }
                catch (Exception ex)
                {
                    result.UsersFailedCount++;
                    result.Errors.Add($"المستخدم {client.UserName}: {ex.Message}");
                    _logger.LogError(ex, "❌ خطأ في إنشاء حساب نظام للمشترك المستورد {UserName}", client.UserName);
                }
            }

            result.Success = true;
            var userMsg = result.UsersCreatedCount > 0 || result.UsersFailedCount > 0
                ? $" تم إنشاء حسابات نظام (دور عميل) لـ {result.UsersCreatedCount} مشترك."
                : "";
            if (result.UsersFailedCount > 0)
                userMsg += $" فشل إنشاء حساب نظام لـ {result.UsersFailedCount} مشترك.";
            result.Message = $"تم استيراد {result.AddedCount} مستخدم جديد، تم تخطي {result.ExistingCount} مستخدم موجود مسبقاً، وفشل استيراد {result.FailedCount} مستخدم.{userMsg}";

            _logger.LogInformation("✅ {Message}", result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"فشل استيراد المستخدمين: {ex.Message}";
            _logger.LogError(ex, "❌ خطأ عام في استيراد جميع المستخدمين من الخادم {ServerId}", serverId);
        }

        return result;
    }

    public async Task<ImportUsersPreviewResult> BuildUsersImportPreviewAsync(int serverId, int networkId)
    {
        var preview = new ImportUsersPreviewResult();
        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            return preview;
        }

        var allUsers = await GetAllPPPoEUsers(serverId);
        preview.TotalUsersOnServer = allUsers.Count;

        var existingUserNames = (await _context.Clients.AsNoTracking()
            .Where(c => c.MikroTikServerId == serverId && c.NetworkId == networkId && !string.IsNullOrEmpty(c.UserName))
            .Select(c => c.UserName!).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var profileNames = (await _context.Profiles.AsNoTracking()
            .Where(p => p.MikroTikServerId == serverId && p.NetworkId == networkId && !string.IsNullOrEmpty(p.Name))
            .Select(p => p.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var user in allUsers)
        {
            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                preview.InvalidUsersCount++;
                continue;
            }
            if (existingUserNames.Contains(user.UserName))
            {
                preview.ExistingUsersCount++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(user.ProfileName) || !profileNames.Contains(user.ProfileName))
            {
                preview.MissingProfileCount++;
                continue;
            }
            preview.ImportableUsersCount++;
        }

        return preview;
    }

    public async Task<ImportProfilesPreviewResult> BuildProfilesImportPreviewAsync(int serverId, int networkId)
    {
        var preview = new ImportProfilesPreviewResult();
        var server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            return preview;
        }

        var mikrotikProfiles = await GetProfilesFromMikroTik(serverId);
        preview.TotalProfilesOnServer = mikrotikProfiles.Count;

        var existingNames = (await _context.Profiles.AsNoTracking()
            .Where(p => p.MikroTikServerId == serverId && p.NetworkId == networkId && !string.IsNullOrEmpty(p.Name))
            .Select(p => p.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mt in mikrotikProfiles)
        {
            if (existingNames.Contains(mt.Name))
            {
                preview.ExistingProfilesCount++;
            }
            else
            {
                preview.ImportableProfilesCount++;
            }
        }

        return preview;
    }
}
