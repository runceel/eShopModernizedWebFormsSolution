using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace eShop.CatalogData;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Catalog> Catalogs => Set<Catalog>();
    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();
    public DbSet<CatalogInitialization> CatalogInitializations => Set<CatalogInitialization>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogBrand>(entity =>
        {
            entity.ToTable("catalog_brand");
            entity.HasKey(item => item.Id).HasName("pk_catalog_brand");
            entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(item => item.Brand).HasColumnName("brand").HasColumnType("varchar(100)").IsRequired();
        });

        modelBuilder.Entity<CatalogType>(entity =>
        {
            entity.ToTable("catalog_type");
            entity.HasKey(item => item.Id).HasName("pk_catalog_type");
            entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(item => item.Type).HasColumnName("type").HasColumnType("varchar(100)").IsRequired();
        });

        modelBuilder.Entity<Catalog>(entity =>
        {
            entity.ToTable("catalog");
            entity.HasKey(item => item.Id).HasName("pk_catalog");
            entity.Property(item => item.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(item => item.Name).HasColumnName("name").HasColumnType("varchar(50)").IsRequired();
            entity.Property(item => item.Description).HasColumnName("description").HasColumnType("text");
            entity.Property(item => item.Price).HasColumnName("price").HasColumnType("numeric(18,2)").IsRequired();
            entity.Property(item => item.PictureFileName).HasColumnName("picture_file_name").HasColumnType("text").IsRequired();
            entity.Property(item => item.CatalogTypeId).HasColumnName("catalog_type_id").IsRequired();
            entity.Property(item => item.CatalogBrandId).HasColumnName("catalog_brand_id").IsRequired();
            entity.Property(item => item.AvailableStock).HasColumnName("available_stock").IsRequired();
            entity.Property(item => item.RestockThreshold).HasColumnName("restock_threshold").IsRequired();
            entity.Property(item => item.MaxStockThreshold).HasColumnName("max_stock_threshold").IsRequired();
            entity.Property(item => item.OnReorder).HasColumnName("on_reorder").IsRequired();

            entity.HasOne(item => item.CatalogBrand)
                .WithMany(brand => brand.Catalogs)
                .HasForeignKey(item => item.CatalogBrandId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_catalog_catalog_brand_catalog_brand_id");

            entity.HasOne(item => item.CatalogType)
                .WithMany(type => type.Catalogs)
                .HasForeignKey(item => item.CatalogTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_catalog_catalog_type_catalog_type_id");
        });

        modelBuilder.Entity<CatalogInitialization>(entity =>
        {
            entity.ToTable("catalog_initialization");
            entity.HasKey(item => item.Id).HasName("pk_catalog_initialization");
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.InitializedAtUtc).HasColumnName("initialized_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        });
    }
}
