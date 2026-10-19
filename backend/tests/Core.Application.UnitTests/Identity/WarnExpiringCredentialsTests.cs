using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Identity;

public sealed class WarnExpiringCredentialsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private const string Address = "owner@example.com";

    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly InMemoryProviderCredentials _credentials = new();
    private readonly IEmailNotifier _email = Substitute.For<IEmailNotifier>();
    private readonly CredentialExpiryOptions _options = new();
    private readonly ApplicationUser _user = new() { Id = Guid.NewGuid(), Email = Address };

    public WarnExpiringCredentialsTests()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(_user);
    }

    private WarnExpiringCredentials Sut() =>
        new(
            _userProvider,
            _credentials,
            _email,
            Options.Create(_options),
            new FakeTimeProvider(Now),
            NullLogger<WarnExpiringCredentials>.Instance
        );

    private ProviderCredential Hold(StoreProvider provider, DateTimeOffset? expiresAt)
    {
        var credential = new ProviderCredential(_user.Id, provider, "encrypted", Now.AddDays(-6));
        credential.SetExpiresAt(expiresAt);
        _credentials.With(credential);
        return credential;
    }

    [Fact]
    public async Task Warns_the_owner_about_a_credential_expiring_inside_the_window()
    {
        _user.SetLocale("pt-BR");
        var expiresAt = Now.AddHours(30);
        var credential = Hold(StoreProvider.GauPoints, expiresAt);

        await Sut().ExecuteAsync();

        await _email
            .Received(1)
            .SendCredentialExpiryAsync(
                Address,
                new CredentialExpiryEmailModel(
                    StoreProvider.GauPoints,
                    expiresAt,
                    HasExpired: false
                ),
                "pt-BR",
                Arg.Any<CancellationToken>()
            );
        credential.ExpiryWarningSentFor.Should().Be(expiresAt);
        await _userProvider.Received(1).SaveAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stays_quiet_about_a_credential_expiring_after_the_window()
    {
        Hold(StoreProvider.GauPoints, Now.AddHours(49));

        await Sut().ExecuteAsync();

        await _email
            .DidNotReceiveWithAnyArgs()
            .SendCredentialExpiryAsync(default!, default!, default!, default);
        await _userProvider.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task Reads_the_window_from_configuration()
    {
        _options.ExpiryWarningHours = 72;
        Hold(StoreProvider.GauPoints, Now.AddHours(71));

        await Sut().ExecuteAsync();

        await _email
            .ReceivedWithAnyArgs(1)
            .SendCredentialExpiryAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Warns_once_per_expiry_across_passes()
    {
        Hold(StoreProvider.GauPoints, Now.AddHours(30));
        var sut = Sut();

        await sut.ExecuteAsync();
        await sut.ExecuteAsync();

        await _email
            .ReceivedWithAnyArgs(1)
            .SendCredentialExpiryAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Tells_the_owner_once_about_a_credential_that_expired_unwarned()
    {
        var expiresAt = Now.AddDays(-3);
        Hold(StoreProvider.GauPoints, expiresAt);

        await Sut().ExecuteAsync();

        await _email
            .Received(1)
            .SendCredentialExpiryAsync(
                Address,
                new CredentialExpiryEmailModel(
                    StoreProvider.GauPoints,
                    expiresAt,
                    HasExpired: true
                ),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Ignores_a_credential_that_announces_no_expiry()
    {
        Hold(StoreProvider.StreamElements, expiresAt: null);

        await Sut().ExecuteAsync();

        await _email
            .DidNotReceiveWithAnyArgs()
            .SendCredentialExpiryAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task A_failed_send_leaves_the_warning_owed_and_the_other_credentials_warned()
    {
        var failing = Hold(StoreProvider.StreamElements, Now.AddHours(10));
        var healthy = Hold(StoreProvider.GauPoints, Now.AddHours(20));
        _email
            .SendCredentialExpiryAsync(
                Address,
                Arg.Is<CredentialExpiryEmailModel>(model =>
                    model.Provider == StoreProvider.StreamElements
                ),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new IOException("smtp down"));

        await Sut().ExecuteAsync();

        failing.ExpiryWarningSentFor.Should().BeNull();
        healthy.ExpiryWarningSentFor.Should().Be(Now.AddHours(20));
        await _userProvider.Received(1).SaveAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_back_to_the_user_name_when_the_account_has_no_email()
    {
        _user.Email = null;
        _user.UserName = "owner@fallback.example";
        Hold(StoreProvider.GauPoints, Now.AddHours(30));

        await Sut().ExecuteAsync();

        await _email
            .Received(1)
            .SendCredentialExpiryAsync(
                "owner@fallback.example",
                Arg.Any<CredentialExpiryEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sends_nothing_when_the_account_has_no_address()
    {
        _user.Email = null;
        _user.UserName = null;
        var credential = Hold(StoreProvider.GauPoints, Now.AddHours(30));

        await Sut().ExecuteAsync();

        await _email
            .DidNotReceiveWithAnyArgs()
            .SendCredentialExpiryAsync(default!, default!, default!, default);
        credential.ExpiryWarningSentFor.Should().BeNull();
    }

    [Fact]
    public async Task No_op_when_no_user_exists()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        await Sut().ExecuteAsync();

        await _email
            .DidNotReceiveWithAnyArgs()
            .SendCredentialExpiryAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task A_cancelled_pass_surfaces_the_cancellation()
    {
        Hold(StoreProvider.GauPoints, Now.AddHours(30));
        using var cts = new CancellationTokenSource();
        _email
            .SendCredentialExpiryAsync(
                Arg.Any<string>(),
                Arg.Any<CredentialExpiryEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                cts.Cancel();
                return Task.FromCanceled(cts.Token);
            });

        var act = () => Sut().ExecuteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
