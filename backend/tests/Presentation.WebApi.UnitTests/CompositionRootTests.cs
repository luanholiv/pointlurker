using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.ExternalServices.Email;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using PointLurker.Presentation.WebApi.Auth;
using PointLurker.Presentation.WebApi.Cors;
using PointLurker.Presentation.WebApi.Errors;
using PointLurker.Presentation.WebApi.HostedServices;
using PointLurker.Presentation.WebApi.Validation;

namespace Presentation.WebApi.UnitTests;

/// <summary>
/// Resolves every service the application registers through a factory delegate, from a container
/// built exactly the way the composition root builds it.
/// </summary>
/// <remarks>
/// <para>
/// Factory-registered services are the container's blind spot. <c>ValidateOnBuild</c> checks that
/// constructor-injected dependencies can be satisfied, but it cannot look inside a lambda, so a
/// factory that throws is reported nowhere — not at startup, not at build, only on the first
/// request that needs the service. For a service injected into an endpoint handler that means a
/// 500 raised during argument binding, before the handler or any of its filters run, which is a
/// long way from the registration that caused it.
/// </para>
/// <para>
/// A Refit client is registered exactly this way, and a Refit major bump once made resolving one
/// throw while the whole build and every client-level test stayed green. This test is the general
/// form of that guard: the next external client added to the composition root is covered by it on
/// the day it is registered, without anyone remembering to write this test again.
/// </para>
/// </remarks>
public sealed class CompositionRootTests : IDisposable
{
    private readonly string _keyRingPath = Directory.CreateTempSubdirectory("dp-keys").FullName;

    public void Dispose() => Directory.Delete(_keyRingPath, recursive: true);

    /// <summary>
    /// Everything the options validators demand before the app will start. Keeping it here rather
    /// than reading <c>appsettings.Development.json</c> makes the required surface explicit: a new
    /// required setting cannot be added without this test naming it.
    /// </summary>
    private IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] =
                        "Host=localhost;Database=test;Username=test;Password=test;",
                    ["StreamElements:BaseUrl"] = "https://api.streamelements.com",
                    ["InputResolution:MatchThreshold"] = "85",
                    ["Sync:FetchCronExpression"] = "* * * * *",
                    ["Sync:UsernameRefreshCronExpression"] = "0 0 * * *",
                    ["Sync:MaxDegreeOfParallelism"] = "4",
                    ["Sync:ResolutionLogRetentionDays"] = "30",
                    ["Redemptions:PollingCronExpression"] = "* * * * *",
                    ["Redemptions:CompletionTimeoutHours"] = "24",
                    ["Smtp:Host"] = "smtp.example.com",
                    ["Smtp:Port"] = "587",
                    ["Smtp:Username"] = "sender@example.com",
                    ["Smtp:Password"] = "password",
                    ["Smtp:From"] = "sender@example.com",
                    ["Smtp:Security"] = "StartTls",
                    ["Smtp:MaxRetries"] = "3",
                    ["Smtp:RetryBaseDelaySeconds"] = "2",
                    ["Authentication:AccessTokenExpirationMinutes"] = "30",
                    ["Authentication:RefreshTokenExpirationHours"] = "24",
                    ["DataProtection:KeyRingPath"] = _keyRingPath,
                }
            )
            .Build();

    /// <summary>
    /// Mirrors <c>Program.cs</c>. A registration added there belongs here too. <c>AddOpenApi</c>
    /// and <c>AddHealthChecks</c> are the two omissions: they register nothing this test can claim,
    /// and the OpenAPI source generator does not run outside the web project.
    /// </summary>
    private IServiceCollection BuildServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddLogging();

        services.AddApiErrorHandling();
        services.AddRequestValidation();
        services.AddInfrastructureDatabase(configuration.GetConnectionString("Database")!);
        services.AddTokenEncryption();
        services.AddStreamElementsApi();
        services.AddGauPointsApi();
        services.AddSmtpEmail();
        services.AddCoreApplication();
        services.AddBackgroundJobs();
        services.AddAppAuthentication();
        services.AddConfiguredCors(configuration, isDevelopment: false);

        return services;
    }

    [Fact]
    public void Every_factory_registered_application_service_resolves()
    {
        var configuration = BuildConfiguration();
        var services = BuildServices(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        using var scope = provider.CreateScope();

        // Framework registrations are excluded by service type, which also drops the hosted
        // services: those are registered as IHostedService, and starting them is a different
        // question from being able to construct them.
        var factoryRegistered = services
            .Where(descriptor =>
                (
                    descriptor.IsKeyedService
                        ? descriptor.KeyedImplementationFactory is not null
                        : descriptor.ImplementationFactory is not null
                )
                && !descriptor.ServiceType.IsGenericTypeDefinition
                && IsApplicationOwned(descriptor.ServiceType)
            )
            .ToList();

        // Naming the Refit pair keeps the filter honest: a predicate that quietly stops matching
        // still passes every resolve below, and the test would report success over an empty sweep.
        factoryRegistered
            .Select(descriptor => descriptor.ServiceType)
            .Should()
            .Contain([
                typeof(IStoreProviderClient),
                typeof(IStreamElementsApi),
                typeof(IGauPointsApi),
            ]);

        foreach (var descriptor in factoryRegistered)
        {
            var resolve = () =>
                descriptor.IsKeyedService
                    ? scope.ServiceProvider.GetRequiredKeyedService(
                        descriptor.ServiceType,
                        descriptor.ServiceKey
                    )
                    : scope.ServiceProvider.GetRequiredService(descriptor.ServiceType);

            resolve
                .Should()
                .NotThrow(
                    "{0} is registered through a factory, so nothing else checks it can be built",
                    Describe(descriptor)
                );
        }
    }

    /// <summary>
    /// The provider factory looks its client up only when a call site asks for one, so the sweep
    /// above cannot see a provider whose keyed registration is missing: that surfaces on the first
    /// sync of a store of that provider. Every provider a store can be created for has to answer.
    /// </summary>
    [Fact]
    public void Provider_client_factory_builds_a_client_for_every_supported_provider()
    {
        var configuration = BuildConfiguration();
        var services = BuildServices(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IStoreProviderClientFactory>();

        foreach (var storeProvider in Enum.GetValues<StoreProvider>())
        {
            factory.For(storeProvider).Provider.Should().Be(storeProvider);
        }
    }

    /// <summary>
    /// Matched on the namespace, not the assembly name: only one project sets an
    /// <c>AssemblyName</c> carrying the product prefix, so an assembly-name test would quietly
    /// cover that project alone. Every source file declares the prefixed namespace.
    /// </summary>
    private static bool IsApplicationOwned(Type serviceType) =>
        serviceType.Namespace?.StartsWith("PointLurker", StringComparison.Ordinal) == true;

    private static string Describe(ServiceDescriptor descriptor) =>
        descriptor.IsKeyedService
            ? $"{descriptor.ServiceType.Name} (key: {descriptor.ServiceKey})"
            : descriptor.ServiceType.Name;
}
