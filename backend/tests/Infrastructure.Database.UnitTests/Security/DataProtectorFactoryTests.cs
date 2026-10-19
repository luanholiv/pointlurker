using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using PointLurker.Core.Application.Security;
using PointLurker.Infrastructure.Database.Security;

namespace Infrastructure.Database.UnitTests.Security;

public sealed class DataProtectorFactoryTests
{
    private readonly DataProtectorFactory _factory = new(DataProtectionProvider.Create("TestApp"));

    [Fact]
    public void Protect_then_Unprotect_round_trips_plaintext()
    {
        var sut = _factory.Create(ProtectionPurposes.RedemptionMessage);

        var ciphertext = sut.Protect("thanks for the sub!");
        var plaintext = sut.Unprotect(ciphertext);

        plaintext.Should().Be("thanks for the sub!");
    }

    [Fact]
    public void Ciphertext_differs_from_plaintext()
    {
        var sut = _factory.Create(ProtectionPurposes.RedemptionMessage);

        var ciphertext = sut.Protect("GG");

        ciphertext.Should().NotBe("GG");
    }

    [Fact]
    public void Ciphertext_from_one_purpose_cannot_be_unprotected_under_another()
    {
        var message = _factory.Create(ProtectionPurposes.RedemptionMessage);
        var token = _factory.Create(ProtectionPurposes.StreamElementsToken);

        var ciphertext = message.Protect("secret");

        var act = () => token.Unprotect(ciphertext);

        act.Should().Throw<Exception>();
    }
}
