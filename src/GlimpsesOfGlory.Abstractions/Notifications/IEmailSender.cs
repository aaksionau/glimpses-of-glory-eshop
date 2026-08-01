using GlimpsesOfGlory.Abstractions.Orders;

namespace GlimpsesOfGlory.Abstractions.Notifications;

public interface IEmailSender
{
    Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken);
}
