using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

/// <summary>
/// What a submitted redemption records while its completion is being polled: an upstream refusal,
/// and the fact that the status check itself keeps failing.
/// </summary>
public sealed class RedemptionPollFeedbackTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void MarkRejected_transitions_submitted_to_se_rejected()
    {
        var redemption = CreateSubmittedRedemption();

        redemption.MarkRejected(Now);

        redemption.Status.Should().Be(RedemptionStatus.Rejected);
        redemption.SettledAt.Should().Be(Now);
    }

    [Fact]
    public void MarkRejected_from_pending_throws()
    {
        var redemption = CreatePendingRedemption();

        var act = () => redemption.MarkRejected(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRejected_from_completed_throws()
    {
        var redemption = CreateSubmittedRedemption();
        redemption.MarkCompleted(Now);

        var act = () => redemption.MarkRejected(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// A refusal is terminal like the other end states, so the failure path cannot reopen it.
    /// </summary>
    [Fact]
    public void MarkFailed_from_se_rejected_throws()
    {
        var redemption = CreateSubmittedRedemption();
        redemption.MarkRejected(Now);

        var act = () => redemption.MarkFailed(SubmissionFailureCode.Unknown);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkCompleted_from_se_rejected_throws()
    {
        var redemption = CreateSubmittedRedemption();
        redemption.MarkRejected(Now);

        var act = () => redemption.MarkCompleted(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_new_redemption_has_no_poll_failures()
    {
        var redemption = CreatePendingRedemption();

        redemption.PollFailures.Should().Be(0);
        redemption.LastPollError.Should().BeNull();
    }

    [Fact]
    public void RecordPollFailure_counts_the_attempt_and_keeps_the_reason()
    {
        var redemption = CreateSubmittedRedemption();

        redemption.RecordPollFailure("ProviderApiException: deserializing the response");

        redemption.PollFailures.Should().Be(1);
        redemption.LastPollError.Should().Be("ProviderApiException: deserializing the response");
    }

    /// <summary>
    /// The count is what separates a blip from a wedged row, so consecutive failures accumulate.
    /// </summary>
    [Fact]
    public void RecordPollFailure_accumulates_across_ticks()
    {
        var redemption = CreateSubmittedRedemption();

        redemption.RecordPollFailure("first");
        redemption.RecordPollFailure("second");
        redemption.RecordPollFailure("third");

        redemption.PollFailures.Should().Be(3);
        redemption.LastPollError.Should().Be("third");
    }

    /// <summary>
    /// Recording a failure must not disturb the status: the redemption really was submitted, and
    /// only our reading of it failed.
    /// </summary>
    [Fact]
    public void RecordPollFailure_leaves_the_status_submitted()
    {
        var redemption = CreateSubmittedRedemption();

        redemption.RecordPollFailure("boom");

        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        redemption.FailureCode.Should().BeNull();
    }

    [Fact]
    public void RecordPollFailure_truncates_an_overlong_reason_to_the_column_width()
    {
        var redemption = CreateSubmittedRedemption();

        redemption.RecordPollFailure(new string('x', 900));

        redemption.LastPollError.Should().HaveLength(512);
    }

    [Fact]
    public void RecordPollFailure_from_pending_throws()
    {
        var redemption = CreatePendingRedemption();

        var act = () => redemption.RecordPollFailure("boom");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ClearPollFailures_resets_the_count_and_the_reason()
    {
        var redemption = CreateSubmittedRedemption();
        redemption.RecordPollFailure("boom");

        redemption.ClearPollFailures();

        redemption.PollFailures.Should().Be(0);
        redemption.LastPollError.Should().BeNull();
    }

    /// <summary>
    /// Lets the caller skip staging a write on the overwhelmingly common healthy tick.
    /// </summary>
    [Fact]
    public void ClearPollFailures_reports_whether_it_changed_anything()
    {
        var redemption = CreateSubmittedRedemption();

        redemption.ClearPollFailures().Should().BeFalse();

        redemption.RecordPollFailure("boom");

        redemption.ClearPollFailures().Should().BeTrue();
    }

    private static Redemption CreateSubmittedRedemption()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se-redemption-1", Now);
        return redemption;
    }

    private static Redemption CreatePendingRedemption()
    {
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        var storeItem = CreateStoreItem(entry.StoreItemId);
        return entry.CreateRedemption(storeItem, null, "Item", "Store", Now);
    }

    private static StoreItem CreateStoreItem(Guid id)
    {
        var data = new StoreItemApiData(
            StoreProvider.StreamElements,
            new ProviderItemId("se-1"),
            new StoreExternalId("ch-1"),
            "Test Item",
            null,
            "perk",
            100,
            "default",
            true,
            false,
            true,
            false,
            false,
            [],
            [],
            new ItemQuantity(-1, -1),
            new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now
        );
        return new StoreItem(id, Guid.NewGuid(), data);
    }
}
