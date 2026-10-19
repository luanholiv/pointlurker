using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.HostedServices;

namespace Presentation.WebApi.UnitTests.HostedServices;

public class BackgroundJobsServiceCollectionExtensionsTests
{
    [Fact]
    public async Task Pairs_the_redemption_writer_and_reader_to_the_same_channel()
    {
        var provider = new ServiceCollection().AddBackgroundJobs().BuildServiceProvider();

        var writer = provider.GetRequiredService<ChannelWriter<Redemption>>();
        var reader = provider.GetRequiredService<ChannelReader<Redemption>>();

        // Completing the writer only completes this reader if both sides came from one channel.
        writer.TryComplete().Should().BeTrue();
        await reader.Completion;
    }

    [Fact]
    public async Task Pairs_the_username_refresh_writer_and_reader_to_the_same_channel()
    {
        var provider = new ServiceCollection().AddBackgroundJobs().BuildServiceProvider();

        var writer = provider.GetRequiredService<ChannelWriter<UsernameRefreshSignal>>();
        var reader = provider.GetRequiredService<ChannelReader<UsernameRefreshSignal>>();

        await writer.WriteAsync(UsernameRefreshSignal.Instance);

        (await reader.ReadAsync()).Should().BeSameAs(UsernameRefreshSignal.Instance);
    }

    [Fact]
    public async Task Pairs_the_fulfillment_writer_and_reader_to_the_same_channel()
    {
        var provider = new ServiceCollection().AddBackgroundJobs().BuildServiceProvider();

        var writer = provider.GetRequiredService<ChannelWriter<FulfillmentCodeReady>>();
        var reader = provider.GetRequiredService<ChannelReader<FulfillmentCodeReady>>();

        await writer.WriteAsync(FulfillmentCodeReady.Instance);

        (await reader.ReadAsync()).Should().BeSameAs(FulfillmentCodeReady.Instance);
    }

    [Fact]
    public void Registers_the_channel_endpoints_as_singletons()
    {
        var services = new ServiceCollection().AddBackgroundJobs();

        var channelServices = new[]
        {
            typeof(ChannelWriter<Redemption>),
            typeof(ChannelReader<Redemption>),
            typeof(ChannelWriter<UsernameRefreshSignal>),
            typeof(ChannelReader<UsernameRefreshSignal>),
            typeof(ChannelWriter<FulfillmentCodeReady>),
            typeof(ChannelReader<FulfillmentCodeReady>),
        };

        foreach (var serviceType in channelServices)
        {
            services
                .Single(d => d.ServiceType == serviceType)
                .Lifetime.Should()
                .Be(ServiceLifetime.Singleton);
        }
    }

    /// <summary>
    /// The <see cref="IHostedService"/> registration order is the startup order, so it is part of
    /// the contract rather than an implementation detail.
    /// </summary>
    [Fact]
    public void Registers_the_five_hosted_services_in_startup_order()
    {
        var services = new ServiceCollection().AddBackgroundJobs();

        var hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .ToArray();

        hostedServices
            .Should()
            .Equal(
                typeof(StoreSyncBackgroundService),
                typeof(RedemptionSubmissionBackgroundService),
                typeof(RedemptionPollingBackgroundService),
                typeof(ProviderAccountRefreshBackgroundService),
                typeof(FulfillmentNotificationBackgroundService)
            );
    }
}
