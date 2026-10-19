using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Identity;

public sealed class ClearProviderCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly InMemoryProviderCredentials _credentials = new();
    private readonly ApplicationUser _user = new() { Id = Guid.NewGuid() };
    private readonly ClearProviderCredential _sut;

    public ClearProviderCredentialTests()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(_user);
        _sut = new ClearProviderCredential(_userProvider, _credentials);
    }

    [Fact]
    public async Task Removes_the_credential()
    {
        _credentials.With(
            new ProviderCredential(_user.Id, StoreProvider.StreamElements, "ciphertext", Now)
        );

        var result = await _sut.ExecuteAsync(StoreProvider.StreamElements);

        result.IsSuccess.Should().BeTrue();
        _credentials.Held.Should().BeEmpty();
        await _userProvider.Received(1).SaveAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Clearing_a_credential_that_is_not_there_succeeds()
    {
        var result = await _sut.ExecuteAsync(StoreProvider.StreamElements);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_provider_that_holds_no_credential_is_refused()
    {
        var result = await _sut.ExecuteAsync((StoreProvider)99);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Credentials.ProviderUnsupported);
        await _userProvider
            .DidNotReceive()
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_there_is_no_user()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.ExecuteAsync(StoreProvider.StreamElements);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Users.NotFound);
    }
}
