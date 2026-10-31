using GlimpsesOfGlory.Core;
using GlimpsesOfGlory.Web.Endpoints;
using GlimpsesOfGlory.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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

// Add services to the container. Each area's registrations live together in its own
// Program.<Area>.cs file (partial class Program), the only Web code allowed to touch Core
// and Infrastructure types — see the project-structure skill.
var productPhotosPath = ResolveProductPhotosPath(builder);

AddWebPlatform(builder);
AddDatabase(builder);
AddCatalog(builder, productPhotosPath);
AddCart(builder);
AddCheckout(builder);
AddAdmin(builder);
AddEmail(builder);

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
