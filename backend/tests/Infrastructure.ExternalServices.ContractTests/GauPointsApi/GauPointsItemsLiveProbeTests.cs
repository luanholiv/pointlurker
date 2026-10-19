using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;
using Refit;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.GauPointsApi;

/// <summary>
/// Reads the live, public catalogue. No credential, no writes: safe on the weekly schedule, and the
/// only thing that notices an undocumented API changing shape.
/// </summary>
[Trait("Category", "LiveProbe")]
public sealed class GauPointsItemsLiveProbeTests
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("GAUPOINTS_PROBE_BASE_URL")
        ?? "https://gaupoints.com.br";

    private readonly IGauPointsApi _api = RestService.For<IGauPointsApi>(BaseUrl);

    [Fact]
    public async Task The_catalogue_deserializes_and_is_not_empty()
    {
        var response = await _api.GetItemsAsync();

        response.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Every_item_has_its_required_fields()
    {
        var response = await _api.GetItemsAsync();

        foreach (var item in response.Items)
        {
            item.Id.Should().NotBeNullOrEmpty();
            item.Name.Should().NotBeNullOrEmpty();
            item.Cost.Should().BePositive();
        }
    }

    /// <summary>
    /// The input form is chosen by type, and an unknown type silently asks for an address. A new
    /// type upstream should surface here, not as a store that quietly wants more than it needs.
    /// </summary>
    [Fact]
    public async Task Every_item_has_a_known_type()
    {
        var response = await _api.GetItemsAsync();

        response
            .Items.Select(item => item.Type)
            .Should()
            .OnlyContain(type => type == "digital" || type == "physical");
    }
}
