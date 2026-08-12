using RadaTik.Models;

namespace RadaTik.Services.MikroTik;

public interface IMikroTikSectorService
{
    Task<ImportSectorsPreviewResult> BuildSectorsImportPreviewAsync(int serverId, int networkId);

    Task<ImportSectorsResult> ImportSectorsFromMikroTikAsync(int serverId, int networkId);
}
