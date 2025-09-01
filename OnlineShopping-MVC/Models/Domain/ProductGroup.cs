namespace OnlineShopping_MVC.Models.Domain;

public class ProductGroup : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    public Guid SubCategoryId { get; set; }
    public SubCategory SubCategory { get; set; } = default!;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
