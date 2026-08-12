using RadaTik.Dtos.MikroTik;

namespace RadaTik.Services.MikroTik;

/// <summary>مزامنة باقات PPPoE بين MikroTik وقاعدة البيانات.</summary>
public interface IMikroTikProfileSyncService
{
    Task<SyncResult> SyncFromMikroTikToDatabase(
        int serverId,
        bool importAsInactive = false,
        int? networkId = null,
        decimal defaultPrice = 100);

    Task<SyncResult> SyncFromDatabaseToMikroTik(int serverId, int? networkId = null);

    Task<SyncResult> TwoWaySync(int serverId, int? networkId = null, decimal defaultImportPrice = 100);
}
