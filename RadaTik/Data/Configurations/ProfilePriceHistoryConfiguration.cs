using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class ProfilePriceHistoryConfiguration : IEntityTypeConfiguration<ProfilePriceHistory>
{
    public void Configure(EntityTypeBuilder<ProfilePriceHistory> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.OldPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NewPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.OldVATPercentage).HasPrecision(5, 2);
        entity.Property(e => e.NewVATPercentage).HasPrecision(5, 2);
        entity.Property(e => e.ChangeDate).HasDefaultValueSql("GETDATE()");

        entity.HasOne(pph => pph.Profile)
            .WithMany(p => p.ProfilePriceHistories)
            .HasForeignKey(pph => pph.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
