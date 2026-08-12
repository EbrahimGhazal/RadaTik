using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class EmployeeWalletTopUpRequestConfiguration : IEntityTypeConfiguration<EmployeeWalletTopUpRequest>
{
    public void Configure(EntityTypeBuilder<EmployeeWalletTopUpRequest> entity)
    {
        entity.ToTable("EmployeeWalletTopUpRequests");
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Status, e.RequestedAt });
        entity.HasIndex(e => new { e.PayrollEmployeeId, e.Status });

        entity.HasOne(e => e.PayrollEmployee)
            .WithMany(p => p.WalletTopUpRequests)
            .HasForeignKey(e => e.PayrollEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
