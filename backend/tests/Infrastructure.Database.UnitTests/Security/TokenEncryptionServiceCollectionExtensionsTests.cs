using FluentAssertions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Security;

namespace Infrastructure.Database.UnitTests.Security;

public sealed class TokenEncryptionServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _keyRingPath = Path.Combine(
        Path.GetTempPath(),
        $"token-encryption-tests-{Guid.NewGuid():N}"
    );

    [Fact]
    public void Registers_the_data_protection_settings_validator()
    {
        var services = BuildServices();

        services
            .Should()
            .Contain(d =>
                d.ServiceType == typeof(IValidateOptions<DataProtectionSettings>)
                && d.ImplementationType == typeof(DataProtectionSettingsValidator)
            );
    }

    [Fact]
    public void Persists_the_key_ring_to_the_configured_path()
    {
        var provider = BuildServices(
                new Dictionary<string, string?> { ["DataProtection:KeyRingPath"] = _keyRingPath }
            )
            .BuildServiceProvider();

        var keyManagement = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        keyManagement.XmlRepository.Should().BeOfType<FileSystemXmlRepository>();
        keyManagement
            .XmlRepository.As<FileSystemXmlRepository>()
            .Directory.FullName.Should()
            .Be(Path.GetFullPath(_keyRingPath));
    }

    /// <summary>
    /// The container path is the options default rather than a fallback at the composition root, so
    /// unconfigured resolution must still land on it.
    /// </summary>
    [Fact]
    public void Falls_back_to_the_container_key_ring_path()
    {
        var provider = BuildServices().BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<DataProtectionSettings>>().Value;

        settings.KeyRingPath.Should().Be("/app/.aspnet/DataProtection-Keys");
    }

    [Fact]
    public void Rejects_a_blank_key_ring_path()
    {
        var provider = BuildServices(
                new Dictionary<string, string?> { ["DataProtection:KeyRingPath"] = "" }
            )
            .BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<DataProtectionSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyRingPath))
        {
            Directory.Delete(_keyRingPath, recursive: true);
        }
    }

    private static IServiceCollection BuildServices(
        Dictionary<string, string?>? configuration = null
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build()
        );
        services.AddTokenEncryption();
        return services;
    }
}
