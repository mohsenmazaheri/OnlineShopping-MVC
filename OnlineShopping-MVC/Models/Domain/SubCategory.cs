using System.Collections.Generic;

namespace OnlineShopping_MVC.Models.Domain
{
    public class SubCategory : BaseEntity
    {
        public required string Name { get; set; }
        public string Slug { get; set; } = string.Empty;

        public Guid CategoryId { get; set; }
        public Category Category { get; set; } = default!;

        public ICollection<ProductGroup> Groups { get; set; } = new List<ProductGroup>();
    }
}
