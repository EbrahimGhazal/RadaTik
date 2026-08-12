using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>سجل لمنع تكرار نفس التذكير لنفس دورة الانتهاء.</summary>
public class ClientRenewalReminderSendLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public virtual Client Client { get; set; } = null!;

    /// <summary>شبكة الشركة الرئيسية المرتبطة بالإعدادات.</summary>
    public int CompanyNetworkId { get; set; }

    [Column(TypeName = "date")]
    public DateTime ExpirationDate { get; set; }

    /// <summary>3 أو 4 أو 5 أيام قبل الانتهاء.</summary>
    public byte DaysBefore { get; set; }

    public RenewalReminderChannel Channel { get; set; }

    public DateTime SentAtUtc { get; set; }

    public bool Success { get; set; } = true;

    [StringLength(500)]
    public string? ErrorMessage { get; set; }
}
