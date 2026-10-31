using System.Diagnostics;
using GlimpsesOfGlory.Web.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace GlimpsesOfGlory.TestSupport;

internal static class TestRazorRenderer
{
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }

    // Builds just enough of the ASP.NET Core Razor view engine (no Kestrel, no HTTP
    // request, no database) to render a .cshtml view to a string, mirroring how
    // Program.cs wires RazorViewToStringRenderer via AddRazorPages().
    public static RazorViewToStringRenderer Create()
    {
        var webAssembly = typeof(RazorViewToStringRenderer).Assembly;

        var services = new ServiceCollection();

        var environment = new TestWebHostEnvironment
        {
            ApplicationName = webAssembly.GetName().Name!,
            ContentRootPath = AppContext.BaseDirectory,
        };
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(environment);

        var partManager = new ApplicationPartManager();
        partManager.ApplicationParts.Add(new CompiledRazorAssemblyPart(webAssembly));
        services.AddSingleton(partManager);

        services.AddSingleton(new DiagnosticListener("GlimpsesOfGlory.TestSupport"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddLogging();
        services.AddRazorPages();

        var provider = services.BuildServiceProvider();

        return new RazorViewToStringRenderer(
            provider.GetRequiredService<IRazorViewEngine>(),
            provider.GetRequiredService<ITempDataProvider>(),
            provider);
    }
}
