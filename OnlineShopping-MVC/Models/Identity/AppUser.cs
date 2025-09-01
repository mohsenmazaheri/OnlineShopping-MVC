using Microsoft.AspNetCore.Identity;

namespace OnlineShopping_MVC.Models.Identity;

public class AppUser : Microsoft.AspNetCore.Identity.IdentityUser
{
    public string? DisplayName { get; set; }
}
