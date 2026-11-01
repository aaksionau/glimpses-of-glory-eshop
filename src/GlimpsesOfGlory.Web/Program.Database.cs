using GlimpsesOfGlory.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

// Composition root, part of Program (see Program.cs): the Postgres-backed AppDbContext, which
// also stores the Data Protection key ring.
public partial class Program
{
    private static void AddDatabase(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured. Set ConnectionStrings__Default.");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        builder.Services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>();
    }
}
