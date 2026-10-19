using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Identity;

public sealed class ProviderSecretsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly InMemoryProviderCredentials _credentials = new();
    private readonly ISecretProtector _streamElements = Substitute.For<ISecretProtector>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly ProviderSecrets _sut;

    public ProviderSecretsTests()
    {
        _sut = new ProviderSecrets(
            _credentials,
            new ProviderSecretProtectors(_streamElements, Substitute.For<ISecretProtector>()),
            _timeProvider
        );
    }

    [Fact]
    public async Task Resolves_a_stored_credential_to_its_decrypted_secret()
    {
        var credential = new ProviderCredential(
            UserId,
            StoreProvider.StreamElements,
            "ciphertext",
            Now
        );
        _credentials.With(credential);
        _streamElements.Unprotect("ciphertext").Returns("plain");

        var resolution = await _sut.ResolveAsync(UserId, StoreProvider.StreamElements);

        resolution.Should().Be(new ProviderSecretResolution.Resolved(credential, "plain"));
    }

    [Fact]
    public async Task Reports_a_provider_the_owner_holds_no_credential_for()
    {
        var resolution = await _sut.ResolveAsync(UserId, StoreProvider.StreamElements);

        resolution.Should().BeOfType<ProviderSecretResolution.NotConfigured>();
    }

    [Fact]
    public async Task Reports_a_provider_that_cannot_hold_a_credential_without_decrypting_anything()
    {
        var unknown = (StoreProvider)99;
        _credentials.With(new ProviderCredential(UserId, unknown, "ciphertext", Now));

        var resolution = await _sut.ResolveAsync(UserId, unknown);

        resolution.Should().BeOfType<ProviderSecretResolution.NotConfigured>();
        _streamElements.DidNotReceive().Unprotect(Arg.Any<string>());
    }

    /// <summary>
    /// The secret's own expiry is final for this pass: nothing sent with it could succeed, so it
    /// is not even decrypted.
    /// </summary>
    [Fact]
    public async Task Reports_a_credential_whose_own_expiry_has_passed_without_decrypting_it()
    {
        var credential = new ProviderCredential(
            UserId,
            StoreProvider.StreamElements,
            "ciphertext",
            Now.AddDays(-7)
        );
        credential.SetExpiresAt(Now);
        _credentials.With(credential);

        var resolution = await _sut.ResolveAsync(UserId, StoreProvider.StreamElements);

        resolution.Should().Be(new ProviderSecretResolution.Expired(credential));
        _streamElements.DidNotReceive().Unprotect(Arg.Any<string>());
    }

    [Fact]
    public async Task Resolves_a_credential_whose_expiry_is_still_ahead()
    {
        var credential = new ProviderCredential(
            UserId,
            StoreProvider.StreamElements,
            "ciphertext",
            Now.AddDays(-1)
        );
        credential.SetExpiresAt(Now.AddSeconds(1));
        _credentials.With(credential);
        _streamElements.Unprotect("ciphertext").Returns("plain");

        var resolution = await _sut.ResolveAsync(UserId, StoreProvider.StreamElements);

        resolution.Should().BeOfType<ProviderSecretResolution.Resolved>();
    }

    [Fact]
    public async Task Reports_a_ciphertext_that_will_not_decrypt()
    {
        _credentials.With(
            new ProviderCredential(UserId, StoreProvider.StreamElements, "ciphertext", Now)
        );
        var fault = new InvalidOperationException("key ring lost");
        _streamElements.Unprotect("ciphertext").Throws(fault);

        var resolution = await _sut.ResolveAsync(UserId, StoreProvider.StreamElements);

        resolution
            .Should()
            .BeOfType<ProviderSecretResolution.DecryptionFailed>()
            .Which.Exception.Should()
            .BeSameAs(fault);
    }
}
