using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>
/// محفظة منصة مدير النظام (صف واحد Id=1). تكمل نموذج المحافظ على مستوى الشبكة والمشترك.
/// </summary>
public class SystemAdminWallet
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceSyp { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceUsd { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
