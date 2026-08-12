using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;

namespace RadaTik.Data.Configurations;

public sealed class SubscriberInstallationInvoiceItemConfiguration : IEntityTypeConfiguration<SubscriberInstallationInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SubscriberInstallationInvoiceItem> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.ItemName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Quantity).HasColumnType("decimal(18,2)");
        entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");

        entity.HasIndex(e => e.SubscriberInstallationInvoiceId);

        entity.HasOne(e => e.SubscriberInstallationInvoice)
            .WithMany(i => i.Items)
            .HasForeignKey(e => e.SubscriberInstallationInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
