using FluentAssertions;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Localization;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Users.Contracts;

public sealed class CurrentUserResponseOperatorTests
{
    [Fact]
    public void Maps_full_field_set()
    {
        var user = MinimalUser();
        user.SetLocale(SupportedLocales.PtBr);
        user.TwoFactorEnabled = true;

        var response = (CurrentUserResponse)
            new CurrentUser(user, [Credential(user, StoreProvider.StreamElements, "protected")]);

        response
            .Should()
            .BeEquivalentTo(
                new CurrentUserResponse(
                    UserId: user.Id,
                    Email: "owner@example.com",
                    Locale: SupportedLocales.PtBr,
                    TwoFactorEnabled: true,
                    Credentials:
                    [
                        new CredentialSummaryResponse("streamelements", null, null, null, null),
                    ]
                )
            );
    }

    [Fact]
    public void Reports_a_StreamElements_token_by_its_provider_and_never_the_value()
    {
        var user = MinimalUser();

        var response = (CurrentUserResponse)
            new CurrentUser(user, [Credential(user, StoreProvider.StreamElements, "super-secret")]);

        response.Credentials.Should().ContainSingle().Which.Provider.Should().Be("streamelements");
        response.ToString().Should().NotContain("super-secret");
    }

    [Fact]
    public void Maps_a_user_without_a_credential()
    {
        var response = (CurrentUserResponse)new CurrentUser(MinimalUser(), []);

        response.Credentials.Should().BeEmpty();
    }

    [Fact]
    public void Summarises_each_credential_without_its_secret()
    {
        var user = MinimalUser();
        var expiresAt = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var validatedAt = expiresAt.AddDays(-2);
        var session = Credential(user, StoreProvider.GauPoints, "cookie-ciphertext");
        session.SetExpiresAt(expiresAt);
        session.SetAccountName("someviewer", validatedAt);
        session.MarkValidated(validatedAt);

        var response = (CurrentUserResponse)new CurrentUser(user, [session]);

        response
            .Credentials.Should()
            .Equal(
                new CredentialSummaryResponse(
                    "gaupoints",
                    "someviewer",
                    expiresAt,
                    validatedAt,
                    null
                )
            );
        response.ToString().Should().NotContain("cookie-ciphertext");
    }

    [Fact]
    public void Maps_a_null_email_to_empty()
    {
        var user = MinimalUser();
        user.Email = null;

        var response = (CurrentUserResponse)new CurrentUser(user, []);

        response.Email.Should().BeEmpty();
    }

    [Fact]
    public void Defaults_the_locale_to_en_us()
    {
        var response = (CurrentUserResponse)new CurrentUser(MinimalUser(), []);

        response.Locale.Should().Be(SupportedLocales.EnUs);
    }

    [Fact]
    public void Reports_two_factor_as_disabled_when_the_user_has_not_enrolled()
    {
        var response = (CurrentUserResponse)new CurrentUser(MinimalUser(), []);

        response.TwoFactorEnabled.Should().BeFalse();
    }

    private static ProviderCredential Credential(
        ApplicationUser user,
        StoreProvider provider,
        string ciphertext
    ) => new(user.Id, provider, ciphertext, DateTimeOffset.UtcNow);

    private static ApplicationUser MinimalUser() =>
        new()
        {
            Id = Guid.NewGuid(),
            UserName = "owner@example.com",
            Email = "owner@example.com",
        };
}
