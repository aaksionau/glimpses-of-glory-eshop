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
using System.Xml.Linq;

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

app.MapGet("/robots.txt", (HttpRequest request) =>
{
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var content = $"""
        User-agent: *
        Allow: /
        Disallow: /admin
        Disallow: /cart
        Disallow: /checkout

        Sitemap: {baseUrl}/sitemap.xml
        """;
    return Results.Text(content, "text/plain");
});

app.MapGet("/sitemap.xml", async (
    HttpRequest request,
    IProductCatalogService productCatalogService,
    CancellationToken cancellationToken) =>
{
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var products = await productCatalogService.GetProductsAsync(cancellationToken);

    var urls = new List<string> { $"{baseUrl}/", $"{baseUrl}/products" };
    urls.AddRange(products.Select(p => $"{baseUrl}/products/{p.Slug}"));

    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var document = new XDocument(
        new XDeclaration("1.0", "utf-8", null),
        new XElement(ns + "urlset", urls.Select(url => new XElement(ns + "url", new XElement(ns + "loc", url)))));

    return Results.Text(document.ToString(), "application/xml");
});

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
