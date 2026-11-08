using OnlineShopping_MVC.Models.Identity;

namespace OnlineShopping_MVC.Tests.Identity;

public class AppRolesTests
{
    [Fact]
    public void All_contains_every_role_exactly_once()
    {
        Assert.Equal(
            [AppRoles.Admin, AppRoles.Manager, AppRoles.Warehouse, AppRoles.Accountant, AppRoles.Customer],
            AppRoles.All);
        Assert.Equal(AppRoles.All.Count, AppRoles.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Staff_is_every_role_except_Customer()
    {
        Assert.DoesNotContain(AppRoles.Customer, AppRoles.Staff);
        Assert.Equal(AppRoles.All.Where(r => r != AppRoles.Customer), AppRoles.Staff);
    }

    [Fact]
    public void A_one_person_shop_needs_only_the_Admin_role_to_reach_the_back_office()
    {
        // The owner of a one-person shop is given Admin only; it must count as staff on its own.
        Assert.Contains(AppRoles.Admin, AppRoles.Staff);
    }
}
