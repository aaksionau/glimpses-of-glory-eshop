using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Core.Services;
using GlimpsesOfGlory.Infrastructure.Options;
using GlimpsesOfGlory.Infrastructure.Services;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Email;
using GlimpsesOfGlory.Web.Endpoints;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Globalization;

if (args is ["hash-password", var passwordToHash])
{
    Console.WriteLine(PasswordHasher.Hash(passwordToHash));
    return;
}

// The store only sells in USD; pin the culture explicitly rather than relying on the
// container's locale environment (unset LANG/LC_ALL resolves to invariant culture,
// which renders currency as "¤" instead of "$").
var defaultCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var builder = WebApplication.CreateBuilder(args);

// GlitchTip speaks the Sentry protocol, so the Sentry SDK ships errors and log events there.
// Options bind from the "Sentry" config section; with no Sentry__Dsn set the SDK stays disabled.
builder.WebHost.UseSentry();

// Add services to the container.
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

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured. Set ConnectionStrings__Default.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<IAdminProductService, AdminProductService>();
builder.Services.AddScoped<IProductPhotoStorage, ProductPhotoStorage>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<ICartStore, SessionCartStore>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICartRestoreService, CartRestoreService>();
builder.Services.AddScoped<CheckoutSessionStore>();

builder.Services.AddScoped<IShippingSettingsService, ShippingSettingsService>();

builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IPaymentGateway>(sp =>
{
    var options = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
    return new StripePaymentGateway(options.SecretKey, options.WebhookSecret);
});
builder.Services.AddScoped<IInventoryStore, EfInventoryStore>();
builder.Services.AddScoped<OrderLineInventory>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAdminOrderService, AdminOrderService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAbandonedCheckoutService, AbandonedCheckoutService>();

builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddScoped<RazorViewToStringRenderer>();
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
    client.BaseAddress = new Uri("https://api.resend.com/"));

// Local-disk product photo storage. In production this path should be a
// Dokploy-mounted persistent volume (set via ProductPhotos__StoragePath),
// separate from wwwroot so uploaded photos survive redeploys.
var configuredPhotosPath = builder.Configuration["ProductPhotos:StoragePath"];
var productPhotosPath = Path.GetFullPath(
    string.IsNullOrEmpty(configuredPhotosPath) ? "App_Data/product-photos" : configuredPhotosPath,
    builder.Environment.ContentRootPath);
builder.Services.AddSingleton(new ProductPhotoStorageOptions { StoragePath = productPhotosPath });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(productPhotosPath),
    RequestPath = "/product-photos",
});

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapSeoEndpoints();
app.MapStripeWebhook();

app.Run();
