using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Architecture.Tests;

public class ApplicationConventionTests
{
    [Fact]
    public void ISingleUserProvider_exists_in_Core_Application()
    {
        var t = typeof(ISingleUserProvider);
        Assert.True(
            t.IsInterface && t.Assembly == ArchitectureAssemblies.Application,
            $"{nameof(ISingleUserProvider_exists_in_Core_Application)}: assembly={t.Assembly.GetName().Name}, isInterface={t.IsInterface}"
        );
    }

    [Fact]
    public void Application_use_cases_resolve_the_owner_via_ISingleUserProvider_not_UserManager()
    {
        var offenders = ArchitectureAssemblies
            .Application.GetTypes()
            .SelectMany(t => t.GetConstructors())
            .Where(c => c.GetParameters().Any(p => p.ParameterType.Name == "UserManager`1"))
            .Select(c => c.DeclaringType!.FullName)
            .Distinct()
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{nameof(Application_use_cases_resolve_the_owner_via_ISingleUserProvider_not_UserManager)}: {string.Join(", ", offenders)}"
        );
    }

    /// <summary>
    /// Use cases take the services they need, not the container they could pull them from. A
    /// constructor parameter is checked at startup and visible in the signature; a call-time
    /// <c>GetRequiredService</c> is neither, and it forces tests to build a service provider to
    /// exercise a use case. Where a fresh scope per item is genuinely needed — the parallel per-store
    /// pass of a sync cycle — the parameter is a typed
    /// <c>Func&lt;ScopedInstance&lt;T&gt;&gt;</c> factory instead.
    /// </summary>
    [Fact]
    public void Application_use_cases_take_their_dependencies_not_the_container()
    {
        Type[] containerTypes =
        [
            typeof(IServiceProvider),
            typeof(IKeyedServiceProvider),
            typeof(IServiceScopeFactory),
            typeof(IServiceScope),
            typeof(IServiceProviderIsService),
        ];

        var offenders = ArchitectureAssemblies
            .Application.GetTypes()
            .SelectMany(t => t.GetConstructors())
            .Where(c => c.GetParameters().Any(p => containerTypes.Contains(p.ParameterType)))
            .Select(c => c.DeclaringType!.FullName)
            .Distinct()
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{nameof(Application_use_cases_take_their_dependencies_not_the_container)}: {string.Join(", ", offenders)}"
        );
    }

    /// <summary>
    /// The counterpart to the constructor guard above, at source level: nothing in Core.Application
    /// resolves a service by type at call time. Only the layer's own DI registration may — that file
    /// <i>is</i> the composition seam, and the factories it registers are what application code takes
    /// instead.
    /// </summary>
    [Fact]
    public void Core_Application_resolves_services_only_in_its_own_registration()
    {
        const string RegistrationFile = "CoreApplicationServiceCollectionExtensions.cs";
        var resolveCall = new Regex(
            @"\.(GetRequiredService|GetService|GetRequiredKeyedService|GetKeyedService|CreateScope|CreateAsyncScope)\b",
            RegexOptions.Compiled
        );

        var files = SourceScan.CSharpFiles(SourcePaths.ProjectSourceRoot("Core.Application"));

        var offenders = files
            .Where(file => file.RelativePath != RegistrationFile)
            .SelectMany(file =>
                resolveCall
                    .Matches(file.Text)
                    .Select(match =>
                        $"{file.RelativePath}:{file.LineAt(match.Index)}: {file.ExcerptAt(match.Index)}"
                    )
            )
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Take the dependency in the constructor instead of resolving it from the container. "
                + $"Found:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
        );
    }

    [Fact]
    public void IStoreProviderClient_exists_in_Core_Application()
    {
        var t = typeof(IStoreProviderClient);
        Assert.True(
            t.IsInterface && t.Assembly == ArchitectureAssemblies.Application,
            $"{nameof(IStoreProviderClient_exists_in_Core_Application)}: assembly={t.Assembly.GetName().Name}, isInterface={t.IsInterface}"
        );
    }

    /// <summary>
    /// Every call site picks its client by the provider of the store, item or redemption it is
    /// handling. A use case that took one client directly would be wired to a single provider
    /// whatever it is handed.
    /// </summary>
    [Fact]
    public void No_application_type_takes_a_provider_client_directly()
    {
        var offenders = ArchitectureAssemblies
            .Application.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type =>
                type.GetConstructors()
                    .Any(ctor =>
                        ctor.GetParameters()
                            .Any(p => p.ParameterType == typeof(IStoreProviderClient))
                    )
            )
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Take {nameof(IStoreProviderClientFactory)} and pick the client by provider. "
                + $"Found:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
        );
    }

    [Fact]
    public void IEmailNotifier_exists_in_Core_Application()
    {
        var t = typeof(IEmailNotifier);
        Assert.True(
            t.IsInterface && t.Assembly == ArchitectureAssemblies.Application,
            $"{nameof(IEmailNotifier_exists_in_Core_Application)}: assembly={t.Assembly.GetName().Name}, isInterface={t.IsInterface}"
        );
    }

    [Fact]
    public void SyncStores_constructor_uses_the_provider_factory_and_IEmailNotifier_only()
    {
        var ctor = GetSinglePublicCtor(typeof(SyncStores));
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

        var hasProviders = paramTypes.Any(t => t == typeof(IStoreProviderClientFactory));
        var hasNotifier = paramTypes.Any(t => t == typeof(IEmailNotifier));
        var hasOldRefit = paramTypes.Any(t => t.Name == "IStreamElementsApi");
        var hasOldEmail = paramTypes.Any(t => t.Name == "IEmailService");

        Assert.True(
            hasProviders && hasNotifier && !hasOldRefit && !hasOldEmail,
            $"{nameof(SyncStores_constructor_uses_the_provider_factory_and_IEmailNotifier_only)}: hasProviderFactory={hasProviders}, hasNotifier={hasNotifier}, hasIStreamElementsApi={hasOldRefit}, hasIEmailService={hasOldEmail}"
        );
    }

    [Fact]
    public void SubmitRedemptions_constructor_uses_the_provider_factory_only()
    {
        var ctor = GetSinglePublicCtor(typeof(SubmitRedemptions));
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

        var hasProviders = paramTypes.Any(t => t == typeof(IStoreProviderClientFactory));
        var hasOldRefit = paramTypes.Any(t => t.Name == "IStreamElementsApi");

        Assert.True(
            hasProviders && !hasOldRefit,
            $"{nameof(SubmitRedemptions_constructor_uses_the_provider_factory_only)}: hasProviderFactory={hasProviders}, hasIStreamElementsApi={hasOldRefit}"
        );
    }

    [Fact]
    public void PollRedemptionStatus_constructor_uses_the_provider_factory_only()
    {
        var ctor = GetSinglePublicCtor(typeof(PollRedemptionStatus));
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

        var hasProviders = paramTypes.Any(t => t == typeof(IStoreProviderClientFactory));
        var hasOldRefit = paramTypes.Any(t => t.Name == "IStreamElementsApi");

        Assert.True(
            hasProviders && !hasOldRefit,
            $"{nameof(PollRedemptionStatus_constructor_uses_the_provider_factory_only)}: hasProviderFactory={hasProviders}, hasIStreamElementsApi={hasOldRefit}"
        );
    }

    private static System.Reflection.ConstructorInfo GetSinglePublicCtor(Type t)
    {
        var ctors = t.GetConstructors();
        return ctors.Length == 1
            ? ctors[0]
            : ctors.OrderByDescending(c => c.GetParameters().Length).First();
    }
}
