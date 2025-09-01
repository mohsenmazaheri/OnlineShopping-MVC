namespace OnlineShopping_MVC.Models.Domain;

public class Order : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderedUtc { get; set; } = DateTime.UtcNow;

    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
