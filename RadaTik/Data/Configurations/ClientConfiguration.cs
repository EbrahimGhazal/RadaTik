using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.SID).IsRequired().HasMaxLength(20);
        entity.Property(e => e.UserName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Password)
            .IsRequired()
            .HasMaxLength(512)
            .HasConversion(SensitiveDataConverters.NullableString);
        entity.Property(e => e.ProfileName).HasMaxLength(100);
        entity.Property(e => e.PhoneNumber).HasMaxLength(15);
        entity.Property(e => e.ResidenceAddress).HasMaxLength(500).IsRequired(false);
        entity.Property(e => e.Latitude).IsRequired(false);
        entity.Property(e => e.Longitude).IsRequired(false);
        entity.Property(e => e.Service).HasMaxLength(50).IsRequired(false);
        entity.Property(e => e.Address).HasMaxLength(50).IsRequired(false);
        entity.Property(e => e.Uptime).HasMaxLength(100).IsRequired(false);
        entity.Property(e => e.ConnectionStatus).HasMaxLength(50).IsRequired(false);
        entity.Property(e => e.MacAddress).HasMaxLength(50).IsRequired(false);
        entity.Property(e => e.PowerSource).HasMaxLength(100).IsRequired(false);
        entity.Property(e => e.Building).HasMaxLength(150).IsRequired(false);
        entity.Property(e => e.Floor).HasMaxLength(50).IsRequired(false);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.IsCrossServerDuplicate).HasDefaultValue(false);
        entity.Property(e => e.IsVip).HasDefaultValue(false);
        entity.Property(e => e.VipNote).HasMaxLength(200).IsRequired(false);
        entity.Property(e => e.ReceiverId).IsRequired(false);
        entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0);
        entity.Property(e => e.AccountCurrency).HasConversion<int>().HasDefaultValue(PricingCurrency.SYP_New);
        entity.Property(e => e.LastUpdated).HasDefaultValueSql("GETDATE()");
        entity.ConfigureBalanceRowVersion();

        entity.HasIndex(e => new { e.MikroTikServerId, e.UserName })
            .IsUnique()
            .HasFilter("[MikroTikServerId] IS NOT NULL");
        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => new { e.NetworkId, e.UserName, e.IsCrossServerDuplicate });

        entity.HasOne(c => c.Receiver)
            .WithMany(r => r.Clients)
            .HasForeignKey(c => c.ReceiverId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.HasOne(c => c.MikroTikServer)
            .WithMany()
            .HasForeignKey(c => c.MikroTikServerId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.HasOne(c => c.Profile)
            .WithMany(p => p.Clients)
            .HasForeignKey(c => c.ProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        entity.HasOne(c => c.Network)
            .WithMany(n => n.Clients)
            .HasForeignKey(c => c.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
