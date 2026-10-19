using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Configuration;
using PointLurker.Core.Application.Notifications;

namespace PointLurker.Infrastructure.ExternalServices.Email;

public static class SmtpServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpEmail(this IServiceCollection services)
    {
        services.AddValidatedOptions<SmtpSettings, SmtpSettingsValidator>(SmtpSettings.SectionName);
        services.AddValidatedOptions<AppUrlSettings, AppUrlSettingsValidator>(
            AppUrlSettings.SectionName
        );

        // Register localization for email resources only; no RequestLocalizationMiddleware.
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.AddTransient<ISmtpTransport, MailKitSmtpTransport>();
        services.AddTransient<AvailabilityEmailTemplate>();
        services.AddTransient<FulfillmentCodeEmailTemplate>();
        services.AddTransient<CredentialExpiryEmailTemplate>();
        services.AddTransient<IEmailNotifier, SmtpEmailService>();

        return services;
    }
}
