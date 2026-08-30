using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class PublicSiteCounterConfiguration : IEntityTypeConfiguration<PublicSiteCounter>
{
    public void Configure(EntityTypeBuilder<PublicSiteCounter> entity)
    {
        entity.ToTable("PublicSiteCounters");
        entity.HasKey(e => e.Key);
        entity.Property(e => e.Key).HasMaxLength(64);
        entity.Property(e => e.UpdatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
