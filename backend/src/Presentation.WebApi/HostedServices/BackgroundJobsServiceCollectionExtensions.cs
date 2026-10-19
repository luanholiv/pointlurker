using System.Threading.Channels;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Presentation.WebApi.HostedServices;

public static class BackgroundJobsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the five hosted services and the channels they consume. Only the writer and
    /// reader ends are exposed to the container — producers take a
    /// <see cref="ChannelWriter{T}"/> and consumers a <see cref="ChannelReader{T}"/>, so nothing
    /// needs the channel itself.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        services.AddChannel<Redemption>();
        services.AddChannel<UsernameRefreshSignal>();
        services.AddChannel<FulfillmentCodeReady>();

        // Registration order is startup order.
        services.AddHostedService<StoreSyncBackgroundService>();
        services.AddHostedService<RedemptionSubmissionBackgroundService>();
        services.AddHostedService<RedemptionPollingBackgroundService>();
        services.AddHostedService<ProviderAccountRefreshBackgroundService>();
        services.AddHostedService<FulfillmentNotificationBackgroundService>();

        return services;
    }

    private static void AddChannel<T>(this IServiceCollection services)
    {
        var channel = Channel.CreateUnbounded<T>();
        services.AddSingleton(channel.Writer);
        services.AddSingleton(channel.Reader);
    }
}
