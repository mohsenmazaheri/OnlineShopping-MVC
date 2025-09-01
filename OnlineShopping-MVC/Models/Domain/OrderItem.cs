namespace OnlineShopping_MVC.Models.Domain;

public class OrderItem : BaseEntity
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty; // snapshot
    public decimal UnitPrice { get; set; }                  // snapshot
    public int Quantity { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
}
