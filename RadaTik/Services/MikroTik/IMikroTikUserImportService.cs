using RadaTik.Models;

namespace RadaTik.Services.MikroTik;

/// <summary>استيراد مستخدمي PPPoE من MikroTik إلى قاعدة البيانات.</summary>
public interface IMikroTikUserImportService
{
    Task<ImportUsersResult> ImportAllUsersToDatabase(int serverId, int networkId);

    Task<ImportUsersPreviewResult> BuildUsersImportPreviewAsync(int serverId, int networkId);
}
