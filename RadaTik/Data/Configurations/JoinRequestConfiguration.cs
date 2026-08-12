using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class JoinRequestConfiguration : IEntityTypeConfiguration<JoinRequest>
{
    public void Configure(EntityTypeBuilder<JoinRequest> entity)
    {
        entity.Property(e => e.RequestedPassword)
            .HasMaxLength(512)
            .HasConversion(SensitiveDataConverters.NullableString);
    }
}
