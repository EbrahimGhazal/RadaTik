using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PaymentAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.AccountAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
        entity.Property(e => e.PaymentCurrency).HasConversion<int>();
        entity.Property(e => e.AccountCurrency).HasConversion<int>();
        entity.Property(e => e.OperationType).HasMaxLength(40).HasDefaultValue("ReceivePayment");
        entity.Property(e => e.ReferenceNumber).HasMaxLength(40);
        entity.Property(e => e.PreviousClientBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NewClientBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PreviousPointBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.NewPointBalance).HasColumnType("decimal(18,2)");
        entity.Property(e => e.PaymentDate).HasDefaultValueSql("GETDATE()");

        entity.HasIndex(e => e.ClientId);
        entity.HasIndex(e => e.ReceivedByUserId);
        entity.HasIndex(e => e.PaymentDate);
        entity.HasIndex(e => e.NetworkId);
        entity.HasIndex(e => e.ReferenceNumber);

        entity.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.ReceivedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReceivedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.Network)
            .WithMany()
            .HasForeignKey(e => e.NetworkId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
