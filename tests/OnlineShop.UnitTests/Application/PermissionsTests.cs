using OnlineShop.Application.Authorization;

namespace OnlineShop.UnitTests.Application;

public sealed class PermissionsTests
{
    [Fact]
    public void Admin_alone_has_every_permission_so_a_one_person_shop_needs_no_other_role()
    {
        Assert.Equal(Permissions.All.ToHashSet(), RolePermissions.For([AppRoles.Admin]));
    }

    [Fact]
    public void Customers_have_no_back_office_permissions()
    {
        Assert.Empty(RolePermissions.For([AppRoles.Customer]));
    }

    [Fact]
    public void Staff_roles_get_only_their_area()
    {
        var warehouse = RolePermissions.For([AppRoles.Warehouse]);
        Assert.Contains(Permissions.InventoryManage, warehouse);
        Assert.DoesNotContain(Permissions.AccountingManage, warehouse);
        Assert.DoesNotContain(Permissions.StaffManage, warehouse);

        var accountant = RolePermissions.For([AppRoles.Accountant]);
        Assert.Contains(Permissions.AccountingManage, accountant);
        Assert.DoesNotContain(Permissions.InventoryManage, accountant);

        var manager = RolePermissions.For([AppRoles.Manager]);
        Assert.Contains(Permissions.CatalogManage, manager);
        Assert.DoesNotContain(Permissions.StaffManage, manager);
        Assert.DoesNotContain(Permissions.SettingsManage, manager);
    }

    [Fact]
    public void Several_roles_combine_their_permissions()
    {
        var combined = RolePermissions.For([AppRoles.Warehouse, AppRoles.Accountant]);

        Assert.Contains(Permissions.InventoryManage, combined);
        Assert.Contains(Permissions.AccountingManage, combined);
    }

    [Fact]
    public void Unknown_roles_grant_nothing()
    {
        Assert.Empty(RolePermissions.For(["Hacker", "admin"]));
    }

    [Fact]
    public void Only_admin_can_manage_staff_and_settings()
    {
        foreach (var role in AppRoles.All.Where(r => r != AppRoles.Admin))
        {
            var permissions = RolePermissions.For([role]);
            Assert.DoesNotContain(Permissions.StaffManage, permissions);
            Assert.DoesNotContain(Permissions.SettingsManage, permissions);
        }
    }

    [Fact]
    public void Policy_names_are_prefixed_permissions()
    {
        Assert.Equal("permission:catalog.manage", Permissions.Policy(Permissions.CatalogManage));
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
    }
}
