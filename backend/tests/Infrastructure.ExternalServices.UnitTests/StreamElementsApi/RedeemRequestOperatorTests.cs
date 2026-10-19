using System.Text.Json;
using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

namespace Infrastructure.ExternalServices.UnitTests.StreamElementsApi;

public sealed class RedeemRequestOperatorTests
{
    [Fact]
    public void Maps_input_values_and_message_from_command()
    {
        var command = Command(
            [new("E-mail", "email@test.com"), new("Twitter", "twitter-handle")],
            "Hello"
        );

        var request = (RedeemRequest)command;

        request.Input.Should().Equal("email@test.com", "twitter-handle");
        request.Message.Should().Be("Hello");
    }

    [Fact]
    public void Allows_null_message()
    {
        var request = (RedeemRequest)Command([new("E-mail", "email")], message: null);

        request.Message.Should().BeNull();
    }

    [Fact]
    public void Preserves_input_order()
    {
        var request = (RedeemRequest)Command([new("A", "a"), new("B", "b"), new("C", "c")]);

        request.Input.Should().Equal("a", "b", "c");
    }

    /// <summary>
    /// StreamElements takes inputs positionally. The labels exist for providers that key their
    /// inputs, and must not change a byte of what StreamElements receives.
    /// </summary>
    [Fact]
    public void Serializes_positionally_with_no_trace_of_the_labels()
    {
        var request = (RedeemRequest)Command([new("Discord Tag", "luan#1234")], "gg");

        JsonSerializer.Serialize(request).Should().Be("""{"input":["luan#1234"],"message":"gg"}""");
    }

    private static ProviderRedeemCommand Command(
        IReadOnlyList<ProviderInputValue> input,
        string? message = null
    ) =>
        new(
            Guid.NewGuid(),
            new StoreExternalId("ch-1"),
            new ProviderItemId("item-1"),
            input,
            message
        );
}
