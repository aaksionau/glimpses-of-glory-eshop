using System.Net.Http.Headers;
using System.Net.Http.Json;
using GlimpsesOfGlory.Abstractions.Dtos;
using GlimpsesOfGlory.Abstractions.Services;
using GlimpsesOfGlory.Web.Configuration;
using Microsoft.Extensions.Options;

namespace GlimpsesOfGlory.Web.Helpers;

// Sends order emails through Resend's HTTP API (https://resend.com/docs/api-reference/emails/send-email).
public sealed class OrderNotifier(
    HttpClient httpClient,
    RazorViewToStringRenderer viewRenderer,
    IOptions<ResendOptions> resendOptions) : IEmailSender
{
    public async Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken)
    {
        var html = await viewRenderer.RenderAsync("~/Emails/OrderConfirmation.cshtml", order);
        await SendAsync(order.Address.Email, $"Order confirmation #{order.OrderId}", html, cancellationToken);
    }

    public async Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken)
    {
        var html = await viewRenderer.RenderAsync("~/Emails/OrderShipped.cshtml", order);
        await SendAsync(order.Address.Email, $"Your order #{order.OrderId} has shipped", html, cancellationToken);
    }

    public async Task SendReadyForPickupNotificationAsync(OrderReadyForPickupView order, CancellationToken cancellationToken)
    {
        var html = await viewRenderer.RenderAsync("~/Emails/OrderReadyForPickup.cshtml", order);
        await SendAsync(order.Address.Email, $"Your order #{order.OrderId} is ready for pickup", html, cancellationToken);
    }

    public async Task SendAbandonedCheckoutReminderAsync(AbandonedCheckoutReminderView reminder, CancellationToken cancellationToken)
    {
        var html = await viewRenderer.RenderAsync("~/Emails/AbandonedCheckoutReminder.cshtml", reminder);
        await SendAsync(reminder.Email, "You left something in your cart – Glimpses of Glory", html, cancellationToken);
    }

    private async Task SendAsync(string to, string subject, string html, CancellationToken cancellationToken)
    {
        var options = resendOptions.Value;

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new { from = options.FromAddress, to = new[] { to }, subject, html }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Resend rejected email ({(int)response.StatusCode}): {body}", null, response.StatusCode);
        }
    }
}
