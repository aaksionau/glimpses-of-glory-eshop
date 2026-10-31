using GlimpsesOfGlory.Core;
using Microsoft.EntityFrameworkCore;

// Composition root, part of Program (see Program.cs): the Postgres-backed AppDbContext.
public partial class Program
{
    private static void AddDatabase(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured. Set ConnectionStrings__Default.");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
    }
}
