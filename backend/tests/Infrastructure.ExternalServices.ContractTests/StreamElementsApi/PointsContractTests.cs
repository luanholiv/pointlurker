using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using Refit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.StreamElementsApi;

[Trait("Category", "Contract")]
public sealed class PointsContractTests : IAsyncLifetime
{
    private const string ChannelId = "5cc799026e852d26fcf16717";
    private const string Username = "luanholiv";

    private WireMockServer _server = null!;
    private IStreamElementsApi _api = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();

        _server
            .Given(Request.Create().WithPath("/kappa/v2/users/current").UsingGet())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(
                        File.ReadAllText(Path.Combine("Fixtures", "current-user-response.json"))
                    )
            );

        _server
            .Given(Request.Create().WithPath($"/kappa/v2/points/{ChannelId}/{Username}").UsingGet())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(File.ReadAllText(Path.Combine("Fixtures", "points-response.json")))
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
    public async Task GetCurrentUserAsync_deserializes_username()
    {
        var response = await _api.GetCurrentUserAsync("Bearer test-token");

        response.Username.Should().Be("luanholiv");
    }

    [Fact]
    public async Task GetPointsAsync_deserializes_points()
    {
        var response = await _api.GetPointsAsync(ChannelId, Username, "Bearer test-token");

        response.Points.Should().Be(249406);
    }

    [Fact]
    public async Task Bearer_token_is_forwarded_on_current_user()
    {
        await _api.GetCurrentUserAsync("Bearer test-token");

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
    public async Task Bearer_token_is_forwarded_on_points()
    {
        await _api.GetPointsAsync(ChannelId, Username, "Bearer test-token");

        var logEntry = _server.LogEntries.Should().ContainSingle().Subject;
        logEntry.RequestMessage!.Headers.Should().ContainKey("Authorization");
        logEntry
            .RequestMessage.Headers!["Authorization"]
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Bearer");
    }
}
