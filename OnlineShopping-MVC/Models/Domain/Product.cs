namespace OnlineShopping_MVC.Models.Domain;

public class Product : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int Stock { get; set; }
    public bool IsPublished { get; set; }

    public Guid ProductGroupId { get; set; }
    public ProductGroup ProductGroup { get; set; } = default!;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
