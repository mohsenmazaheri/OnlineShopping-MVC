using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineShopping_MVC.Data;
using OnlineShopping_MVC.Models.Identity;

var builder = WebApplication.CreateBuilder(args);

// EF Core (SQL Server)
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity + Roles
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = false;
    o.Password.RequiredLength = 6;
    o.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();       // Identity UI
builder.Services.AddServerSideBlazor(); // optional: embed Blazor components in MVC views

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// MVC default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Identity UI and (optional) Blazor
app.MapRazorPages();
app.MapBlazorHub();

// Seed roles + default admin
using (var scope = app.Services.CreateScope())
{
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

await app.RunAsync();
