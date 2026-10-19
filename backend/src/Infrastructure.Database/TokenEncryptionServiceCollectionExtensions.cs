using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Configuration;
using PointLurker.Core.Application.Security;
using PointLurker.Infrastructure.Database.Security;

namespace PointLurker.Infrastructure.Database;

public static class TokenEncryptionServiceCollectionExtensions
{
    public static IServiceCollection AddTokenEncryption(this IServiceCollection services)
    {
        services.AddValidatedOptions<DataProtectionSettings, DataProtectionSettingsValidator>(
            DataProtectionSettings.SectionName
        );

        // The application name is an input to every key derivation: changing it leaves every stored
        // secret and issued token unreadable.
        services.AddDataProtection().SetApplicationName("PointLurker");

        // PersistKeysToFileSystem wants a DirectoryInfo at registration time, which would force the
        // key ring path to be read from IConfiguration back in the composition root. Configuring
        // KeyManagementOptions from the bound settings is what that helper does internally anyway,
        // only deferred until the options are available.
        services
            .AddOptions<KeyManagementOptions>()
            .Configure<IOptions<DataProtectionSettings>, ILoggerFactory>(
                (keyManagement, dataProtection, loggerFactory) =>
                    keyManagement.XmlRepository = new FileSystemXmlRepository(
                        new DirectoryInfo(dataProtection.Value.KeyRingPath),
                        loggerFactory
                    )
            );

        services.AddSingleton<IDataProtectorFactory, DataProtectorFactory>();
        foreach (
            var purpose in new[]
            {
                ProtectionPurposes.StreamElementsToken,
                ProtectionPurposes.StoreInputProfile,
                ProtectionPurposes.RedemptionMessage,
                ProtectionPurposes.GauPointsSession,
                ProtectionPurposes.FulfillmentCode,
            }
        )
        {
            services.AddKeyedSingleton<ISecretProtector>(
                purpose,
                (sp, key) => sp.GetRequiredService<IDataProtectorFactory>().Create((string)key!)
            );
        }

        return services;
    }
}
