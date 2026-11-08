using Microsoft.AspNetCore.Identity;

namespace OnlineShop.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
