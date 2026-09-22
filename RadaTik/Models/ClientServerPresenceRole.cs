namespace RadaTik.Models;

/// <summary>
/// دور حضور حساب PPPoE على سيرفر MikroTik ضمن نفس المشترك.
/// </summary>
public enum ClientServerPresenceRole
{
    /// <summary>البرج الأساسي (منزل المشترك).</summary>
    Primary = 0,

    /// <summary>نسخة احتياطية أثناء failover أو تواجد دائم على برج آخر.</summary>
    Standby = 1
}
