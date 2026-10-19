using System.Security.Claims;
using FluentAssertions;
using PointLurker.Presentation.WebApi.Auth;

namespace Presentation.WebApi.UnitTests.Auth;

public sealed class CurrentUserClaimsTests
{
    [Fact]
    public void GetUserId_reads_the_NameIdentifier_claim()
    {
        var expected = Guid.NewGuid();

        var principal = Authenticated(new Claim(ClaimTypes.NameIdentifier, expected.ToString()));

        principal.GetUserId().Should().Be(expected);
    }

    [Fact]
    public void GetUserId_throws_when_the_claim_is_absent()
    {
        var principal = Authenticated();

        var act = () => principal.GetUserId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetUserId_throws_when_the_claim_is_not_a_guid()
    {
        var principal = Authenticated(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        var act = () => principal.GetUserId();

        act.Should().Throw<InvalidOperationException>();
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Bearer"));
}
