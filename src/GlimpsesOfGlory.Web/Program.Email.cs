using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Configuration;
using GlimpsesOfGlory.Web.Email;

// Composition root, part of Program (see Program.cs): transactional email, rendered from the
// Razor templates in Emails/ and sent through Resend's HTTP API.
public partial class Program
{
    private static void AddEmail(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
        builder.Services.AddScoped<RazorViewToStringRenderer>();
        builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
            client.BaseAddress = new Uri("https://api.resend.com/"));
    }
}
