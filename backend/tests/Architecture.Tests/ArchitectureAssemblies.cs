using System.Reflection;
using NetArchTest.Rules;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using PointLurker.Presentation.WebApi.HostedServices;

namespace PointLurker.Architecture.Tests;

internal static class ArchitectureAssemblies
{
    public static Assembly Domain { get; } = typeof(ApplicationUser).Assembly;
    public static Assembly Application { get; } = typeof(SyncStores).Assembly;
    public static Assembly InfrastructureDatabase { get; } = typeof(AppDbContext).Assembly;
    public static Assembly InfrastructureExternalServices { get; } =
        typeof(StreamElementsApiClient).Assembly;
    public static Assembly Presentation { get; } = typeof(StoreSyncBackgroundService).Assembly;

    public static IReadOnlyList<Assembly> All { get; } =
    [Domain, Application, InfrastructureDatabase, InfrastructureExternalServices, Presentation];

    public static IReadOnlyList<Assembly> SourceAssemblies { get; } = All;
}

internal static class ArchAssert
{
    public static string Format(string rule, TestResult result) =>
        $"{rule}: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
