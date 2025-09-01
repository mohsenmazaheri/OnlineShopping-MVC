using Microsoft.AspNetCore.Identity;

namespace OnlineShopping_MVC.Models.Identity;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
