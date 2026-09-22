using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class ClientServerPresenceConfiguration : IEntityTypeConfiguration<ClientServerPresence>
{
    public void Configure(EntityTypeBuilder<ClientServerPresence> entity)
    {
        entity.ToTable("ClientServerPresences");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Role).HasConversion<byte>();
        entity.HasIndex(e => new { e.ClientId, e.MikroTikServerId }).IsUnique();
        entity.HasIndex(e => e.MikroTikServerId);
        entity.HasOne(e => e.Client)
            .WithMany(c => c.ServerPresences)
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.MikroTikServer)
            .WithMany()
            .HasForeignKey(e => e.MikroTikServerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
