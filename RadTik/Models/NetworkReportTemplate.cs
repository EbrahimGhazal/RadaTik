using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadTik.ViewModels.CompanyAdmin;

namespace RadTik.Models;

/// <summary>
/// قالب تقرير مخصص لكل شركة ونوع تقرير — يدعم ثوابت يكتبها مدير الشركة ومتغيرات {{Name}} تُستبدل من النظام.
/// </summary>
public class NetworkReportTemplate
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>الشركة الرئيسية (ParentNetworkId == null).</summary>
    [Required]
    public int CompanyNetworkId { get; set; }

    [ForeignKey(nameof(CompanyNetworkId))]
    public virtual Network? CompanyNetwork { get; set; }

    [Required]
    public CompanyReportKind ReportKind { get; set; }

    /// <summary>
    /// HTML أو نص بسيط. استخدم {{DATA_TABLE}} لموضع جدول البيانات المُولَّد من التطبيق.
    /// متغيرات شائعة: {{CompanyName}} {{NetworkName}} {{ReportTitle}} {{PeriodFrom}} {{PeriodTo}} {{RowCount}} {{GeneratedAt}} {{ManagerName}}
    /// </summary>
    public string? BodyContent { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? UpdatedByUserId { get; set; }
}
