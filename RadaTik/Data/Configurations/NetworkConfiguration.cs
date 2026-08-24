using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class NetworkConfiguration : IEntityTypeConfiguration<Network>
{
    public void Configure(EntityTypeBuilder<Network> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Governorates).HasMaxLength(500);
        entity.Property(e => e.LogoPath).HasMaxLength(500);
        entity.Property(e => e.CreationDate).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.Status).HasDefaultValue(NetworkStatus.Active);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        entity.Property(e => e.DefaultUsdToSypExchangeRate).HasColumnType("decimal(18,4)");
        entity.Property(e => e.BalanceUsd).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        entity.Property(e => e.DefaultMaterialInvoiceCurrency).HasConversion<int>().HasDefaultValue(PricingCurrency.SYP_New);
        entity.Property(e => e.VipDiscountPercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        entity.Property(e => e.VipGraceDays).HasDefaultValue(0);
        entity.Property(e => e.VipSkipAutoDisable).HasDefaultValue(false);
        entity.Property(e => e.ManagerUserId).HasMaxLength(450);
        entity.ConfigureBalanceRowVersion();

        entity.HasIndex(e => e.Name).IsUnique();
        entity.HasIndex(e => e.ParentNetworkId);

        entity.HasOne(n => n.ParentNetwork)
            .WithMany(n => n.ChildNetworks)
            .HasForeignKey(n => n.ParentNetworkId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasOne(n => n.ManagerUser)
            .WithMany()
            .HasForeignKey(n => n.ManagerUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
