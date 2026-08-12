using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Dtos.MikroTik;
using RadaTik.Models;
using RadaTik.ViewModels.MikroTikServers;

namespace RadaTik.Services;

public partial class MikroTikService
{
    /// <summary>
    /// يطابق صف بروفايل من RouterOS مع سجل RadaTik: أولاً بمعرف MikroTik الثابت (.id) ثم بالاسم.
    /// يمنع تكرار السجلات عندما يُغيّر المستخدم الاسم في التطبيق دون أن يتطابق بعد مع اسم السجل على الراوتر.
    /// </summary>
    private async Task<Profile?> FindProfileForMikroTikImportAsync(int serverId, int? networkId, MikroTikProfileInfo mt)
    {
        if (!string.IsNullOrEmpty(mt.Id))
        {
            IQueryable<Profile> qById = _context.Profiles.Where(p => p.MikroTikServerId == serverId && p.MikroTikProfileId == mt.Id);
            if (networkId.HasValue)
            {
                qById = qById.Where(p => p.NetworkId == networkId.Value);
            }

            Profile? byId = await qById.FirstOrDefaultAsync();
            if (byId != null)
            {
                return byId;
            }
        }

        IQueryable<Profile> qByName = _context.Profiles.Where(p => p.MikroTikServerId == serverId && p.Name == mt.Name);
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

        SyncResult result = new SyncResult();
        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);

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
            List<MikroTikProfileInfo> mikrotikProfiles = await GetProfilesFromMikroTik(serverId);

            foreach (MikroTikProfileInfo mtProfile in mikrotikProfiles)
            {
                try
                {
                    Profile? existingProfile = await FindProfileForMikroTikImportAsync(serverId, networkId, mtProfile);

                    if (existingProfile == null)
                    {
                        (int downloadValue, SpeedUnit downloadUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, true);
                        (int uploadValue, SpeedUnit uploadUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, false);

                        Profile newProfile = new Profile
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
                        bool matchedByStableId = !string.IsNullOrEmpty(mtProfile.Id)
                            && string.Equals(existingProfile.MikroTikProfileId, mtProfile.Id, StringComparison.Ordinal);
                        bool nameDiffersFromMt = !string.Equals(existingProfile.Name, mtProfile.Name, StringComparison.Ordinal);

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
                            (int dlVal, SpeedUnit dlUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, true);
                            (int ulVal, SpeedUnit ulUnit) = ParseSpeedFromRateLimitToIntUnit(mtProfile.RateLimit, false);
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
        SyncResult result = new SyncResult();
        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
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
            IQueryable<Profile> dbProfilesQuery = _context.Profiles.Where(p => p.MikroTikServerId == serverId && p.IsActive);
            if (networkId.HasValue)
            {
                dbProfilesQuery = dbProfilesQuery.Where(p => p.NetworkId == networkId.Value);
            }

            List<Profile> dbProfiles = await dbProfilesQuery.ToListAsync();
            foreach (Profile? dbProfile in dbProfiles)
            {
                try
                {
                    if (string.IsNullOrEmpty(dbProfile.MikroTikProfileId))
                    {
                        string mikrotikId = await AddProfileToMikroTik(serverId, dbProfile);
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
        SyncResult result = new SyncResult();
        try
        {
            if (!networkId.HasValue)
            {
                MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
                if (server?.NetworkId.HasValue == true)
                {
                    networkId = server.NetworkId.Value;
                }
            }

            SyncResult exportResult = await SyncFromDatabaseToMikroTik(serverId, networkId);
            SyncResult importResult = await SyncFromMikroTikToDatabase(serverId, false, networkId, defaultImportPrice);

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

    public async Task<ImportProfilesPreviewResult> BuildProfilesImportPreviewAsync(int serverId, int networkId)
    {
        ImportProfilesPreviewResult preview = new ImportProfilesPreviewResult();
        MikroTikServer? server = await _context.MikroTikServers.FindAsync(serverId);
        if (server == null)
        {
            return preview;
        }

        List<MikroTikProfileInfo> mikrotikProfiles = await GetProfilesFromMikroTik(serverId);
        preview.TotalProfilesOnServer = mikrotikProfiles.Count;

        HashSet<string> existingNames = (await _context.Profiles.AsNoTracking()
            .Where(p => p.MikroTikServerId == serverId && p.NetworkId == networkId && !string.IsNullOrEmpty(p.Name))
            .Select(p => p.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (MikroTikProfileInfo mt in mikrotikProfiles)
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
