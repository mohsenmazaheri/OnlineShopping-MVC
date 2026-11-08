using OnlineShop.Domain.Common;

namespace OnlineShop.Domain.Sales;

public class Cart : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
