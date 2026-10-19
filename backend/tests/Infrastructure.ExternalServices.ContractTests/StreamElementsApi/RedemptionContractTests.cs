using System.Text.Json;
using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using Refit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.StreamElementsApi;

[Trait("Category", "Contract")]
public sealed class RedemptionContractTests : IAsyncLifetime
{
    private const string ChannelId = "5ea766d578edd899791c1eb8";
    private const string ItemId = "5ecede07db1d069c8feacee2";

    private WireMockServer _server = null!;
    private IStreamElementsApi _api = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();

        var fixtureJson = File.ReadAllText(Path.Combine("Fixtures", "redemption-response.json"));

        _server
            .Given(
                Request
                    .Create()
                    .WithPath($"/kappa/v2/store/{ChannelId}/redemptions/{ItemId}")
                    .UsingPost()
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(fixtureJson)
            );

        _api = RestService.For<IStreamElementsApi>(_server.Url!);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Request_body_serializes_input_and_message()
    {
        var request = new RedeemRequest { Input = ["test@email.com", "John Doe"], Message = "" };

        await _api.RedeemAsync(ChannelId, ItemId, request, "Bearer test-token");

        var logEntry = _server.LogEntries.Should().ContainSingle().Subject;
        var body = JsonDocument.Parse(logEntry.RequestMessage!.Body!);

        body.RootElement.GetProperty("input")
            .EnumerateArray()
            .Select(e => e.GetString())
            .Should()
            .BeEquivalentTo(["test@email.com", "John Doe"]);

        body.RootElement.GetProperty("message").GetString().Should().Be("");
    }

    [Fact]
    public void Serialize_RedeemRequest_with_null_message_omits_message_key()
    {
        var request = new RedeemRequest { Input = ["a@b.com"], Message = null };

        var json = JsonSerializer.Serialize(request);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("message", out _).Should().BeFalse();
    }

    [Fact]
    public void Serialize_RedeemRequest_with_empty_message_includes_message_key()
    {
        var request = new RedeemRequest { Input = ["a@b.com"], Message = "" };

        var json = JsonSerializer.Serialize(request);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("message", out var prop).Should().BeTrue();
        prop.GetString().Should().Be("");
    }

    [Fact]
    public async Task RedeemAsync_with_null_message_sends_body_without_message_key()
    {
        var request = new RedeemRequest { Input = ["test@email.com"], Message = null };

        await _api.RedeemAsync(ChannelId, ItemId, request, "Bearer test-token");

        var logEntry = _server.LogEntries.Should().ContainSingle().Subject;
        var body = JsonDocument.Parse(logEntry.RequestMessage!.Body!);

        body.RootElement.TryGetProperty("message", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Response_deserializes_all_fields()
    {
        var request = new RedeemRequest { Input = ["test@email.com", "John Doe"] };

        var response = await _api.RedeemAsync(ChannelId, ItemId, request, "Bearer test-token");

        response.Id.Should().Be("69a595406f7bf684bcf63750");
        response.Channel.Should().Be(ChannelId);
        response.Item.Should().Be(ItemId);
        response.Completed.Should().BeFalse();
        response.Input.Should().BeEquivalentTo(["test@email.com", "John Doe"]);
        response.Message.Should().Be("");
        response.CreatedAt.Should().Be(DateTimeOffset.Parse("2026-03-02T13:48:48.043Z"));
        response.UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-03-02T13:48:48.043Z"));
    }

    [Fact]
    public async Task Bearer_token_is_present_in_authorization_header()
    {
        var request = new RedeemRequest { Input = ["test@email.com"] };

        await _api.RedeemAsync(ChannelId, ItemId, request, "Bearer test-token");

        var logEntry = _server.LogEntries.Should().ContainSingle().Subject;
        logEntry.RequestMessage!.Headers.Should().ContainKey("Authorization");
        logEntry
            .RequestMessage.Headers!["Authorization"]
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Bearer");
    }

    [Fact]
    public async Task Error_response_throws_ApiException()
    {
        _server.Reset();
        _server
            .Given(
                Request
                    .Create()
                    .WithPath($"/kappa/v2/store/{ChannelId}/redemptions/{ItemId}")
                    .UsingPost()
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(400)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"statusCode":400,"message":"Item out of stock"}""")
            );

        var request = new RedeemRequest { Input = ["test@email.com"] };

        var act = () => _api.RedeemAsync(ChannelId, ItemId, request, "Bearer test-token");

        await act.Should()
            .ThrowAsync<ApiException>()
            .Where(e => e.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRedemptionAsync_deserializes_completed_true_response()
    {
        const string RedemptionId = "69a595406f7bf684bcf63750";
        var fixtureJson = File.ReadAllText(
            Path.Combine("Fixtures", "get-redemption-completed.json")
        );

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
                    .WithBody(fixtureJson)
            );

        var response = await _api.GetRedemptionAsync(ChannelId, RedemptionId, "Bearer test-token");

        response.Id.Should().Be(RedemptionId);
        response.Channel.Should().Be(ChannelId);
        response.Completed.Should().BeTrue();
        response.Rejected.Should().BeFalse();
        response.Input.Should().BeEquivalentTo(["test@email.com", "John Doe"]);
        response.UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-04-15T10:00:00.000Z"));
    }

    /// <summary>
    /// The lookup expands <c>item</c> into an object; the redeem call answers with a bare id
    /// string. Reading the lookup through the redeem contract throws on every poll, so pin the
    /// nested shape rather than the flattened id.
    /// </summary>
    [Fact]
    public async Task GetRedemptionAsync_expands_the_item_into_an_object()
    {
        const string RedemptionId = "69a595406f7bf684bcf63750";
        var fixtureJson = File.ReadAllText(
            Path.Combine("Fixtures", "get-redemption-completed.json")
        );

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
                    .WithBody(fixtureJson)
            );

        var response = await _api.GetRedemptionAsync(ChannelId, RedemptionId, "Bearer test-token");

        response.Item.Id.Should().Be(ItemId);
        response.Item.Name.Should().Be("AK-47 | Slate");
        response.Item.UserInput.Should().BeEquivalentTo(["Trade link", "Confirma?"]);
        response.Redeemer.Should().NotBeNull();
        response.Redeemer!.Username.Should().Be("luanholiv");
    }

    /// <summary>
    /// A refusal upstream arrives as <c>rejected: true</c> alongside <c>completed: false</c>. Read
    /// through a contract that ignores the flag, it is indistinguishable from work still pending.
    /// </summary>
    [Fact]
    public async Task GetRedemptionAsync_deserializes_a_rejected_response()
    {
        const string RedemptionId = "69a595406f7bf684bcf63750";
        var fixtureJson = File.ReadAllText(
            Path.Combine("Fixtures", "get-redemption-rejected.json")
        );

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
                    .WithBody(fixtureJson)
            );

        var response = await _api.GetRedemptionAsync(ChannelId, RedemptionId, "Bearer test-token");

        response.Completed.Should().BeFalse();
        response.Rejected.Should().BeTrue();
    }

    [Fact]
    public async Task GetRedemptionAsync_deserializes_completed_false_response()
    {
        const string RedemptionId = "69a595406f7bf684bcf63750";
        var fixtureJson = File.ReadAllText(Path.Combine("Fixtures", "get-redemption-pending.json"));

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
                    .WithBody(fixtureJson)
            );

        var response = await _api.GetRedemptionAsync(ChannelId, RedemptionId, "Bearer test-token");

        response.Id.Should().Be(RedemptionId);
        response.Completed.Should().BeFalse();
        response.UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-03-02T13:48:48.043Z"));
    }
}
