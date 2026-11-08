using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OnlineShopping_MVC.Models.Domain;
using OnlineShopping_MVC.Models.Identity;

namespace OnlineShopping_MVC.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    /// <summary>
    /// Filter for unique indexes so a soft-deleted row does not block reusing its value.
    /// Column names are snake_case (<c>UseSnakeCaseNamingConvention</c>); the SQL is valid in PostgreSQL and SQLite.
    /// </summary>
    internal const string NotDeletedFilter = "is_deleted = false";

    internal const string OrderStatusCheckConstraintName = "CK_Orders_Status";

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money columns: a fixed precision keeps every database provider consistent (no unbounded numeric).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        ConfigureIdentityTables(b);
        ConfigureCatalog(b);
        ConfigureCart(b);
        ConfigureOrders(b);
        ApplySoftDeleteQueryFilters(b);
    }

    /// <summary>The SQL check that keeps the Status column in sync with every <see cref="OrderStatus"/> value.</summary>
    internal static string BuildOrderStatusCheckSql() =>
        $"status IN ({string.Join(", ", Enum.GetNames<OrderStatus>().Select(n => $"'{n}'"))})";

    /// <summary>
    /// Identity sets explicit PascalCase table names ("AspNetUsers") that the snake_case convention does not rename;
    /// in PostgreSQL those would need quoting in every hand-written query.
    /// </summary>
    private static void ConfigureIdentityTables(ModelBuilder b)
    {
        b.Entity<AppUser>().ToTable("users");
        b.Entity<IdentityRole>().ToTable("roles");
        b.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        b.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        b.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        b.Entity<IdentityUserToken<string>>().ToTable("user_tokens");
        b.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
    }

    private static void ConfigureCatalog(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(200);
            e.HasIndex(x => x.Slug).IsUnique().HasFilter(NotDeletedFilter);
        });

        b.Entity<SubCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(200);
            e.HasIndex(x => new { x.CategoryId, x.Slug }).IsUnique().HasFilter(NotDeletedFilter);
            e.HasOne(s => s.Category)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductGroup>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => new { x.SubCategoryId, x.Name });
            e.HasOne(g => g.SubCategory)
                .WithMany(s => s.Groups)
                .HasForeignKey(g => g.SubCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.SKU).HasMaxLength(64);
            e.HasIndex(x => x.SKU).IsUnique().HasFilter(NotDeletedFilter);
            e.HasIndex(x => x.Name);
            e.HasOne(p => p.ProductGroup)
                .WithMany(pg => pg.Products)
                .HasForeignKey(p => p.ProductGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductImage>(e =>
        {
            e.Property(x => x.Url).HasMaxLength(2048);
            e.HasOne(i => i.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCart(ModelBuilder b)
    {
        b.Entity<Cart>(e =>
        {
            e.HasIndex(x => x.CustomerId).IsUnique().HasFilter(NotDeletedFilter);
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CartItem>(e =>
        {
            e.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique().HasFilter(NotDeletedFilter);
            e.HasOne(i => i.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrders(ModelBuilder b)
    {
        b.Entity<Order>(e =>
        {
            // Orders are financial records: deleting a user must never delete their orders.
            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CustomerId, x.OrderedUtc });

            e.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            e.ToTable(tb => tb.HasCheckConstraint(OrderStatusCheckConstraintName, BuildOrderStatusCheckSql()));
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(300);
            e.HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            // ProductName/UnitPrice are snapshots; the FK only guarantees the product existed.
            e.HasOne<Product>()
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder b)
    {
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(BaseEntity.IsDeleted)));
            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
