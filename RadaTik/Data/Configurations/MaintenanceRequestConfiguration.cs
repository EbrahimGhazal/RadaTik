using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class MaintenanceRequestConfiguration : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequest> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        entity.Property(e => e.RequestDate).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.Status).HasDefaultValue(MaintenanceRequestStatus.Pending);

        entity.HasOne(m => m.Client)
            .WithMany()
            .HasForeignKey(m => m.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(m => m.AssignedTo)
            .WithMany()
            .HasForeignKey(m => m.AssignedToId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        entity.HasOne(m => m.ProcessedBy)
            .WithMany()
            .HasForeignKey(m => m.ProcessedById)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);
    }
}
