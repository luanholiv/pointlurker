using System.Buffers.Text;
using System.Text;
using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;

namespace Infrastructure.ExternalServices.UnitTests.GauPointsApi;

public sealed class GauPointsSessionTests
{
    private static string Jwt(string payloadJson) =>
        $"{Segment("""{"alg":"HS256","typ":"JWT"}""")}.{Segment(payloadJson)}.signature";

    private static string Segment(string json) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Reads_the_expiry_and_the_username_from_the_payload()
    {
        var session = Jwt(
            """{"userId":1,"username":"someviewer","iat":1790000000,"exp":1790604800}"""
        );

        GauPointsSession
            .Describe(session)
            .Should()
            .Be(
                new ProviderCredentialHints(
                    DateTimeOffset.FromUnixTimeSeconds(1790604800),
                    "someviewer"
                )
            );
    }

    [Fact]
    public void A_payload_without_the_claims_describes_nothing()
    {
        GauPointsSession.Describe(Jwt("""{"sub":"x"}""")).Should().Be(ProviderCredentialHints.None);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.b")]
    [InlineData("a.!!!.c")]
    [InlineData("")]
    public void A_value_that_is_not_a_JWT_describes_nothing(string session)
    {
        GauPointsSession.Describe(session).Should().Be(ProviderCredentialHints.None);
    }

    [Fact]
    public void A_payload_that_is_not_JSON_describes_nothing()
    {
        GauPointsSession
            .Describe($"x.{Segment("not json")}.y")
            .Should()
            .Be(ProviderCredentialHints.None);
    }

    [Fact]
    public void An_expiry_of_the_wrong_type_is_ignored()
    {
        GauPointsSession
            .Describe(Jwt("""{"username":"someviewer","exp":"soon"}"""))
            .Should()
            .Be(new ProviderCredentialHints(null, "someviewer"));
    }
}
