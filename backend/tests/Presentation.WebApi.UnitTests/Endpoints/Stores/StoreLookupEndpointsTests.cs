using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Presentation.WebApi.Endpoints.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores;

/// <summary>
/// The handler's own answers only. A provider that does not parse, or a missing StreamElements
/// query, never reaches here — that is <c>StoreLookupQueryValidator</c>'s rule, refused by the
/// endpoint filter before the handler runs.
/// </summary>
public sealed class StoreLookupEndpointsTests
{
    private readonly IStoreProviderClient _client = Substitute.For<IStoreProviderClient>();
    private readonly IStoreProviderClientFactory _providers;

    public StoreLookupEndpointsTests()
    {
        _providers = StoreProviderClients.FactoryFor(_client);
    }

    [Fact]
    public async Task Maps_the_lookup_onto_the_response()
    {
        _client
            .LookupStoreAsync("xqc", Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("channel-id-123"),
                    "xqc",
                    "xQc",
                    "https://cdn.example.com/a.png",
                    ItemCount: 3
                )
            );

        var result = await StoreLookupEndpoints.LookupStoreAsync(
            new StoreLookupQuery("streamelements", "xqc"),
            _providers,
            CancellationToken.None
        );

        var ok = result.Should().BeOfType<Ok<StoreLookupResponse>>().Subject;
        ok.Value.Should()
            .Be(
                new StoreLookupResponse(
                    "channel-id-123",
                    "xqc",
                    "xQc",
                    "https://cdn.example.com/a.png",
                    3
                )
            );
    }

    /// <summary>
    /// The validator refuses a blank query but not a padded one, so the handler keeps trimming.
    /// </summary>
    [Fact]
    public async Task Trims_the_query_before_asking_the_provider()
    {
        _client
            .LookupStoreAsync("xqc", Arg.Any<CancellationToken>())
            .Returns(new ProviderStoreLookup(new StoreExternalId("id"), "xqc", "xQc"));

        await StoreLookupEndpoints.LookupStoreAsync(
            new StoreLookupQuery("streamelements", "  xqc  "),
            _providers,
            CancellationToken.None
        );

        await _client.Received(1).LookupStoreAsync("xqc", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Asks_the_provider_the_query_names()
    {
        _client
            .LookupStoreAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(new StoreExternalId("gaupoints"), "gaupoints", "GauPoints")
            );

        await StoreLookupEndpoints.LookupStoreAsync(
            new StoreLookupQuery("gaupoints", null),
            _providers,
            CancellationToken.None
        );

        _providers.Received(1).For(StoreProvider.GauPoints);
    }

    [Fact]
    public async Task A_provider_404_answers_not_found_with_provider_and_query()
    {
        _client
            .LookupStoreAsync("nobody", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(404, "Not Found", "Not found"));

        var result = await StoreLookupEndpoints.LookupStoreAsync(
            new StoreLookupQuery("streamelements", "nobody"),
            _providers,
            CancellationToken.None
        );

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Extensions["code"].Should().Be(ErrorCodes.StoreLookup.NotFound);
        var p = problem.ProblemDetails.Extensions["params"] as IReadOnlyDictionary<string, object?>;
        p.Should().ContainKey("provider").WhoseValue.Should().Be("streamelements");
        p.Should().ContainKey("query").WhoseValue.Should().Be("nobody");
    }

    [Theory]
    [InlineData(ProviderApiException.NoResponse, ErrorCodes.ProviderApi.Unreachable)]
    [InlineData(500, ErrorCodes.ProviderApi.FetchFailed)]
    public async Task Any_other_provider_failure_answers_502(int status, string code)
    {
        _client
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(status, null, "boom"));

        var result = await StoreLookupEndpoints.LookupStoreAsync(
            new StoreLookupQuery("gaupoints", null),
            _providers,
            CancellationToken.None
        );

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        problem.ProblemDetails.Extensions["code"].Should().Be(code);
    }
}
