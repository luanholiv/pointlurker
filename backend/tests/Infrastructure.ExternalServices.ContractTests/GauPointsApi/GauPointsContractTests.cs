using System.Net;
using System.Text.Json;
using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;
using Refit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.GauPointsApi;

/// <summary>
/// Pins the recorded GauPoints shapes: the catalogue, both session states, and the two observed
/// refusal envelopes. These cannot discover a shape; they prove the contracts read the known ones.
/// </summary>
[Trait("Category", "Contract")]
public sealed class GauPointsContractTests : IAsyncLifetime
{
    private WireMockServer _server = null!;
    private IGauPointsApi _api = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        _api = RestService.For<IGauPointsApi>(_server.Url!);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    private void Stub(string path, string method, int status, string fixture) =>
        _server
            .Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(status)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(File.ReadAllText(Path.Combine("Fixtures", fixture)))
            );

    [Fact]
    public async Task Items_deserialize_every_recorded_field()
    {
        Stub("/api/items", "GET", 200, "gaupoints-items-response.json");

        var response = await _api.GetItemsAsync();

        response.Items.Should().HaveCount(2);
        var netflix = response.Items[0];
        netflix.Id.Should().Be("netflix");
        netflix.Name.Should().Be("Gift Card Netflix");
        netflix.Cost.Should().Be(8000);
        netflix.Type.Should().Be("digital");
        netflix.Stock.Should().Be(9);
        netflix.Image.Should().StartWith("https://");
        response.Items[1].Type.Should().Be("physical");
        response.Items[1].Stock.Should().Be(0);
    }

    [Fact]
    public async Task An_item_missing_its_optional_fields_still_deserializes()
    {
        _server
            .Given(Request.Create().WithPath("/api/items").UsingGet())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"items":[{"id":"x","name":"X","cost":1,"type":"digital"}]}""")
            );

        var response = await _api.GetItemsAsync();

        var item = response.Items.Should().ContainSingle().Subject;
        item.Stock.Should().BeNull();
        item.Image.Should().BeNull();
    }

    [Fact]
    public async Task A_signed_in_session_reads_the_account_and_the_balance()
    {
        Stub("/api/me", "GET", 200, "gaupoints-me-signed-in.json");

        var me = await _api.GetMeAsync("kp_session=session");

        me.Logged.Should().BeTrue();
        me.Points.Should().Be(1234);
        me.User!.Username.Should().Be("someviewer");
        me.User.Name.Should().Be("Some Viewer");
    }

    [Fact]
    public async Task A_signed_out_session_answers_200_with_logged_false_alone()
    {
        Stub("/api/me", "GET", 200, "gaupoints-me-signed-out.json");

        var me = await _api.GetMeAsync("kp_session=stale");

        me.Logged.Should().BeFalse();
        me.Points.Should().BeNull();
        me.User.Should().BeNull();
    }

    [Theory]
    [InlineData(401, "gaupoints-redeem-unauthenticated.json", "Você precisa entrar com o Kick.")]
    [InlineData(400, "gaupoints-redeem-invalid-item.json", "Item inválido.")]
    public async Task A_refusal_carries_the_ok_false_envelope_on_its_error_status(
        int status,
        string fixture,
        string error
    )
    {
        Stub("/api/redeem", "POST", status, fixture);

        var response = await _api.RedeemAsync(
            new GauPointsRedeemRequest { ItemId = "netflix", Data = new GauPointsRedeemData() },
            "kp_session=session",
            "key"
        );

        response.StatusCode.Should().Be((HttpStatusCode)status);
        response.HasResponseError(out var apiError).Should().BeTrue();
        var envelope = JsonSerializer.Deserialize<GauPointsRedeemResponse>(apiError!.Content!)!;
        envelope.Ok.Should().BeFalse();
        envelope.Error.Should().Be(error);
    }
}
