namespace OnlineShop.Application.Authorization;

/// <summary>
/// Everything a back-office user can be allowed to do. Endpoints require permissions, never role names,
/// so a one-person shop (Admin only) and a shop with divided roles use the same checks.
/// </summary>
public static class Permissions
{
    public const string CatalogManage = "catalog.manage";
    public const string OrdersView = "orders.view";
    public const string OrdersManage = "orders.manage";
    public const string CustomersView = "customers.view";
    public const string ReviewsModerate = "reviews.moderate";
    public const string DiscountsManage = "discounts.manage";
    public const string ContentManage = "content.manage";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string AccountingView = "accounting.view";
    public const string AccountingManage = "accounting.manage";
    public const string ReportsView = "reports.view";
    public const string StaffManage = "staff.manage";
    public const string SettingsManage = "settings.manage";

    /// <summary>Claim type under which a signed-in user's permissions are exposed.</summary>
    public const string ClaimType = "permission";

    /// <summary>Authorization policy names are derived from permissions: "permission:catalog.manage".</summary>
    public const string PolicyPrefix = "permission:";

    public static IReadOnlyList<string> All { get; } =
    [
        CatalogManage, OrdersView, OrdersManage, CustomersView, ReviewsModerate, DiscountsManage, ContentManage,
        InventoryView, InventoryManage, AccountingView, AccountingManage, ReportsView, StaffManage, SettingsManage
    ];

    public static string Policy(string permission) => PolicyPrefix + permission;
}

/// <summary>Fixed role → permission mapping (decided: not editable from the admin panel).</summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, IReadOnlySet<string>> Map = new(StringComparer.Ordinal)
    {
        [AppRoles.Admin] = Permissions.All.ToHashSet(StringComparer.Ordinal),
        [AppRoles.Manager] = Set(
            Permissions.CatalogManage, Permissions.OrdersView, Permissions.OrdersManage, Permissions.CustomersView,
            Permissions.ReviewsModerate, Permissions.DiscountsManage, Permissions.ContentManage,
            Permissions.InventoryView, Permissions.ReportsView),
        [AppRoles.Warehouse] = Set(Permissions.InventoryView, Permissions.InventoryManage, Permissions.OrdersView),
        [AppRoles.Accountant] = Set(Permissions.AccountingView, Permissions.AccountingManage, Permissions.ReportsView,
            Permissions.OrdersView, Permissions.InventoryView),
        [AppRoles.Customer] = Set()
    };

    /// <summary>Union of the permissions of all the given roles; unknown roles grant nothing.</summary>
    public static IReadOnlySet<string> For(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (Map.TryGetValue(role, out var permissions))
                result.UnionWith(permissions);
        }
        return result;
    }

    private static HashSet<string> Set(params string[] permissions) => permissions.ToHashSet(StringComparer.Ordinal);
}
