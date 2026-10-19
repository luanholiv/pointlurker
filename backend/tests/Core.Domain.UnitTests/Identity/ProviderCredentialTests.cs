using FluentAssertions;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Domain.UnitTests.Identity;

public sealed class ProviderCredentialTests
{
    private static readonly Guid UserId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly DateTimeOffset SetAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static ProviderCredential NewCredential() =>
        new(UserId, StoreProvider.StreamElements, "ciphertext", SetAt);

    [Fact]
    public void A_credential_expires_at_its_announced_instant_and_not_before()
    {
        var credential = NewCredential();
        credential.SetExpiresAt(SetAt.AddDays(7));

        credential.IsExpiredAt(SetAt.AddDays(7).AddTicks(-1)).Should().BeFalse();
        credential.IsExpiredAt(SetAt.AddDays(7)).Should().BeTrue();
    }

    [Fact]
    public void A_credential_with_no_announced_expiry_never_reads_as_expired()
    {
        NewCredential().IsExpiredAt(DateTimeOffset.MaxValue).Should().BeFalse();
    }

    [Fact]
    public void Replacing_the_secret_forgets_the_old_secret_expiry()
    {
        var credential = NewCredential();
        credential.SetExpiresAt(SetAt.AddDays(7));

        credential.ReplaceSecret("new-ciphertext", SetAt.AddDays(8));

        credential.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void A_new_credential_carries_its_secret_and_has_never_been_validated()
    {
        var credential = NewCredential();

        credential.UserId.Should().Be(UserId);
        credential.Provider.Should().Be(StoreProvider.StreamElements);
        credential.EncryptedSecret.Should().Be("ciphertext");
        credential.SecretSetAt.Should().Be(SetAt);
        credential.AccountName.Should().BeNull();
        credential.AccountNameRefreshedAt.Should().BeNull();
        credential.LastValidatedAt.Should().BeNull();
        credential.LastValidationError.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_secret_is_refused(string secret)
    {
        var create = () =>
            new ProviderCredential(UserId, StoreProvider.StreamElements, secret, SetAt);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_credential_without_an_owner_is_refused()
    {
        var create = () =>
            new ProviderCredential(Guid.Empty, StoreProvider.StreamElements, "ciphertext", SetAt);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Replacing_the_secret_forgets_the_old_secret_validation_but_keeps_the_account_name()
    {
        var credential = NewCredential();
        credential.SetAccountName("owner", SetAt);
        credential.MarkValidationFailed("credentials.rejected");
        var replacedAt = SetAt.AddDays(1);

        credential.ReplaceSecret("new-ciphertext", replacedAt);

        credential.EncryptedSecret.Should().Be("new-ciphertext");
        credential.SecretSetAt.Should().Be(replacedAt);
        credential.LastValidatedAt.Should().BeNull();
        credential.LastValidationError.Should().BeNull();
        credential.AccountName.Should().Be("owner");
    }

    [Fact]
    public void Replacing_with_a_blank_secret_is_refused()
    {
        var credential = NewCredential();

        var replace = () => credential.ReplaceSecret(" ", SetAt);

        replace.Should().Throw<ArgumentException>();
        credential.EncryptedSecret.Should().Be("ciphertext");
    }

    [Fact]
    public void Setting_the_account_name_stamps_when_it_was_refreshed()
    {
        var credential = NewCredential();
        var refreshedAt = SetAt.AddHours(2);

        credential.SetAccountName("owner", refreshedAt);

        credential.AccountName.Should().Be("owner");
        credential.AccountNameRefreshedAt.Should().Be(refreshedAt);
    }

    [Fact]
    public void A_blank_account_name_is_refused_and_the_known_one_kept()
    {
        var credential = NewCredential();
        credential.SetAccountName("owner", SetAt);

        var set = () => credential.SetAccountName("", SetAt.AddHours(1));

        set.Should().Throw<ArgumentException>();
        credential.AccountName.Should().Be("owner");
    }

    [Fact]
    public void A_successful_validation_clears_the_last_error()
    {
        var credential = NewCredential();
        credential.MarkValidationFailed("credentials.rejected");
        var validatedAt = SetAt.AddHours(3);

        credential.MarkValidated(validatedAt);

        credential.LastValidatedAt.Should().Be(validatedAt);
        credential.LastValidationError.Should().BeNull();
    }

    [Fact]
    public void A_failed_validation_keeps_the_time_of_the_last_success()
    {
        var credential = NewCredential();
        var validatedAt = SetAt.AddHours(3);
        credential.MarkValidated(validatedAt);

        credential.MarkValidationFailed("credentials.rejected");

        credential.LastValidationError.Should().Be("credentials.rejected");
        credential.LastValidatedAt.Should().Be(validatedAt);
    }

    [Fact]
    public void A_validation_failure_needs_an_error_code()
    {
        var credential = NewCredential();

        var fail = () => credential.MarkValidationFailed(" ");

        fail.Should().Throw<ArgumentException>();
    }

    private static readonly TimeSpan Window = TimeSpan.FromDays(2);

    [Fact]
    public void A_credential_needs_a_warning_once_its_expiry_is_inside_the_window()
    {
        var credential = NewCredential();
        credential.SetExpiresAt(SetAt.AddDays(7));

        credential.NeedsExpiryWarning(SetAt.AddDays(5).AddTicks(-1), Window).Should().BeFalse();
        credential.NeedsExpiryWarning(SetAt.AddDays(5), Window).Should().BeTrue();
    }

    [Fact]
    public void An_expired_credential_that_was_never_warned_about_still_needs_a_warning()
    {
        var credential = NewCredential();
        credential.SetExpiresAt(SetAt.AddDays(7));

        credential.NeedsExpiryWarning(SetAt.AddDays(30), Window).Should().BeTrue();
    }

    [Fact]
    public void A_credential_with_no_announced_expiry_never_needs_a_warning()
    {
        NewCredential().NeedsExpiryWarning(DateTimeOffset.MaxValue, Window).Should().BeFalse();
    }

    [Fact]
    public void A_warning_is_needed_once_per_expiry()
    {
        var credential = NewCredential();
        credential.SetExpiresAt(SetAt.AddDays(7));

        credential.MarkExpiryWarningSent();

        credential.ExpiryWarningSentFor.Should().Be(SetAt.AddDays(7));
        credential.NeedsExpiryWarning(SetAt.AddDays(6), Window).Should().BeFalse();
    }

    [Fact]
    public void A_new_secret_with_a_new_expiry_needs_its_own_warning()
    {
        var credential = NewCredential();
        credential.SetExpiresAt(SetAt.AddDays(7));
        credential.MarkExpiryWarningSent();

        credential.ReplaceSecret("new-ciphertext", SetAt.AddDays(6));
        credential.SetExpiresAt(SetAt.AddDays(13));

        credential.NeedsExpiryWarning(SetAt.AddDays(12), Window).Should().BeTrue();
    }

    [Fact]
    public void The_same_session_pasted_again_is_not_warned_about_twice()
    {
        var credential = NewCredential();
        credential.SetExpiresAt(SetAt.AddDays(7));
        credential.MarkExpiryWarningSent();

        credential.ReplaceSecret("same-session-reencrypted", SetAt.AddDays(6));
        credential.SetExpiresAt(SetAt.AddDays(7));

        credential.NeedsExpiryWarning(SetAt.AddDays(6), Window).Should().BeFalse();
    }

    [Fact]
    public void A_warning_cannot_be_recorded_for_a_credential_with_no_expiry()
    {
        var mark = () => NewCredential().MarkExpiryWarningSent();

        mark.Should().Throw<InvalidOperationException>();
    }
}
