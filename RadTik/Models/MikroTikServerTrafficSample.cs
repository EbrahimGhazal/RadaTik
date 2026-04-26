using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models;

public class MikroTikServerTrafficSample
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    [Required]
    public int MikroTikServerId { get; set; }

    [ForeignKey(nameof(MikroTikServerId))]
    public virtual MikroTikServer? MikroTikServer { get; set; }

    [Required]
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    public int InterfaceCount { get; set; }

    public double RxBps { get; set; }

    public double TxBps { get; set; }
}
