namespace OnlineShopping.Application.Authorization;

/// <summary>
/// Role names. A one-person shop gives the owner <see cref="Admin"/>, which covers everything;
/// a shop with staff splits the work between <see cref="Manager"/>, <see cref="Warehouse"/> and <see cref="Accountant"/>.
/// </summary>
public static class AppRoles
{
    /// <summary>Shop owner / system administrator: every permission, including system settings.</summary>
    public const string Admin = "Admin";

    /// <summary>Runs the shop: catalog, orders, discounts, reviews, customers.</summary>
    public const string Manager = "Manager";

    /// <summary>Inventory: stock movements, receipts, stocktaking.</summary>
    public const string Warehouse = "Warehouse";

    /// <summary>Accounting: journal entries, payments, financial reports.</summary>
    public const string Accountant = "Accountant";

    public const string Customer = "Customer";

    /// <summary>Every role, in the order they are seeded.</summary>
    public static IReadOnlyList<string> All { get; } = [Admin, Manager, Warehouse, Accountant, Customer];

    /// <summary>Roles that work in the back office (everyone except customers).</summary>
    public static IReadOnlyList<string> Staff { get; } = [Admin, Manager, Warehouse, Accountant];
}
