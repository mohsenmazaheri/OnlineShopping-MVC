using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OnlineShopping_MVC.Models.Domain;
using OnlineShopping_MVC.Models.Identity;

namespace OnlineShopping_MVC.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Category>().HasIndex(x => x.Slug);
        b.Entity<SubCategory>().HasIndex(x => new { x.CategoryId, x.Slug });
        b.Entity<ProductGroup>().HasIndex(x => new { x.SubCategoryId, x.Name });
        b.Entity<Product>().HasIndex(x => x.SKU).IsUnique();
        b.Entity<Product>().HasIndex(x => x.Name);

        b.Entity<SubCategory>()
            .HasOne(s => s.Category)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ProductGroup>()
            .HasOne(g => g.SubCategory)
            .WithMany(s => s.Groups)
            .HasForeignKey(g => g.SubCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Product>()
            .HasOne(p => p.ProductGroup)
            .WithMany(pg => pg.Products)
            .HasForeignKey(p => p.ProductGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Product>()
            .HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<CartItem>()
            .HasOne(i => i.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CartId);

        b.Entity<OrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId);

        b.Entity<Order>()
 .Property(o => o.Status)
 .HasConversion<string>()        // store enum as NVARCHAR
 .HasMaxLength(32)
 .HasDefaultValue(OrderStatus.Pending);

        // Optional: enforce valid values at the DB level (works with SQL Server)
        b.Entity<Order>()
         .ToTable(tb => tb.HasCheckConstraint(
             "CK_Orders_Status",
             $"[Status] IN ('Pending','Paid','Shipped','Completed','Canceled')"));

    }
}
