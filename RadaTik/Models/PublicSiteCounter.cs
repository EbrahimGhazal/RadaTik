namespace RadaTik.Models;

/// <summary>عدادات عامة للموقع والتطبيقات (زوار وتحميلات).</summary>
public sealed class PublicSiteCounter
{
    public string Key { get; set; } = string.Empty;

    public long Count { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
