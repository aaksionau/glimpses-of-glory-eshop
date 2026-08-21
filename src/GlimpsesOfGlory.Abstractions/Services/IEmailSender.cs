using GlimpsesOfGlory.Abstractions.Dtos;

namespace GlimpsesOfGlory.Abstractions.Services;

public interface IEmailSender
{
    Task SendOrderConfirmationAsync(OrderConfirmationView order, CancellationToken cancellationToken);

    Task SendShippedNotificationAsync(OrderShippedView order, CancellationToken cancellationToken);
}
