using GlimpsesOfGlory.Abstractions.Cart;
using GlimpsesOfGlory.Abstractions.Inventory;
using GlimpsesOfGlory.Abstractions.Notifications;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Abstractions.Payments;
using GlimpsesOfGlory.Abstractions.Products;
using GlimpsesOfGlory.Abstractions.Shipping;
using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Core.Cart.Services;
using GlimpsesOfGlory.Core.Inventory.Services;
using GlimpsesOfGlory.Core.Orders.Services;
using GlimpsesOfGlory.Core.Payments.Services;
using GlimpsesOfGlory.Core.Products;
using GlimpsesOfGlory.Core.Products.Seeding;
using GlimpsesOfGlory.Core.Products.Services;
using GlimpsesOfGlory.Core.Shipping.Services;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Helpers;
using GlimpsesOfGlory.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

if (args is ["hash-password", var passwordToHash])
{
    Console.WriteLine(PasswordHasher.Hash(passwordToHash));
    return;
}

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<ProductPhotoStorage>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<ICartStore, SessionCartStore>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<CheckoutSessionStore>();

builder.Services.AddScoped<IShippingSettingsService, ShippingSettingsService>();

builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IPaymentGateway>(sp =>
{
    var options = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
    return new StripePaymentGateway(options.SecretKey, options.WebhookSecret);
});
builder.Services.AddScoped<IInventoryStore, EfInventoryStore>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAdminOrderService, AdminOrderService>();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<RazorViewToStringRenderer>();
builder.Services.AddScoped<IEmailSender, OrderNotifier>();

// Local-disk product photo storage. In production this path should be a
// Dokploy-mounted persistent volume (set via ProductPhotos__StoragePath),
// separate from wwwroot so uploaded photos survive redeploys.
var productPhotosPath = Path.GetFullPath(
    builder.Configuration["ProductPhotos:StoragePath"] ?? "App_Data/product-photos",
    builder.Environment.ContentRootPath);
builder.Services.AddSingleton(new ProductPhotoStorageOptions { StoragePath = productPhotosPath });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrate = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    var seedPhotos = ProductPhotoSeeder.EnsureSeededAsync(productPhotosPath);
    await Task.WhenAll(migrate, seedPhotos);
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

app.MapPost("/webhooks/stripe", async (
    HttpRequest request,
    IPaymentGateway paymentGateway,
    IOrderService orderService,
    CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync(cancellationToken);
    var signature = request.Headers["Stripe-Signature"].ToString();

    PaymentWebhookResult result;
    try
    {
        result = paymentGateway.HandleWebhookEvent(payload, signature);
    }
    catch (PaymentSignatureVerificationException)
    {
        return Results.BadRequest();
    }

    if (result is { Outcome: PaymentEventOutcome.Succeeded, PaymentIntentId: not null })
    {
        await orderService.ConfirmPaymentAsync(result.PaymentIntentId, cancellationToken);
    }

    return Results.Ok();
});

app.Run();
