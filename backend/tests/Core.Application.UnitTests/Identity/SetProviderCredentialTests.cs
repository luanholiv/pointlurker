using System.Threading.Channels;
using Core.Application.UnitTests.Stores;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Identity;

public sealed class SetProviderCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly InMemoryProviderCredentials _credentials = new();
    private readonly ISecretProtector _protector = Substitute.For<ISecretProtector>();
    private readonly IStoreProviderClient _client = Substitute.For<IStoreProviderClient>();
    private readonly Channel<UsernameRefreshSignal> _refreshChannel =
        Channel.CreateUnbounded<UsernameRefreshSignal>();
    private readonly ApplicationUser _user = new() { Id = Guid.NewGuid() };
    private readonly SetProviderCredential _sut;

    public SetProviderCredentialTests()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(_user);
        _protector.Protect("plain-token").Returns("encrypted-token");
        _client.GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>()).Returns("owner");

        _sut = new SetProviderCredential(
            _userProvider,
            _credentials,
            new ProviderSecretProtectors(_protector, Substitute.For<ISecretProtector>()),
            StoreProviderClients.FactoryFor(_client),
            _refreshChannel.Writer,
            new FakeTimeProvider(Now),
            NullLogger<SetProviderCredential>.Instance
        );
    }

    private Task<Result<Unit, IdentityError>> SetAsync() =>
        _sut.ExecuteAsync(StoreProvider.StreamElements, "plain-token");

    [Fact]
    public async Task A_credential_the_provider_accepts_is_stored_encrypted_and_confirmed()
    {
        var result = await SetAsync();

        result.IsSuccess.Should().BeTrue();
        var credential = _credentials.Held.Should().ContainSingle().Subject;
        credential.UserId.Should().Be(_user.Id);
        credential.Provider.Should().Be(StoreProvider.StreamElements);
        credential.EncryptedSecret.Should().Be("encrypted-token");
        credential.SecretSetAt.Should().Be(Now);
        credential.AccountName.Should().Be("owner");
        credential.LastValidatedAt.Should().Be(Now);
        await _userProvider.Received(1).SaveAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_credential_whose_own_expiry_has_passed_is_refused_without_asking_the_provider()
    {
        _client
            .DescribeCredential("plain-token")
            .Returns(new ProviderCredentialHints(Now, "owner"));

        var result = await SetAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Credentials.Expired);
        result.Error.Params.Should().Contain("provider", "streamelements");
        _credentials.Held.Should().BeEmpty();
        await _client
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_expiry_the_credential_announces_is_stored_with_it()
    {
        _client
            .DescribeCredential("plain-token")
            .Returns(new ProviderCredentialHints(Now.AddDays(7), "from-token"));

        await SetAsync();

        var credential = _credentials.Held.Should().ContainSingle().Subject;
        credential.ExpiresAt.Should().Be(Now.AddDays(7));
        credential
            .AccountName.Should()
            .Be("owner", "the provider's answer outranks the secret's claim");
    }

    [Fact]
    public async Task The_account_name_the_credential_carries_fills_in_when_the_check_is_inconclusive()
    {
        _client
            .DescribeCredential("plain-token")
            .Returns(new ProviderCredentialHints(Now.AddDays(7), "from-token"));
        _client
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(503, "Service Unavailable", "down"));

        await SetAsync();

        var credential = _credentials.Held.Should().ContainSingle().Subject;
        credential.AccountName.Should().Be("from-token");
        credential.LastValidatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Setting_again_replaces_the_secret_rather_than_adding_a_second_credential()
    {
        var existing = new ProviderCredential(
            _user.Id,
            StoreProvider.StreamElements,
            "old-ciphertext",
            Now.AddDays(-3)
        );
        existing.MarkValidationFailed(ErrorCodes.Credentials.Rejected);
        _credentials.With(existing);

        await SetAsync();

        _credentials.Held.Should().ContainSingle().Which.Should().BeSameAs(existing);
        existing.EncryptedSecret.Should().Be("encrypted-token");
        existing.LastValidationError.Should().BeNull();
        existing.LastValidatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task A_credential_the_provider_refuses_is_not_stored(int status)
    {
        _client
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(status, "Unauthorized", "refused"));

        var result = await SetAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Credentials.Rejected);
        result.Error.Params.Should().Contain("provider", "streamelements");
        _credentials.Held.Should().BeEmpty();
        await _userProvider
            .DidNotReceive()
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
        _refreshChannel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_refused_replacement_leaves_the_stored_credential_alone()
    {
        var existing = new ProviderCredential(
            _user.Id,
            StoreProvider.StreamElements,
            "old-ciphertext",
            Now.AddDays(-3)
        );
        _credentials.With(existing);
        _client
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(401, "Unauthorized", "refused"));

        await SetAsync();

        existing.EncryptedSecret.Should().Be("old-ciphertext");
    }

    [Theory]
    [InlineData(ProviderApiException.NoResponse)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task A_credential_the_check_cannot_judge_is_stored_unconfirmed(int status)
    {
        _client
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(status, null, "no verdict"));

        var result = await SetAsync();

        result.IsSuccess.Should().BeTrue();
        var credential = _credentials.Held.Should().ContainSingle().Subject;
        credential.EncryptedSecret.Should().Be("encrypted-token");
        credential.LastValidatedAt.Should().BeNull();
        credential.AccountName.Should().BeNull();
        await _userProvider.Received(1).SaveAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Every_stored_credential_signals_an_account_refresh()
    {
        await SetAsync();

        _refreshChannel.Reader.TryRead(out var signal).Should().BeTrue();
        signal.Should().NotBeNull();
    }

    [Fact]
    public async Task A_blank_account_name_confirms_the_credential_without_naming_the_account()
    {
        _client.GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>()).Returns(" ");

        await SetAsync();

        var credential = _credentials.Held.Should().ContainSingle().Subject;
        credential.LastValidatedAt.Should().Be(Now);
        credential.AccountName.Should().BeNull();
    }

    [Fact]
    public async Task A_provider_that_holds_no_credential_is_refused_before_any_call()
    {
        var result = await _sut.ExecuteAsync((StoreProvider)99, "plain-token");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Credentials.ProviderUnsupported);
        result.Error.Params.Should().Contain("provider", "99");
        await _client
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _credentials.Held.Should().BeEmpty();
    }

    [Fact]
    public async Task Fails_when_there_is_no_user()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await SetAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Users.NotFound);
        _refreshChannel.Reader.TryRead(out _).Should().BeFalse();
        await _client
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
