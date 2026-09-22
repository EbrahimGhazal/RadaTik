using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RadaTik.Models;

/// <summary>
/// حضور حساب PPPoE لمشترك واحد على سيرفر MikroTik (أساسي أو احتياطي).
/// المحفظة والبوابة تبقى على سجل <see cref="Client"/> الواحد.
/// </summary>
public sealed class ClientServerPresence
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int ClientId { get; set; }

    public int MikroTikServerId { get; set; }

    public ClientServerPresenceRole Role { get; set; } = ClientServerPresenceRole.Standby;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastSeenActiveUtc { get; set; }

    [ForeignKey(nameof(ClientId))]
    [ValidateNever]
    public Client? Client { get; set; }

    [ForeignKey(nameof(MikroTikServerId))]
    [ValidateNever]
    public MikroTikServer? MikroTikServer { get; set; }
}
