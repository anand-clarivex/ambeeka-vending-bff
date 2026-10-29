using Microsoft.EntityFrameworkCore;

namespace Pervaxis.Vpa.Vending.Data;

public sealed class VendingDbContext(DbContextOptions<VendingDbContext> options)
    : DbContext(options)
{
    public DbSet<VendingProduct> VendingProducts => Set<VendingProduct>();

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
    }
}
