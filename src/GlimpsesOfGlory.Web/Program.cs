using GlimpsesOfGlory.Application.Products;
using GlimpsesOfGlory.Infrastructure;
using GlimpsesOfGlory.Infrastructure.Products;
using GlimpsesOfGlory.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

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

builder.Services.AddScoped<IProductStore, ProductStore>();
builder.Services.AddScoped<GetProducts>();
builder.Services.AddScoped<GetProductBySlug>();

var app = builder.Build();

// Local-disk product photo storage. In production this path should be a
// Dokploy-mounted persistent volume (set via ProductPhotos__StoragePath),
// separate from wwwroot so uploaded photos survive redeploys.
var productPhotosPath = Path.GetFullPath(
    builder.Configuration["ProductPhotos:StoragePath"] ?? "App_Data/product-photos",
    app.Environment.ContentRootPath);

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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
