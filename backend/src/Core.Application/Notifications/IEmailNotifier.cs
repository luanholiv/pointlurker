using PointLurker.Core.Domain.Localization;

namespace PointLurker.Core.Application.Notifications;

public interface IEmailNotifier
{
    Task SendAvailabilityAsync(
        string toAddress,
        AvailabilityEmailModel model,
        string locale = SupportedLocales.EnUs,
        CancellationToken cancellationToken = default
    );

    Task SendFulfillmentCodeAsync(
        string toAddress,
        FulfillmentCodeEmailModel model,
        string locale = SupportedLocales.EnUs,
        CancellationToken cancellationToken = default
    );

    Task SendCredentialExpiryAsync(
        string toAddress,
        CredentialExpiryEmailModel model,
        string locale = SupportedLocales.EnUs,
        CancellationToken cancellationToken = default
    );
}
