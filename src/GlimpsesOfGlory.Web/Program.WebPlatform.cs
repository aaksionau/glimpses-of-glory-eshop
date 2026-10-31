using Microsoft.AspNetCore.Authentication.Cookies;

// Composition root, part of Program (see Program.cs): ASP.NET Core platform services —
// Razor Pages, admin cookie auth, routing and the session that backs the cart.
public partial class Program
{
    private static void AddWebPlatform(WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizeFolder("/Admin");
            options.Conventions.AllowAnonymousToPage("/Admin/Login");
        });

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Admin/Login";
                options.LogoutPath = "/Admin/Logout";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
            });
        builder.Services.AddAuthorization();
        builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromDays(7);
            options.Cookie.IsEssential = true;
        });
    }
}
