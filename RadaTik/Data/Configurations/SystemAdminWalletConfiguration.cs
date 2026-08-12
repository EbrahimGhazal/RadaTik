using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Data.Infrastructure;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public class SystemAdminWalletConfiguration : IEntityTypeConfiguration<SystemAdminWallet>
{
    public void Configure(EntityTypeBuilder<SystemAdminWallet> entity)
    {
        entity.ToTable("SystemAdminWallets");
        entity.HasKey(w => w.Id);
        entity.Property(w => w.BalanceSyp).HasColumnType("decimal(18,2)");
        entity.Property(w => w.BalanceUsd).HasColumnType("decimal(18,2)");
        entity.ConfigureBalanceRowVersion();
        entity.HasData(new SystemAdminWallet
        {
            Id = 1,
            BalanceSyp = 0m,
            BalanceUsd = 0m,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
