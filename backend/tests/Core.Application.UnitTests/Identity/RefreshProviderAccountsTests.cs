using Core.Application.UnitTests.Stores;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Identity;

public sealed class RefreshProviderAccountsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly InMemoryProviderCredentials _credentials = new();
    private readonly ISecretProtector _encryption = Substitute.For<ISecretProtector>();
    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private readonly ApplicationUser _user = new() { Id = Guid.NewGuid() };
    private readonly RefreshProviderAccounts _sut;

    public RefreshProviderAccountsTests()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(_user);
        _encryption.Unprotect("encrypted").Returns("plain-token");

        _sut = new RefreshProviderAccounts(
            _userProvider,
            _credentials,
            new ProviderSecretProtectors(_encryption, Substitute.For<ISecretProtector>()),
            StoreProviderClients.FactoryFor(_seApi),
            new FakeTimeProvider(Now),
            NullLogger<RefreshProviderAccounts>.Instance
        );
    }

    private ProviderCredential HoldStreamElementsCredential(string? accountName = null)
    {
        var credential = new ProviderCredential(
            _user.Id,
            StoreProvider.StreamElements,
            "encrypted",
            Now.AddDays(-7)
        );
        if (accountName is not null)
            credential.SetAccountName(accountName, Now.AddDays(-1));

        _credentials.With(credential);
        return credential;
    }

    [Fact]
    public async Task Persists_the_account_name_and_confirms_the_credential()
    {
        var credential = HoldStreamElementsCredential();
        _seApi
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .Returns("luanholiv");

        await _sut.ExecuteAsync();

        credential.AccountName.Should().Be("luanholiv");
        credential.AccountNameRefreshedAt.Should().Be(Now);
        credential.LastValidatedAt.Should().Be(Now);
        await _userProvider.Received(1).SaveAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_op_when_no_credential_is_held()
    {
        await _sut.ExecuteAsync();

        await _seApi
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _userProvider
            .DidNotReceive()
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_op_when_no_user_exists()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        await _sut.ExecuteAsync();

        await _seApi
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task A_refused_credential_is_marked_and_keeps_its_account_name(int status)
    {
        var credential = HoldStreamElementsCredential("old-username");
        _seApi
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(status, "Unauthorized", "Unauthorized"));

        await _sut.ExecuteAsync();

        credential.LastValidationError.Should().Be(ErrorCodes.Credentials.Rejected);
        credential.AccountName.Should().Be("old-username");
        await _userProvider.Received(1).SaveAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failure_that_says_nothing_about_the_credential_changes_nothing()
    {
        var credential = HoldStreamElementsCredential("old-username");
        _seApi
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(503, "Service Unavailable", "down"));

        await _sut.ExecuteAsync();

        credential.AccountName.Should().Be("old-username");
        credential.LastValidationError.Should().BeNull();
        await _userProvider
            .DidNotReceive()
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retains_the_account_name_when_decryption_fails()
    {
        var credential = HoldStreamElementsCredential("old-username");
        _encryption.Unprotect("encrypted").Returns(_ => throw new InvalidOperationException("bad"));

        await _sut.ExecuteAsync();

        credential.AccountName.Should().Be("old-username");
        await _seApi
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_credential_for_a_provider_with_no_protector_is_skipped()
    {
        _credentials.With(new ProviderCredential(_user.Id, (StoreProvider)99, "encrypted", Now));

        await _sut.ExecuteAsync();

        await _seApi
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
