using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.HostedServices;

namespace IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _environment;
    private readonly IReadOnlyDictionary<string, string?> _extraConfiguration;

    public IStoreProviderClient? MockStreamElementsApi { get; set; }
    public IStoreProviderClient? MockGauPointsApi { get; set; }
    public IEmailNotifier? MockEmailService { get; set; }

    /// <param name="connectionString">Testcontainers PostgreSQL connection string.</param>
    /// <param name="environment">
    /// Hosting environment. Defaults to <c>Development</c>, which also loads
    /// <c>appsettings.Development.json</c>; pass <c>Production</c> to exercise production-only
    /// behaviour, and supply any settings that file would have provided via
    /// <paramref name="extraConfiguration"/>.
    /// </param>
    /// <param name="extraConfiguration">Extra configuration keys, applied after the defaults below.</param>
    public TestWebApplicationFactory(
        string connectionString,
        string environment = "Development",
        IReadOnlyDictionary<string, string?>? extraConfiguration = null
    )
    {
        _connectionString = connectionString;
        _environment = environment;
        _extraConfiguration = extraConfiguration ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Database", _connectionString);

        // Also applied as host settings: keys added through ConfigureAppConfiguration below only
        // reach the built app's IConfiguration, not builder.Configuration, so anything Program.cs
        // reads while registering services (the connection string, the CORS origins) must go here.
        foreach (var (key, value) in _extraConfiguration)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = _connectionString,
                    ["StreamElements:BaseUrl"] = "https://api.streamelements.com",
                    ["Smtp:Host"] = "localhost",
                    ["Smtp:Port"] = "587",
                    ["Smtp:Username"] = "test-user",
                    ["Smtp:Password"] = "test-pass",
                    ["Smtp:From"] = "test@example.com",
                };

                foreach (var (key, value) in _extraConfiguration)
                {
                    values[key] = value;
                }

                configuration.AddInMemoryCollection(values);
            }
        );
        builder.ConfigureServices(services =>
        {
            if (MockStreamElementsApi is not null)
            {
                // The real client's shape, so a substitute stays true to what the sync and polling
                // paths read before they ever call it.
                MockStreamElementsApi.Capabilities.Returns(
                    new StoreProviderCapabilities(
                        SupportsMessages: true,
                        SupportsMultipleStores: true,
                        SupportsRedemptionPolling: true,
                        ReturnsFulfillmentCode: false,
                        SupportsUnlimitedStock: true,
                        ReportsTotalStock: true,
                        InputProfile: InputProfileKind.PerItemDeclared,
                        ProvidesChannelMetadata: true,
                        MinimumSyncInterval: TimeSpan.Zero
                    )
                );
                MockStreamElementsApi
                    .DescribeCredential(Arg.Any<string>())
                    .Returns(ProviderCredentialHints.None);
                services.RemoveAllKeyed<IStoreProviderClient>(StoreProvider.StreamElements);
                services.AddKeyedSingleton(StoreProvider.StreamElements, MockStreamElementsApi);
            }

            if (MockGauPointsApi is not null)
            {
                // The real client's shape: one fixed store, no messages, no polling.
                MockGauPointsApi.Capabilities.Returns(
                    new StoreProviderCapabilities(
                        SupportsMessages: false,
                        SupportsMultipleStores: false,
                        SupportsRedemptionPolling: false,
                        ReturnsFulfillmentCode: true,
                        SupportsUnlimitedStock: false,
                        ReportsTotalStock: false,
                        InputProfile: InputProfileKind.FixedSchema,
                        ProvidesChannelMetadata: false,
                        MinimumSyncInterval: TimeSpan.FromMinutes(5)
                    )
                );
                MockGauPointsApi
                    .DescribeCredential(Arg.Any<string>())
                    .Returns(ProviderCredentialHints.None);
                services.RemoveAllKeyed<IStoreProviderClient>(StoreProvider.GauPoints);
                services.AddKeyedSingleton(StoreProvider.GauPoints, MockGauPointsApi);
            }

            if (MockEmailService is not null)
            {
                services.RemoveAll<IEmailNotifier>();
                services.AddSingleton(MockEmailService);
            }

            services.AddDataProtection().UseEphemeralDataProtectionProvider();

            services.RemoveAll<IHostedService>();
        });
    }
}
