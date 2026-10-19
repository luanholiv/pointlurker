using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;
using Refit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PointLurker.Infrastructure.ExternalServices.ContractTests.StreamElementsApi;

[Trait("Category", "Contract")]
public sealed class StoreItemsContractTests : IAsyncLifetime
{
    private WireMockServer _server = null!;
    private IStreamElementsApi _api = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();

        var fixtureJson = File.ReadAllText(Path.Combine("Fixtures", "store-items-response.json"));

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
    public async Task Deserializes_required_string_fields()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items.Should().HaveCount(4);

        items[0].Id.Should().Be("5ccdd5c2087e147306568b24");
        items[0].Channel.Should().Be("5cc799026e852d26fcf16717");
        items[0].Name.Should().Be("AWP | Redline");
        items[0].Type.Should().Be("perk");

        items[1].Id.Should().Be("5cf19592f031df37b0c39d01");
        items[1].Channel.Should().Be("5cc799026e852d26fcf16717");
        items[1].Name.Should().Be("Mensagem");
        items[1].Type.Should().Be("perk");

        items[2].Id.Should().Be("5cdf4ded5078407eb40c5d78");
        items[2].Channel.Should().Be("5cc799026e852d26fcf16717");
        items[2].Name.Should().Be("Twitch Sub");
        items[2].Type.Should().Be("perk");

        items[3].Id.Should().Be("5d44f0261707f0c96776020d");
        items[3].Channel.Should().Be("5cc799026e852d26fcf16717");
        items[3].Name.Should().Be("Assinatura Steam");
        items[3].Type.Should().Be("perk");
    }

    [Fact]
    public async Task Deserializes_nullable_string_fields()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items[0]
            .Description.Should()
            .Be(
                "Estado: Field Tested -- FAVOR DEIXAR O INVENTARIO PUBLICO E NAO ALTERAR O TRADE LINK APOS EFETUAR A COMPRA"
            );
        items[0].UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-02-05T20:21:39.441Z"));

        items[1].Description.Should().Contain("Mande uma mensagem pro Gaules!");
        items[1].UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-04-02T18:11:19.416Z"));
    }

    [Fact]
    public async Task Deserializes_boolean_fields()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        // Item 0: AWP | Redline — disabled, not featured, not public, not subscriber-only
        // Note: allowMessages is MISSING from this item's JSON — should default to false
        items[0].Enabled.Should().BeFalse();
        items[0].Featured.Should().BeFalse();
        items[0].Public.Should().BeFalse();
        items[0].SubscriberOnly.Should().BeFalse();
        items[0].AllowMessages.Should().BeFalse();

        // Item 1: Mensagem — enabled, allowMessages=true
        items[1].Enabled.Should().BeTrue();
        items[1].Featured.Should().BeFalse();
        items[1].Public.Should().BeFalse();
        items[1].SubscriberOnly.Should().BeFalse();
        items[1].AllowMessages.Should().BeTrue();
    }

    [Fact]
    public async Task Deserializes_numeric_fields()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items[0].Cost.Should().Be(270_000);
        items[1].Cost.Should().Be(5_000);
        items[2].Cost.Should().Be(25_000);
        items[3].Cost.Should().Be(15_000);
    }

    [Fact]
    public async Task Deserializes_nested_quantity()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items[0].Quantity.Total.Should().Be(1);
        items[0].Quantity.Current.Should().Be(0);

        items[1].Quantity.Total.Should().Be(-1);
        items[1].Quantity.Current.Should().Be(-1);

        items[2].Quantity.Total.Should().Be(3);
        items[2].Quantity.Current.Should().Be(0);

        items[3].Quantity.Total.Should().Be(110);
        items[3].Quantity.Current.Should().Be(73);
    }

    [Fact]
    public async Task Deserializes_nested_cooldown()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items[0].Cooldown.User.Should().Be(36_000);
        items[0].Cooldown.Global.Should().Be(60);
        items[0].Cooldown.Category.Should().Be(2_592_000);

        items[1].Cooldown.User.Should().Be(900);
        items[1].Cooldown.Global.Should().Be(60);
        items[1].Cooldown.Category.Should().Be(0);

        items[2].Cooldown.User.Should().Be(50_000);
        items[2].Cooldown.Global.Should().Be(100);
        items[2].Cooldown.Category.Should().Be(432_000);
    }

    [Fact]
    public async Task Deserializes_collection_fields()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items[0].UserInput.Should().BeEquivalentTo(["Steam Trade Link", "E-mail"]);
        items[0].Sources.Should().BeEquivalentTo(["website"]);

        items[1].UserInput.Should().BeEmpty();
        items[1].Sources.Should().BeEquivalentTo(["website", "extension"]);

        items[2].UserInput.Should().BeEquivalentTo(["Nick Twitch"]);
        items[2].Sources.Should().BeEquivalentTo(["website"]);

        items[3].UserInput.Should().BeEquivalentTo(["Link Steam (Deixe o perfil desbloqueado)"]);
    }

    [Fact]
    public async Task Deserializes_datetime_fields()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items[0].CreatedAt.Should().Be(DateTimeOffset.Parse("2019-05-04T18:11:14.661Z"));
        items[1].CreatedAt.Should().Be(DateTimeOffset.Parse("2019-05-31T20:58:58.510Z"));
        items[2].CreatedAt.Should().Be(DateTimeOffset.Parse("2019-05-18T00:12:29.446Z"));
        items[3].CreatedAt.Should().Be(DateTimeOffset.Parse("2019-08-03T02:23:34.085Z"));
    }

    [Fact]
    public async Task Deserializes_image_url_fields()
    {
        var items = await _api.GetStoreItemsAsync("test-channel");

        items[0]
            .Thumbnail.Should()
            .Be("https://cdn.streamelements.com/uploads/f57c97ca-488c-4e34-883f-18b264ed7428.png");
        items[0]
            .Preview.Should()
            .Be("https://cdn.streamelements.com/uploads/3f202956-177a-41e2-a171-632cf43de496.png");

        items[1]
            .Thumbnail.Should()
            .Be("https://cdn.streamelements.com/uploads/89e82840-d76d-45d6-8f8c-9bdf6a8f0639.jpg");
        items[1]
            .Preview.Should()
            .Be("https://cdn.streamelements.com/uploads/0c6833ad-2fea-4618-bb76-b7e4bc1ea771.gif");
    }
}
