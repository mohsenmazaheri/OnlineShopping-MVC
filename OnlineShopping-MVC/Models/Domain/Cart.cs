namespace OnlineShopping_MVC.Models.Domain;

public class Cart : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
