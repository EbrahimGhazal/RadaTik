using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class MoneyDiaryEntryConfiguration : IEntityTypeConfiguration<MoneyDiaryEntry>
{
    public void Configure(EntityTypeBuilder<MoneyDiaryEntry> entity)
    {
        entity.ToTable("MoneyDiaryEntries");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.EntryType).HasConversion<int>();
        entity.Property(e => e.CategoryKey).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Currency).HasConversion<int>().HasDefaultValue(PricingCurrency.SYP_New);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => e.EntryDate);
        entity.HasIndex(e => e.MaterialPurchaseInvoiceId);
        entity.HasIndex(e => e.MaterialSalesInvoiceId);

        entity.HasOne(e => e.CompanyNetwork)
          .WithMany()
          .HasForeignKey(e => e.CompanyNetworkId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
          .WithMany()
          .HasForeignKey(e => e.CreatedByUserId)
          .OnDelete(DeleteBehavior.SetNull);
    }
}
