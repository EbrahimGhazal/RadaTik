using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadTik.Models;

namespace RadTik.Data.Configurations;

public sealed class MaintenanceInvoiceConfiguration : IEntityTypeConfiguration<MaintenanceInvoice>
{
    public void Configure(EntityTypeBuilder<MaintenanceInvoice> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.ServiceBasePrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.TransportFee).HasColumnType("decimal(18,2)");
        entity.Property(e => e.GrossAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.CommissionValue).HasColumnType("decimal(18,2)");
        entity.Property(e => e.CommissionAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NetAmountToCompany).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PreviousClientBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NewClientBalance).HasColumnType("decimal(18,2)");

        entity.Property(e => e.FaultExplanation).HasMaxLength(1000).IsRequired();
        entity.Property(e => e.FixExplanation).HasMaxLength(1000).IsRequired();
        entity.Property(e => e.IssuedByUserId).HasMaxLength(450).IsRequired();
        entity.Property(e => e.PaidByUserId).HasMaxLength(450);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.MaintenanceRequestId).IsUnique();
        entity.HasIndex(e => e.ClientId);
        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.CreatedAt);

        entity.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.MaintenanceRequest)
            .WithMany()
            .HasForeignKey(e => e.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
