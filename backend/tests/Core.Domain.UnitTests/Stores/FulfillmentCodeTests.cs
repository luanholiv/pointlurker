using FluentAssertions;
using PointLurker.Core.Domain.Stores;

namespace Core.Domain.UnitTests.Stores;

public sealed class FulfillmentCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_code_is_undelivered_with_no_attempts()
    {
        var redemptionId = Guid.NewGuid();

        var code = new FulfillmentCode(Guid.NewGuid(), redemptionId, "cipher", Now);

        code.RedemptionId.Should().Be(redemptionId);
        code.EncryptedCode.Should().Be("cipher");
        code.CreatedAt.Should().Be(Now);
        code.SentAt.Should().BeNull();
        code.Attempts.Should().Be(0);
        code.NextRetryAt.Should().BeNull();
        code.LastError.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_ciphertext_is_refused(string encryptedCode)
    {
        var act = () => new FulfillmentCode(Guid.NewGuid(), Guid.NewGuid(), encryptedCode, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_failed_attempt_counts_and_schedules_the_next_one()
    {
        var code = NewCode();

        code.RecordFailedAttempt("SMTP 451", Now.AddMinutes(2));

        code.Attempts.Should().Be(1);
        code.NextRetryAt.Should().Be(Now.AddMinutes(2));
        code.LastError.Should().Be("SMTP 451");
        code.SentAt.Should().BeNull();
    }

    [Fact]
    public void A_long_error_is_cut_to_the_column_width()
    {
        var code = NewCode();

        code.RecordFailedAttempt(new string('x', FulfillmentCode.MaxLastErrorLength + 40), Now);

        code.LastError.Should().HaveLength(FulfillmentCode.MaxLastErrorLength);
    }

    [Fact]
    public void Sending_clears_the_retry_state()
    {
        var code = NewCode();
        code.RecordFailedAttempt("SMTP 451", Now.AddMinutes(2));

        code.MarkSent(Now.AddMinutes(3));

        code.SentAt.Should().Be(Now.AddMinutes(3));
        code.Attempts.Should().Be(2);
        code.NextRetryAt.Should().BeNull();
        code.LastError.Should().BeNull();
    }

    [Fact]
    public void A_delivered_code_is_never_delivered_again()
    {
        var code = NewCode();
        code.MarkSent(Now);

        code.Invoking(c => c.MarkSent(Now)).Should().Throw<InvalidOperationException>();
        code.Invoking(c => c.RecordFailedAttempt("late", Now))
            .Should()
            .Throw<InvalidOperationException>();
    }

    private static FulfillmentCode NewCode() => new(Guid.NewGuid(), Guid.NewGuid(), "cipher", Now);
}
