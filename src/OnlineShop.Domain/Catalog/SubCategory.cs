using OnlineShop.Domain.Common;

namespace OnlineShop.Domain.Catalog;

public class SubCategory : BaseEntity
{
    public required string Name { get; set; }
    public string Slug { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public ICollection<ProductGroup> Groups { get; set; } = new List<ProductGroup>();
}
