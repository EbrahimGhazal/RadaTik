using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SpeedChangeRequestConfiguration : IEntityTypeConfiguration<SpeedChangeRequest>
{
    public void Configure(EntityTypeBuilder<SpeedChangeRequest> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.RequestDate).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.Status).HasDefaultValue(SpeedChangeRequestStatus.Pending);
        entity.Property(e => e.PriceDifference).HasColumnType("decimal(18,2)");

        entity.HasOne(s => s.Client)
            .WithMany()
            .HasForeignKey(s => s.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(s => s.CurrentProfile)
            .WithMany()
            .HasForeignKey(s => s.CurrentProfileId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(s => s.RequestedProfile)
            .WithMany()
            .HasForeignKey(s => s.RequestedProfileId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(s => s.ProcessedBy)
            .WithMany()
            .HasForeignKey(s => s.ProcessedById)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        entity.HasOne(s => s.ImplementedBy)
            .WithMany()
            .HasForeignKey(s => s.ImplementedById)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);
    }
}
