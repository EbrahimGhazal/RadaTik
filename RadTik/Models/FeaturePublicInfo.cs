using System.ComponentModel.DataAnnotations;

namespace RadTik.Models;

/// <summary>
/// محتوى عرض الخدمة لمدير الشركة (نافذة التفاصيل): شرح العمل وسياسة التسعير — يحرره مدير النظام.
/// </summary>
public class FeaturePublicInfo
{
    [Key]
    [StringLength(100)]
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>شرح عمل الخدمة بالكامل (HTML آمن من مدير النظام).</summary>
    public string? DetailHtml { get; set; }

    /// <summary>سياسة التسعير والفوترة (HTML).</summary>
    public string? PricingPolicyHtml { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
