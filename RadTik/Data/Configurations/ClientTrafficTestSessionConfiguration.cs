using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class ClientTrafficTestSessionConfiguration : IEntityTypeConfiguration<ClientTrafficTestSession>
{
    public void Configure(EntityTypeBuilder<ClientTrafficTestSession> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.ClientId, e.StartedAtUtc });
        entity.HasIndex(e => e.StartedAtUtc);

        entity.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
