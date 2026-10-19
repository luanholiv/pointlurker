using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Infrastructure.ExternalServices.UnitTests.StreamElementsApi;

/// <summary>
/// Drives <c>AddStreamElementsApi</c> through a real <see cref="ServiceProvider"/> and a real HTTP
/// round trip, which is the only thing that can show the registration is <em>usable</em> rather
/// than merely present.
/// </summary>
/// <remarks>
/// Resolution alone is not enough. A Refit client is assembled from a generated implementation, a
/// base address taken from options, and a resilience handler, and the pieces fail independently:
/// the generated client can fail to resolve, the base address can go missing, a route template is
/// only validated when the first request is built, and the retry handler can fall out of the
/// message pipeline without anything else noticing. Each assertion below covers one of those
/// seams. The payloads are deliberately minimal — response shape is the contract tests' job, and
/// duplicating their fixtures here would make this test fail for reasons that are not about wiring.
/// </remarks>
public sealed class StreamElementsServiceCollectionExtensionsTests : IAsyncLifetime
{
    private const string ChannelId = "5cc799026e852d26fcf16717";
    private const string Username = "luanholiv";
    private const string Token = "se-token";

    private WireMockServer _server = null!;
    private ServiceProvider _provider = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        _provider = BuildProvider(_server.Url!);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider.Dispose();
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    private static ServiceProvider BuildProvider(string baseUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["StreamElements:BaseUrl"] = baseUrl }
            )
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddStreamElementsApi()
            .BuildServiceProvider();
    }

    private void StubJson(string path, string body, string method = "GET") =>
        _server
            .Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(body)
            );

    /// <summary>
    /// Refit resolves a client registered through the container by a different path than
    /// <c>RestService.For</c>, so a client every contract test can build may still throw the moment
    /// the container tries to hand it to an endpoint. That gap is what this asserts away.
    /// </summary>
    [Fact]
    public void Registration_resolves_both_the_refit_client_and_the_wrapper()
    {
        var act = () =>
        {
            _provider.GetRequiredService<IStreamElementsApi>();
            _provider.GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Factory_hands_out_the_wrapper_for_StreamElements()
    {
        _provider
            .GetRequiredService<IStoreProviderClientFactory>()
            .For(StoreProvider.StreamElements)
            .Should()
            .BeOfType<StreamElementsApiClient>();
    }

    [Fact]
    public void Factory_refuses_a_provider_nothing_is_registered_for()
    {
        var act = () =>
            _provider
                .GetRequiredService<IStoreProviderClientFactory>()
                .For(StoreProvider.GauPoints);

        act.Should().Throw<InvalidOperationException>().WithMessage("*GauPoints*");
    }

    [Fact]
    public async Task Store_items_route_composes_against_the_configured_base_address()
    {
        StubJson($"/kappa/v2/store/{ChannelId}/items", "[]");

        var items = await _provider
            .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
            .GetStoreItemsAsync(new StoreExternalId(ChannelId), CancellationToken.None);

        items.Should().BeEmpty();
        _server
            .FindLogEntries(
                Request
                    .Create()
                    .WithPath($"/kappa/v2/store/{ChannelId}/items")
                    .WithParam("source", "website")
                    .UsingGet()
            )
            .Should()
            .ContainSingle(
                "the route template's literal query string has to survive request building"
            );
    }

    [Fact]
    public async Task Channel_lookup_deserializes_through_the_registered_client()
    {
        StubJson(
            $"/kappa/v2/channels/{Username}",
            $$"""{"_id":"{{ChannelId}}","username":"{{Username}}","displayName":"Luan","avatar":"https://cdn/a.png"}"""
        );

        var channel = await _provider
            .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
            .LookupStoreAsync(Username, CancellationToken.None);

        channel
            .Should()
            .Be(
                new ProviderStoreLookup(
                    new StoreExternalId(ChannelId),
                    Username,
                    "Luan",
                    "https://cdn/a.png"
                )
            );
    }

    [Fact]
    public async Task Bearer_token_reaches_the_wire_as_an_authorization_header()
    {
        StubJson("/kappa/v2/users/current", """{"username":"luanholiv"}""");

        var username = await _provider
            .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
            .GetAccountNameAsync(Token, CancellationToken.None);

        username.Should().Be(Username);
        _server
            .FindLogEntries(
                Request
                    .Create()
                    .WithPath("/kappa/v2/users/current")
                    .WithHeader("Authorization", $"Bearer {Token}")
                    .UsingGet()
            )
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Points_route_composes_both_path_parameters()
    {
        StubJson($"/kappa/v2/points/{ChannelId}/{Username}", """{"points":1234}""");

        var points = await _provider
            .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
            .GetPointsAsync(
                new StoreExternalId(ChannelId),
                Username,
                Token,
                CancellationToken.None
            );

        points.Should().Be(1234);
    }

    [Fact]
    public async Task Redeem_serializes_the_body_and_reads_the_response()
    {
        const string ItemId = "item-1";
        StubJson(
            $"/kappa/v2/store/{ChannelId}/redemptions/{ItemId}",
            $$"""
            {"_id":"redemption-1","channel":"{{ChannelId}}","item":"{{ItemId}}","completed":false,
             "input":["ign"],"message":"gg","createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}
            """,
            "POST"
        );

        var redemption = await _provider
            .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
            .RedeemAsync(
                new ProviderRedeemCommand(
                    Guid.NewGuid(),
                    new StoreExternalId(ChannelId),
                    new ProviderItemId(ItemId),
                    [new ProviderInputValue("IGN", "ign")],
                    "gg"
                ),
                Token,
                CancellationToken.None
            );

        redemption
            .Should()
            .BeOfType<ProviderRedemptionResult.Accepted>()
            .Which.RedemptionId.Should()
            .Be("redemption-1");
        _server
            .FindLogEntries(
                Request
                    .Create()
                    .WithPath($"/kappa/v2/store/{ChannelId}/redemptions/{ItemId}")
                    .WithBody("*\"input\":[\"ign\"]*")
                    .UsingPost()
            )
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Get_redemption_is_routed_by_verb_not_by_path()
    {
        const string RedemptionId = "redemption-1";
        _server
            .Given(
                Request
                    .Create()
                    .WithPath($"/kappa/v2/store/{ChannelId}/redemptions/{RedemptionId}")
                    .UsingGet()
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(
                        // The lookup expands `item` into an object and carries `rejected`, unlike
                        // the redeem response.
                        $$"""
                        {"_id":"{{RedemptionId}}","channel":"{{ChannelId}}",
                         "item":{"_id":"item-1","name":"Item","userInput":[]},
                         "completed":true,"rejected":false,"input":[],
                         "createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}
                        """
                    )
            );

        var redemption = await _provider
            .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
            .GetRedemptionAsync(
                new StoreExternalId(ChannelId),
                RedemptionId,
                Token,
                CancellationToken.None
            );

        redemption.Completed.Should().BeTrue();
    }

    /// <summary>
    /// A permanent error has to reach the caller as itself: translated to a
    /// <see cref="ProviderApiException"/> carrying the status, and delivered on the first attempt. Both
    /// halves matter — retrying a 404 would multiply every miss by four and delay the answer.
    /// </summary>
    [Fact]
    public async Task A_permanent_error_is_translated_and_not_retried()
    {
        _server
            .Given(Request.Create().WithPath("/kappa/v2/channels/ghost").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404).WithBody("no such channel"));

        var act = async () =>
            await _provider
                .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
                .LookupStoreAsync("ghost", CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ProviderApiException>();
        thrown.Which.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        thrown.Which.IsUnreachable.Should().BeFalse();
        _server
            .FindLogEntries(Request.Create().WithPath("/kappa/v2/channels/ghost").UsingGet())
            .Should()
            .ContainSingle();
    }

    /// <summary>
    /// The retry handler lives in the message pipeline, so nothing but a real request can show it
    /// is still there. One retry is enough, and keeps the configured two-second backoff from
    /// dominating the suite.
    /// </summary>
    [Fact]
    public async Task Resilience_handler_retries_a_transient_failure()
    {
        const string Scenario = "transient";
        var path = $"/kappa/v2/store/{ChannelId}/items";

        _server
            .Given(Request.Create().WithPath(path).UsingGet())
            .InScenario(Scenario)
            .WillSetStateTo("recovered")
            .RespondWith(Response.Create().WithStatusCode(500));

        _server
            .Given(Request.Create().WithPath(path).UsingGet())
            .InScenario(Scenario)
            .WhenStateIs("recovered")
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("[]")
            );

        var items = await _provider
            .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
            .GetStoreItemsAsync(new StoreExternalId(ChannelId), CancellationToken.None);

        items.Should().BeEmpty();
        _server
            .FindLogEntries(Request.Create().WithPath(path).UsingGet())
            .Should()
            .HaveCount(2, "the 500 has to be retried rather than surfaced");
    }

    /// <summary>
    /// A redeem that fails with a 5xx may still have been created upstream, and StreamElements
    /// honours no idempotency key, so a transport retry could spend the points again. The use case
    /// owns the one retry that is allowed: counted, durable, and scheduled.
    /// </summary>
    [Fact]
    public async Task Redeem_is_never_retried_by_the_transport()
    {
        const string ItemId = "item-1";
        var path = $"/kappa/v2/store/{ChannelId}/redemptions/{ItemId}";
        _server
            .Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        var act = async () =>
            await _provider
                .GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.StreamElements)
                .RedeemAsync(
                    new ProviderRedeemCommand(
                        Guid.NewGuid(),
                        new StoreExternalId(ChannelId),
                        new ProviderItemId(ItemId),
                        [],
                        null
                    ),
                    Token,
                    CancellationToken.None
                );

        var thrown = await act.Should().ThrowAsync<ProviderApiException>();
        thrown.Which.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        _server
            .FindLogEntries(Request.Create().WithPath(path).UsingPost())
            .Should()
            .ContainSingle("a retried redeem can create a second redemption upstream");
    }
}
