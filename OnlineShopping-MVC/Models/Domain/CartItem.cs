namespace OnlineShopping_MVC.Models.Domain;

public class CartItem : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public int Quantity { get; set; }

    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = default!;
}
