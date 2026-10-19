using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using Refit;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.StreamElementsApi;

[Trait("Category", "LiveProbe")]
public sealed class StoreItemsLiveProbeTests
{
    private static readonly string ChannelId =
        Environment.GetEnvironmentVariable("SE_PROBE_CHANNEL_ID") ?? "5cc799026e852d26fcf16717";

    private readonly IStreamElementsApi _api = RestService.For<IStreamElementsApi>(
        "https://api.streamelements.com"
    );

    [Fact]
    public async Task Api_returns_http_200()
    {
        var response = await _api.GetStoreItemsAsync(ChannelId);

        response.Should().NotBeNull();
    }

    [Fact]
    public async Task Response_deserializes_without_exceptions()
    {
        IReadOnlyList<StoreItemResponse> items = null!;

        var act = async () => items = await _api.GetStoreItemsAsync(ChannelId);

        await act.Should().NotThrowAsync();
        items.Should().NotBeNull();
    }

    [Fact]
    public async Task Response_collection_is_non_empty()
    {
        var items = await _api.GetStoreItemsAsync(ChannelId);

        items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Every_item_has_required_string_fields()
    {
        var items = await _api.GetStoreItemsAsync(ChannelId);

        foreach (var item in items)
        {
            item.Id.Should().NotBeNullOrEmpty();
            item.Channel.Should().NotBeNullOrEmpty();
            item.Name.Should().NotBeNullOrEmpty();
            item.Type.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Every_item_has_valid_created_at()
    {
        var items = await _api.GetStoreItemsAsync(ChannelId);

        foreach (var item in items)
        {
            item.CreatedAt.Should().NotBe(default);
        }
    }

    [Fact]
    public async Task Every_item_has_nested_quantity_and_cooldown()
    {
        var items = await _api.GetStoreItemsAsync(ChannelId);

        foreach (var item in items)
        {
            item.Quantity.Should().NotBeNull();
            item.Cooldown.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Every_item_has_non_null_collections()
    {
        var items = await _api.GetStoreItemsAsync(ChannelId);

        foreach (var item in items)
        {
            item.Sources.Should().NotBeNull();
            item.UserInput.Should().NotBeNull();
        }
    }
}
