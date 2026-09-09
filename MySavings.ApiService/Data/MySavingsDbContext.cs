using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Models;

namespace MySavings.ApiService.Data;

public class MySavingsDbContext(DbContextOptions<MySavingsDbContext> options) : DbContext(options)
{
    public DbSet<MonthlyEntry> MonthlyEntries { get; set; }
    public DbSet<SavingsAccount> SavingsAccounts { get; set; }
    public DbSet<SavingsAllocation> SavingsAllocations { get; set; }
    public DbSet<AllocationRule> AllocationRules { get; set; }
    public DbSet<Owner> Owners { get; set; }
    public DbSet<TransferGroup> TransferGroups { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MonthlyEntry configuration
        modelBuilder.Entity<MonthlyEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Person1Salary).HasPrecision(18, 2);
            entity.Property(e => e.Person1Expenses).HasPrecision(18, 2);
            entity.Property(e => e.Person1Savings).HasPrecision(18, 2);
            entity.Property(e => e.Person1SavingsRatio).HasPrecision(5, 4);
            entity.Property(e => e.Person2Salary).HasPrecision(18, 2);
            entity.Property(e => e.Person2Expenses).HasPrecision(18, 2);
            entity.Property(e => e.Person2Savings).HasPrecision(18, 2);
            entity.Property(e => e.Person2SavingsRatio).HasPrecision(5, 4);
            entity.Property(e => e.TotalSalary).HasPrecision(18, 2);
            entity.Property(e => e.TotalExpenses).HasPrecision(18, 2);
            entity.Property(e => e.TotalSavings).HasPrecision(18, 2);
            entity.Property(e => e.TotalSavingsRatio).HasPrecision(5, 4);
            entity.Property(e => e.PreviousMonthPerson1Percent).HasPrecision(5, 4);
            entity.Property(e => e.PreviousMonthPerson2Percent).HasPrecision(5, 4);

            entity.HasIndex(e => e.Month).IsUnique();
        });

        // Owner configuration (formerly PersonSettings)
        modelBuilder.Entity<Owner>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("Owner");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AllocationSharePercent).HasPrecision(5, 4).HasDefaultValue(0.5m);
        });

        // SavingsAccount configuration
        modelBuilder.Entity<SavingsAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);

            entity.HasIndex(e => e.Name).IsUnique();

            entity.Property(e => e.LiquidityLevel)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(LiquidityLevel.Liquide);

            entity.Property(e => e.TransferThreshold).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.TransferAccumulator).HasPrecision(18, 2).HasDefaultValue(0m);

            entity.HasOne<Owner>()
                .WithMany(o => o.SavingsAccounts)
                .HasForeignKey(a => a.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            // One-to-one relationship with AllocationRule
            entity.HasOne(e => e.AllocationRule)
                .WithOne(r => r.SavingsAccount)
                .HasForeignKey<AllocationRule>(r => r.SavingsAccountId);
        });

        // SavingsAllocation configuration
        modelBuilder.Entity<SavingsAllocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Percentage).HasPrecision(5, 4);
            entity.Property(e => e.Weight).HasPrecision(5, 4);
            entity.Property(e => e.TransferableAmount).HasPrecision(18, 2);
            entity.Property(e => e.AccumulatorBefore).HasPrecision(18, 2);

            entity.HasOne(e => e.MonthlyEntry)
                .WithMany(m => m.SavingsAllocations)
                .HasForeignKey(e => e.MonthlyEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SavingsAccount)
                .WithMany()
                .HasForeignKey(e => e.SavingsAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.MonthlyEntryId, e.SavingsAccountId }).IsUnique();
        });

        // AllocationRule configuration
        modelBuilder.Entity<AllocationRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Weight).HasPrecision(5, 4);

            entity.HasIndex(e => e.SavingsAccountId).IsUnique();
        });

        // AppSettings configuration
        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SecurityBuffer).HasPrecision(18, 2).HasDefaultValue(500m);
        });

        // TransferGroup configuration
        modelBuilder.Entity<TransferGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);

            entity.HasMany(g => g.SavingsAccounts)
                .WithMany(a => a.TransferGroups)
                .UsingEntity(j => j.ToTable("TransferGroupSavingsAccount"));
        });
    }
}
