using Microsoft.EntityFrameworkCore;

namespace Pervaxis.Vpa.Vending.Data;

public sealed class VendingDbContext(DbContextOptions<VendingDbContext> options)
    : DbContext(options)
{
    public DbSet<VendingProduct> VendingProducts => Set<VendingProduct>();
    public DbSet<BillingImport> BillingImports => Set<BillingImport>();
    public DbSet<BillingImportTransaction> BillingImportTransactions => Set<BillingImportTransaction>();
    public DbSet<BillingCalculation> BillingCalculations => Set<BillingCalculation>();
    public DbSet<BillingCalculationImport> BillingCalculationImports => Set<BillingCalculationImport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<VendingProduct>();
        product.ToTable("vending_products", "public");
        product.HasKey(item => item.Id);

        product.Property(item => item.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        product.Property(item => item.SourceFile).HasColumnName("source_file");
        product.Property(item => item.SourceRowNumber).HasColumnName("source_row_number");
        product.Property(item => item.SystemProductId)
            .HasColumnName("system_product_id")
            .HasMaxLength(64)
            .IsRequired();
        product.Property(item => item.SystemProductName)
            .HasColumnName("system_product_name")
            .HasMaxLength(255)
            .IsRequired();
        product.Property(item => item.VendPrice)
            .HasColumnName("vend_price")
            .HasColumnType("numeric(10,2)");
        product.Property(item => item.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(255)
            .IsRequired();
        product.Property(item => item.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");
        product.Property(item => item.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        product.HasIndex(item => item.SystemProductId)
            .IsUnique()
            .HasDatabaseName("vending_products_system_product_id_key");
        product.HasIndex(item => new { item.SourceFile, item.SourceRowNumber })
            .IsUnique()
            .HasDatabaseName("uq_vending_products_source_row");

        var billingImport = modelBuilder.Entity<BillingImport>();
        billingImport.ToTable("billing_imports", "public");
        billingImport.HasKey(item => item.Id);
        billingImport.Property(item => item.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        billingImport.Property(item => item.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        billingImport.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(255).IsRequired();
        billingImport.Property(item => item.ContentSha256).HasColumnName("content_sha256").HasMaxLength(64).IsRequired();
        billingImport.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        billingImport.Property(item => item.TransactionCount).HasColumnName("transaction_count");
        billingImport.Property(item => item.RowsWithoutProductName).HasColumnName("rows_without_product_name");
        billingImport.Property(item => item.CalculatedAt).HasColumnName("calculated_at");
        billingImport.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        billingImport.HasIndex(item => item.ContentSha256)
            .IsUnique()
            .HasDatabaseName("uq_billing_imports_content_sha256");
        billingImport.HasMany(item => item.Transactions)
            .WithOne(item => item.BillingImport)
            .HasForeignKey(item => item.BillingImportId)
            .OnDelete(DeleteBehavior.Cascade);

        var billingTransaction = modelBuilder.Entity<BillingImportTransaction>();
        billingTransaction.ToTable("billing_import_transactions", "public");
        billingTransaction.HasKey(item => item.Id);
        billingTransaction.Property(item => item.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        billingTransaction.Property(item => item.BillingImportId).HasColumnName("billing_import_id");
        billingTransaction.Property(item => item.SourceRowNumber).HasColumnName("source_row_number");
        billingTransaction.Property(item => item.TransactionId).HasColumnName("transaction_id").HasMaxLength(128).IsRequired();
        billingTransaction.Property(item => item.SiteId).HasColumnName("site_id").HasMaxLength(128);
        billingTransaction.Property(item => item.Currency).HasColumnName("currency").HasMaxLength(8);
        billingTransaction.Property(item => item.MachineName).HasColumnName("machine_name").HasMaxLength(255);
        billingTransaction.Property(item => item.SettlementValue).HasColumnName("settlement_value").HasColumnType("numeric(12,2)");
        billingTransaction.Property(item => item.MachineSettlementTime).HasColumnName("machine_settlement_time").HasColumnType("timestamp without time zone");
        billingTransaction.Property(item => item.ProductSelectionInfo).HasColumnName("product_selection_info").HasMaxLength(1000);
        billingTransaction.HasIndex(item => new { item.BillingImportId, item.SourceRowNumber })
            .IsUnique()
            .HasDatabaseName("uq_billing_import_transactions_source_row");

        var billingCalculation = modelBuilder.Entity<BillingCalculation>();
        billingCalculation.ToTable("billing_calculations", "public");
        billingCalculation.HasKey(item => item.Id);
        billingCalculation.Property(item => item.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        billingCalculation.Property(item => item.EmployerName).HasColumnName("employer_name").HasMaxLength(255).IsRequired();
        billingCalculation.Property(item => item.CalculationName).HasColumnName("calculation_name").HasMaxLength(255).IsRequired();
        billingCalculation.Property(item => item.Year).HasColumnName("year");
        billingCalculation.Property(item => item.Month).HasColumnName("month");
        billingCalculation.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        billingCalculation.Property(item => item.ResultJson).HasColumnName("result_json").HasColumnType("jsonb").IsRequired();
        billingCalculation.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        billingCalculation.HasIndex(item => new { item.EmployerName, item.Year, item.Month, item.CreatedAt })
            .HasDatabaseName("ix_billing_calculations_employer_period_created");

        var calculationImport = modelBuilder.Entity<BillingCalculationImport>();
        calculationImport.ToTable("billing_calculation_imports", "public");
        calculationImport.HasKey(item => new { item.CalculationId, item.BillingImportId });
        calculationImport.Property(item => item.CalculationId).HasColumnName("calculation_id");
        calculationImport.Property(item => item.BillingImportId).HasColumnName("billing_import_id");
        calculationImport.HasOne(item => item.Calculation)
            .WithMany(item => item.Imports)
            .HasForeignKey(item => item.CalculationId)
            .OnDelete(DeleteBehavior.Cascade);
        calculationImport.HasOne(item => item.BillingImport)
            .WithMany()
            .HasForeignKey(item => item.BillingImportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
