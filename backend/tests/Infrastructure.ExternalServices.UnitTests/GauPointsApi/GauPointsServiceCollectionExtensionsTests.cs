using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Infrastructure.ExternalServices.UnitTests.GauPointsApi;

/// <summary>
/// Drives <c>AddGauPointsApi</c> through a real container and real HTTP, which is what shows the
/// registration works: headers built from settings, the resilience handler, and the redeem
/// classification all sit where only a request exercises them.
/// </summary>
public sealed class GauPointsServiceCollectionExtensionsTests : IAsyncLifetime
{
    private const string Session = "header.payload.signature";

    private WireMockServer _server = null!;
    private ServiceProvider _provider = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["GauPoints:BaseUrl"] = _server.Url,
                    ["GauPoints:SessionCookieName"] = "kp_session",
                }
            )
            .Build();

        _provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddGauPointsApi()
            .BuildServiceProvider();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider.Dispose();
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    private IStoreProviderClient Client =>
        _provider.GetRequiredKeyedService<IStoreProviderClient>(StoreProvider.GauPoints);

    private void Stub(string path, string method, int status, string? body) =>
        _server
            .Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(
                body is null
                    ? Response.Create().WithStatusCode(status)
                    : Response
                        .Create()
                        .WithStatusCode(status)
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(body)
            );

    private static ProviderRedeemCommand Command(Guid id) =>
        new(
            id,
            new StoreExternalId("gaupoints"),
            new ProviderItemId("netflix"),
            [new("Full name", "Ana"), new("Phone", "1"), new("E-mail", "a@b.co")],
            null
        );

    [Fact]
    public void Factory_hands_out_the_GauPoints_client()
    {
        _provider
            .GetRequiredService<IStoreProviderClientFactory>()
            .For(StoreProvider.GauPoints)
            .Should()
            .BeOfType<GauPointsApiClient>();
    }

    [Fact]
    public async Task The_catalogue_is_fetched_with_an_identifying_user_agent()
    {
        Stub(
            "/api/items",
            "GET",
            200,
            """{"items":[{"id":"netflix","name":"N","cost":1,"type":"digital","stock":1}]}"""
        );

        var items = await Client.GetStoreItemsAsync(new StoreExternalId("gaupoints"));

        items.Should().ContainSingle().Which.Id.Should().Be(new ProviderItemId("netflix"));
        _server
            .LogEntries.Should()
            .ContainSingle()
            .Which.RequestMessage!.Headers!["User-Agent"]
            .Should()
            .ContainMatch("PointLurker/*");
    }

    [Fact]
    public async Task Lookup_proves_the_site_is_reachable_and_answers_the_fixed_identity()
    {
        Stub("/api/items", "GET", 200, """{"items":[]}""");

        var lookup = await Client.LookupStoreAsync("anything");

        lookup.ExternalId.Should().Be(new StoreExternalId("gaupoints"));
        lookup.DisplayName.Should().Be("GauPoints");
        lookup.AvatarUrl.Should().Be("https://gaupoints.com.br/logo.png");
        lookup.ItemCount.Should().Be(0);
        _server.LogEntries.Should().ContainSingle();
    }

    [Fact]
    public async Task Lookup_counts_the_catalogue_it_read()
    {
        Stub(
            "/api/items",
            "GET",
            200,
            """{"items":[{"id":"a","name":"A","cost":1,"stock":1,"type":"digital"},{"id":"b","name":"B","cost":2,"stock":0,"type":"physical"}]}"""
        );

        var lookup = await Client.LookupStoreAsync("anything");

        lookup.ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task The_session_travels_as_a_cookie_never_as_a_bearer()
    {
        Stub(
            "/api/me",
            "GET",
            200,
            """{"logged":true,"points":1234,"user":{"username":"someviewer"}}"""
        );

        var name = await Client.GetAccountNameAsync(Session);
        var points = await Client.GetPointsAsync(new StoreExternalId("gaupoints"), name, Session);

        name.Should().Be("someviewer");
        points.Should().Be(1234);
        foreach (var entry in _server.LogEntries)
        {
            entry
                .RequestMessage!.Headers!["Cookie"]
                .Should()
                .ContainSingle($"kp_session={Session}");
            entry.RequestMessage.Headers.Should().NotContainKey("Authorization");
        }
    }

    /// <summary>
    /// <c>/api/me</c> answers 200 even signed out. Reported as the 401 it means, every caller that
    /// treats a 401 as a refused credential does the same here.
    /// </summary>
    [Fact]
    public async Task A_signed_out_session_is_reported_as_unauthorized()
    {
        Stub("/api/me", "GET", 200, """{"logged":false}""");

        var act = async () => await Client.GetAccountNameAsync(Session);

        (await act.Should().ThrowAsync<ProviderApiException>()).Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Redeem_sends_the_redemption_id_as_the_idempotency_key()
    {
        var id = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        Stub("/api/redeem", "POST", 200, """{"ok":true,"message":"Resgate realizado!"}""");

        var result = await Client.RedeemAsync(Command(id), Session);

        result
            .Should()
            .Be(
                new ProviderRedemptionResult.Accepted(null, Completed: true, FulfillmentCode: null)
            );
        var request = _server.LogEntries.Should().ContainSingle().Subject.RequestMessage!;
        request.Headers!["Idempotency-Key"].Should().ContainSingle(id.ToString());
        request.Headers["Cookie"].Should().ContainSingle($"kp_session={Session}");
        request.Body.Should().Contain("\"itemId\":\"netflix\"").And.Contain("\"address\":\"\"");
    }

    [Fact]
    public async Task A_gift_card_code_in_the_answer_travels_with_the_acceptance()
    {
        Stub("/api/redeem", "POST", 200, """{"ok":true,"code":"ABCD-EFGH-1234"}""");

        var result = await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        result
            .Should()
            .Be(
                new ProviderRedemptionResult.Accepted(
                    null,
                    Completed: true,
                    FulfillmentCode: "ABCD-EFGH-1234"
                )
            );
    }

    [Fact]
    public async Task A_blank_code_is_no_code()
    {
        Stub("/api/redeem", "POST", 200, """{"ok":true,"code":"  "}""");

        var result = await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        result
            .Should()
            .BeOfType<ProviderRedemptionResult.Accepted>()
            .Which.FulfillmentCode.Should()
            .BeNull();
    }

    [Fact]
    public async Task An_error_status_with_the_ok_false_envelope_is_a_refusal()
    {
        Stub("/api/redeem", "POST", 400, """{"ok":false,"error":"Item inválido."}""");

        var result = await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        result.Should().Be(new ProviderRedemptionResult.Refused("HTTP 400: Item inválido."));
    }

    /// <summary>Whether a business refusal is 2xx is unobserved, so <c>ok</c> is read either way.</summary>
    [Fact]
    public async Task A_success_status_saying_ok_false_is_still_a_refusal()
    {
        Stub("/api/redeem", "POST", 200, """{"ok":false,"error":"Pontos insuficientes."}""");

        var result = await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        result.Should().Be(new ProviderRedemptionResult.Refused("HTTP 200: Pontos insuficientes."));
    }

    /// <summary>
    /// A success that does not say <c>false</c> may already have spent the points. Reading it as a
    /// failure would release the quota and redeem again.
    /// </summary>
    [Fact]
    public async Task A_success_status_with_no_readable_body_is_taken_as_accepted()
    {
        Stub("/api/redeem", "POST", 200, null);

        var result = await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        result.Should().BeOfType<ProviderRedemptionResult.Accepted>();
    }

    [Fact]
    public async Task A_dead_session_on_redeem_is_an_error_carrying_its_status()
    {
        Stub(
            "/api/redeem",
            "POST",
            401,
            """{"ok":false,"error":"Você precisa entrar com o Kick."}"""
        );

        var act = async () => await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        (await act.Should().ThrowAsync<ProviderApiException>()).Which.StatusCode.Should().Be(401);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    public async Task A_status_that_decided_nothing_is_an_error_and_is_sent_once(int status)
    {
        Stub("/api/redeem", "POST", status, null);

        var act = async () => await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        (await act.Should().ThrowAsync<ProviderApiException>())
            .Which.StatusCode.Should()
            .Be(status);
        _server.LogEntries.Should().ContainSingle("a redeem is never retried by the transport");
    }

    [Fact]
    public async Task A_transient_failure_on_a_read_is_retried()
    {
        const string Scenario = "transient";
        _server
            .Given(Request.Create().WithPath("/api/items").UsingGet())
            .InScenario(Scenario)
            .WillSetStateTo("recovered")
            .RespondWith(Response.Create().WithStatusCode(503));
        _server
            .Given(Request.Create().WithPath("/api/items").UsingGet())
            .InScenario(Scenario)
            .WhenStateIs("recovered")
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"items":[]}""")
            );

        var items = await Client.GetStoreItemsAsync(new StoreExternalId("gaupoints"));

        items.Should().BeEmpty();
        _server.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_site_being_unreachable_is_reported_as_no_response()
    {
        _server.Stop();

        var act = async () => await Client.RedeemAsync(Command(Guid.NewGuid()), Session);

        (await act.Should().ThrowAsync<ProviderApiException>())
            .Which.IsUnreachable.Should()
            .BeTrue();
    }

    [Fact]
    public async Task The_client_never_reads_a_redemption_back()
    {
        Client.Capabilities.SupportsRedemptionPolling.Should().BeFalse();
        var act = () => Client.GetRedemptionAsync(new StoreExternalId("gaupoints"), "x", Session);

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
