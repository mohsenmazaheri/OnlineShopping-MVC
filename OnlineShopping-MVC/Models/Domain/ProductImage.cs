namespace OnlineShopping_MVC.Models.Domain;

public class ProductImage : BaseEntity
{
    public required string Url { get; set; }
    public int SortOrder { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
}
