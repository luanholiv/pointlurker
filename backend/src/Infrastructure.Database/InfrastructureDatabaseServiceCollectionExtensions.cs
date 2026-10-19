using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Infrastructure.Database.Identity;
using PointLurker.Infrastructure.Database.Repositories;

namespace PointLurker.Infrastructure.Database;

public static class InfrastructureDatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureDatabase(
        this IServiceCollection services,
        string connectionString
    )
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                    npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
        );

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        services.AddScoped<ISingleUserProvider, SingleUserProvider>();
        services.AddScoped<IProviderCredentialProvider, ProviderCredentialProvider>();

        // Scoped with the repositories and the AppDbContext they share: the repositories stage into
        // that context and this commits it, so both halves of a write see the same change tracker.
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IWatchedStoreRepository, WatchedStoreRepository>();
        services.AddScoped<IStoreItemRepository, StoreItemRepository>();
        services.AddScoped<IWishlistEntryRepository, WishlistEntryRepository>();
        services.AddScoped<IRedemptionRepository, RedemptionRepository>();
        services.AddScoped<IFulfillmentCodeRepository, FulfillmentCodeRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IStoreInputResolutionLogRepository, StoreInputResolutionLogRepository>();

        return services;
    }
}
