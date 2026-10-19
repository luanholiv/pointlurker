using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using Refit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.StreamElementsApi;

[Trait("Category", "Contract")]
public sealed class StoreItemsEdgeCaseTests : IAsyncLifetime
{
    private WireMockServer _server = null!;
    private IStreamElementsApi _api = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        _api = RestService.For<IStreamElementsApi>(_server.Url!);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    private void StubResponse(string json)
    {
        _server
            .Given(
                Request
                    .Create()
                    .WithPath("/kappa/v2/store/*/items")
                    .WithParam("source", "website")
                    .UsingGet()
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(json)
            );
    }

    [Fact]
    public async Task Missing_allowMessages_defaults_to_false()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-missing-allow-messages");
        item.AllowMessages.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_categoryName_defaults_to_empty_string()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-missing-category-name");
        item.CategoryName.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlimited_quantity_deserializes_as_negative_one()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-unlimited-quantity");
        item.Quantity.Total.Should().Be(-1);
        item.Quantity.Current.Should().Be(-1);
    }

    [Fact]
    public async Task Out_of_stock_quantity_deserializes_as_zero()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-out-of-stock");
        item.Quantity.Total.Should().Be(10);
        item.Quantity.Current.Should().Be(0);
    }

    [Fact]
    public async Task Null_description_deserializes_as_null()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-out-of-stock");
        item.Description.Should().BeNull();
    }

    [Fact]
    public async Task Unknown_fields_do_not_break_deserialization()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-unknown-fields");
        item.Name.Should().Be("Item With Unknown Fields");
        item.Cost.Should().Be(300);
        item.Quantity.Total.Should().Be(1);
        item.Quantity.Current.Should().Be(1);
    }

    [Fact]
    public async Task Empty_array_deserializes_to_empty_list()
    {
        StubResponse("[]");

        var items = await _api.GetStoreItemsAsync("test-channel");

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task Image_url_fields_parsed_when_present()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-unknown-fields");
        item.Thumbnail.Should().Be("https://example.com/thumb.png");
        item.Preview.Should().Be("https://example.com/preview.png");
    }

    [Fact]
    public async Task Missing_image_fields_default_to_null()
    {
        var fixtureJson = await File.ReadAllTextAsync(
            Path.Combine("Fixtures", "store-items-edge-cases.json")
        );
        StubResponse(fixtureJson);

        var items = await _api.GetStoreItemsAsync("test-channel");

        var item = items.Single(i => i.Id == "edge-out-of-stock");
        item.Thumbnail.Should().BeNull();
        item.Preview.Should().BeNull();
    }
}
