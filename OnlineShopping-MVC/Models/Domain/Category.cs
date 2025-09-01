namespace OnlineShopping_MVC.Models.Domain;

public class Category : BaseEntity
{
    public required string Name { get; set; }
    public string Slug { get; set; } = string.Empty;
    public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
}
