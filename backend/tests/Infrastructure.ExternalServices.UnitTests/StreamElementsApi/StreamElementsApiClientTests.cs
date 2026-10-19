using System.Net;
using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using Refit;

namespace Infrastructure.ExternalServices.UnitTests.StreamElementsApi;

public sealed class StreamElementsApiClientTests
{
    [Fact]
    public async Task GetChannelAsync_passes_channel_id_path_parameter_unchanged()
    {
        const string channelId = "5cc799026e852d26fcf16717";
        var refit = Substitute.For<IStreamElementsApi>();
        refit
            .GetChannelByUsernameAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(new ChannelLookupResponse(channelId, "gaules", "Gaules"));
        var client = new StreamElementsApiClient(refit);

        await client.LookupStoreAsync(channelId);

        await refit.Received(1).GetChannelByUsernameAsync(channelId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Describes_StreamElements_as_a_multi_store_per_item_input_provider()
    {
        var client = new StreamElementsApiClient(Substitute.For<IStreamElementsApi>());

        client.Provider.Should().Be(StoreProvider.StreamElements);
        client
            .Capabilities.Should()
            .Be(
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
    }

    [Fact]
    public async Task LookupStoreAsync_surfaces_avatar_from_lookup()
    {
        const string channelId = "5cc799026e852d26fcf16717";
        var refit = Substitute.For<IStreamElementsApi>();
        refit
            .GetChannelByUsernameAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(
                new ChannelLookupResponse(channelId, "gaules", "Gaules")
                {
                    Avatar = "https://cdn.example.com/avatar.png",
                }
            );
        var client = new StreamElementsApiClient(refit);

        var lookup = await client.LookupStoreAsync(channelId);

        lookup.AvatarUrl.Should().Be("https://cdn.example.com/avatar.png");
        lookup.DisplayName.Should().Be("Gaules");
    }

    /// <summary>
    /// This client is the only place allowed to know Refit exists, so every Refit failure shape has
    /// to leave it as a <see cref="ProviderApiException"/>. Callers branch on that type alone, and a
    /// shape that slips through reaches an endpoint as an unhandled exception — a 500 where the
    /// caller had a considered answer ready.
    /// </summary>
    [Fact]
    public async Task An_error_response_is_translated_to_SeApiException()
    {
        var refit = Substitute.For<IStreamElementsApi>();
        refit
            .GetChannelByUsernameAsync("ghost", Arg.Any<CancellationToken>())
            .Returns<ChannelLookupResponse>(_ => throw CreateApiException(HttpStatusCode.NotFound));
        var client = new StreamElementsApiClient(refit);

        var act = async () => await client.LookupStoreAsync("ghost");

        var thrown = await act.Should().ThrowAsync<ProviderApiException>();
        thrown.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        thrown.Which.IsUnreachable.Should().BeFalse();
    }

    [Fact]
    public async Task A_transport_failure_is_translated_to_an_unreachable_SeApiException()
    {
        var refit = Substitute.For<IStreamElementsApi>();
        refit
            .GetChannelByUsernameAsync("gaules", Arg.Any<CancellationToken>())
            .Returns<ChannelLookupResponse>(_ =>
                throw new HttpRequestException("Connection refused")
            );
        var client = new StreamElementsApiClient(refit);

        var act = async () => await client.LookupStoreAsync("gaules");

        var thrown = await act.Should().ThrowAsync<ProviderApiException>();
        thrown.Which.IsUnreachable.Should().BeTrue();
        thrown.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    /// <summary>
    /// A caller's own cancellation has to come back as cancellation. Translating it would turn a
    /// deliberate shutdown into what looks like a StreamElements outage.
    /// </summary>
    [Fact]
    public async Task Cancellation_is_not_translated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var refit = Substitute.For<IStreamElementsApi>();
        refit
            .GetChannelByUsernameAsync("gaules", Arg.Any<CancellationToken>())
            .Returns<ChannelLookupResponse>(_ => throw new OperationCanceledException(cts.Token));
        var client = new StreamElementsApiClient(refit);

        var act = async () => await client.LookupStoreAsync("gaules", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RedeemAsync_reports_too_few_points_as_a_refusal_rather_than_an_error()
    {
        const string Body =
            "{\"statusCode\":400,\"error\":\"Bad Request\",\"message\":\"Sorry you don't have enough points\"}";
        var client = new StreamElementsApiClient(
            RedeemFailingWith(HttpStatusCode.BadRequest, Body)
        );

        var result = await client.RedeemAsync(Command, "token");

        result.Should().Be(new ProviderRedemptionResult.InsufficientPoints($"HTTP 400: {Body}"));
    }

    /// <summary>
    /// Only the one wording is a refusal. Any other 400 is still an error the use case classifies by
    /// status, so a reworded message degrades to a generic failure rather than a false refusal.
    /// </summary>
    [Fact]
    public async Task RedeemAsync_leaves_any_other_bad_request_as_an_error()
    {
        var client = new StreamElementsApiClient(
            RedeemFailingWith(HttpStatusCode.BadRequest, "{\"message\":\"Item is disabled\"}")
        );

        var act = async () => await client.RedeemAsync(Command, "token");

        var thrown = await act.Should().ThrowAsync<ProviderApiException>();
        thrown.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RedeemAsync_does_not_read_enough_points_off_a_status_other_than_400()
    {
        var client = new StreamElementsApiClient(
            RedeemFailingWith(HttpStatusCode.InternalServerError, "not enough points to log")
        );

        var act = async () => await client.RedeemAsync(Command, "token");

        var thrown = await act.Should().ThrowAsync<ProviderApiException>();
        thrown.Which.StatusCode.Should().Be(500);
    }

    private static readonly ProviderRedeemCommand Command = new(
        Guid.NewGuid(),
        new StoreExternalId("5cc799026e852d26fcf16717"),
        new ProviderItemId("item-1"),
        [],
        null
    );

    private static IStreamElementsApi RedeemFailingWith(HttpStatusCode status, string body)
    {
        var refit = Substitute.For<IStreamElementsApi>();
        refit
            .RedeemAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<RedeemRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<RedemptionResponse>(_ => throw CreateApiException(status, body));
        return refit;
    }

    private static ApiException CreateApiException(HttpStatusCode status, string? body = null) =>
        ApiException
            .Create(
                new HttpRequestMessage(HttpMethod.Get, "https://api.streamelements.com/"),
                HttpMethod.Get,
                new HttpResponseMessage(status)
                {
                    Content = body is null ? null : new StringContent(body),
                },
                new RefitSettings()
            )
            .GetAwaiter()
            .GetResult();
}
