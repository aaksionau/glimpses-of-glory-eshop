using System.Net;
using System.Net.Mail;
using GlimpsesOfGlory.Abstractions.Notifications;
using GlimpsesOfGlory.Abstractions.Orders;
using GlimpsesOfGlory.Web.Configuration;
using Microsoft.Extensions.Options;

namespace GlimpsesOfGlory.Web.Helpers;

public sealed class OrderNotifier(
    RazorViewToStringRenderer viewRenderer,
    IOptions<SmtpOptions> smtpOptions) : IEmailSender
{
    public async Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken)
    {
        var html = await viewRenderer.RenderAsync("~/Emails/OrderConfirmation.cshtml", order);
        var options = smtpOptions.Value;

        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress),
            Subject = $"Order confirmation #{order.OrderId}",
            Body = html,
            IsBodyHtml = true,
        };
        message.To.Add(order.Address.Email);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            Credentials = new NetworkCredential(options.Username, options.Password),
            EnableSsl = true,
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken)
    {
        var html = await viewRenderer.RenderAsync("~/Emails/OrderShipped.cshtml", order);
        var options = smtpOptions.Value;

        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress),
            Subject = $"Your order #{order.OrderId} has shipped",
            Body = html,
            IsBodyHtml = true,
        };
        message.To.Add(order.Address.Email);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            Credentials = new NetworkCredential(options.Username, options.Password),
            EnableSsl = true,
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
