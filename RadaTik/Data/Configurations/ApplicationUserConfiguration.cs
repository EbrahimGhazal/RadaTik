using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.HasOne(u => u.Client)
            .WithMany()
            .HasForeignKey(u => u.ClientId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.HasOne(u => u.Network)
            .WithMany(n => n.Users)
            .HasForeignKey(u => u.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
