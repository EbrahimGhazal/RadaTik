using Microsoft.EntityFrameworkCore;

namespace RadaTik.Models;

/// <summary>سطر موحّد من عرض vw_WalletLedgerUnified (قراءة فقط للتقارير).</summary>
[Keyless]
public class WalletLedgerUnifiedEntry
{
    public string LedgerSource { get; set; } = string.Empty;

    public long SourceId { get; set; }

    public int? NetworkId { get; set; }

    public decimal Amount { get; set; }

    public string? CurrencyCode { get; set; }

    public DateTime OccurredAt { get; set; }

    public string? Category { get; set; }

    public string? Notes { get; set; }
}
