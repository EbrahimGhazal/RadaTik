using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RadaTik.Models.Business;

namespace RadaTik.Data.Configurations;

public sealed class ErpCustomerConfiguration : IEntityTypeConfiguration<ErpCustomer>
{
    public void Configure(EntityTypeBuilder<ErpCustomer> entity)
    {
        entity.ToTable("ErpCustomers");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
        entity.Property(e => e.Phone).HasMaxLength(30);
        entity.Property(e => e.Email).HasMaxLength(120);
        entity.Property(e => e.Address).HasMaxLength(250);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Name });

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ErpSupplierConfiguration : IEntityTypeConfiguration<ErpSupplier>
{
    public void Configure(EntityTypeBuilder<ErpSupplier> entity)
    {
        entity.ToTable("ErpSuppliers");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
        entity.Property(e => e.Phone).HasMaxLength(30);
        entity.Property(e => e.Email).HasMaxLength(120);
        entity.Property(e => e.Address).HasMaxLength(250);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Name });

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CompanyEmployeeTaskConfiguration : IEntityTypeConfiguration<CompanyEmployeeTask>
{
    public void Configure(EntityTypeBuilder<CompanyEmployeeTask> entity)
    {
        entity.ToTable("CompanyEmployeeTasks");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(2000);
        entity.Property(e => e.AssignedToUserId).HasMaxLength(450).IsRequired();
        entity.Property(e => e.AssignedByUserId).HasMaxLength(450);
        entity.Property(e => e.CompletionNotes).HasMaxLength(1000);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Status });
        entity.HasIndex(e => e.AssignedToUserId);

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.AssignedToUser)
            .WithMany()
            .HasForeignKey(e => e.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.AssignedByUser)
            .WithMany()
            .HasForeignKey(e => e.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class EmployeeRewardPenaltyConfiguration : IEntityTypeConfiguration<EmployeeRewardPenalty>
{
    public void Configure(EntityTypeBuilder<EmployeeRewardPenalty> entity)
    {
        entity.ToTable("EmployeeRewardPenalties");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
        entity.Property(e => e.ReviewedByUserId).HasMaxLength(450);
        entity.Property(e => e.ReviewNotes).HasMaxLength(500);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Status });

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PayrollEmployee)
            .WithMany()
            .HasForeignKey(e => e.PayrollEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PayrollTransaction)
            .WithMany()
            .HasForeignKey(e => e.PayrollTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
{
    public void Configure(EntityTypeBuilder<ChartOfAccount> entity)
    {
        entity.ToTable("ChartOfAccounts");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
        entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
        entity.HasIndex(e => new { e.CompanyNetworkId, e.Code }).IsUnique();

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ParentAccount)
            .WithMany(e => e.ChildAccounts)
            .HasForeignKey(e => e.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> entity)
    {
        entity.ToTable("JournalEntries");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
        entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
        entity.Property(e => e.PostedByUserId).HasMaxLength(450);
        entity.HasIndex(e => e.CompanyNetworkId);
        entity.HasIndex(e => e.EntryDate);

        entity.HasOne(e => e.CompanyNetwork)
            .WithMany()
            .HasForeignKey(e => e.CompanyNetworkId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.PostedByUser)
            .WithMany()
            .HasForeignKey(e => e.PostedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> entity)
    {
        entity.ToTable("JournalEntryLines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Debit).HasColumnType("decimal(18,2)");
        entity.Property(e => e.Credit).HasColumnType("decimal(18,2)");
        entity.Property(e => e.LineDescription).HasMaxLength(250);
        entity.HasIndex(e => e.JournalEntryId);

        entity.HasOne(e => e.JournalEntry)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.ChartOfAccount)
            .WithMany(e => e.JournalLines)
            .HasForeignKey(e => e.ChartOfAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
