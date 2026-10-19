using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class RedemptionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateRedemption_stores_name_snapshot()
    {
        var entry = CreateEntry();
        var storeItem = CreateStoreItem(entry.StoreItemId);

        var redemption = entry.CreateRedemption(storeItem, null, "VIP Badge", "xQc", Now);

        redemption.StoreItemName.Should().Be("VIP Badge");
        redemption.StoreName.Should().Be("xQc");
    }

    [Fact]
    public void CreateRedemption_produces_pending_with_zero_attempts()
    {
        var entry = CreateEntry();
        var storeItem = CreateStoreItem(entry.StoreItemId);

        var redemption = entry.CreateRedemption(storeItem, "msg", "Item", "Store", Now);

        redemption.Status.Should().Be(RedemptionStatus.Pending);
        redemption.Attempts.Should().Be(0);
        redemption.SubmittedAt.Should().BeNull();
        redemption.SettledAt.Should().BeNull();
        redemption.NextRetryAt.Should().BeNull();
        redemption.ProviderRedemptionId.Should().BeNull();
        redemption.FailureCode.Should().BeNull();
        redemption.FailureContext.Should().BeNull();
    }

    [Fact]
    public void MarkSubmitted_transitions_pending_to_submitted()
    {
        var redemption = CreatePendingRedemption();

        redemption.MarkSubmitted("se_123", Now);

        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        redemption.ProviderRedemptionId.Should().Be("se_123");
        redemption.SubmittedAt.Should().Be(Now);
        redemption.Attempts.Should().Be(1);
    }

    [Fact]
    public void MarkSubmitted_from_non_pending_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se_1", Now);

        var act = () => redemption.MarkSubmitted("se_2", Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkCompleted_transitions_submitted_to_completed()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se_123", Now);

        redemption.MarkCompleted(Now.AddHours(1));

        redemption.Status.Should().Be(RedemptionStatus.Completed);
        redemption.SettledAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void MarkCompleted_from_pending_throws()
    {
        var redemption = CreatePendingRedemption();

        var act = () => redemption.MarkCompleted(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkCompleted_from_terminal_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkFailed(SubmissionFailureCode.Unknown);

        var act = () => redemption.MarkCompleted(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_transitions_pending_to_submission_failed()
    {
        var redemption = CreatePendingRedemption();

        redemption.MarkFailed(SubmissionFailureCode.ProviderApiUnauthorized, "HTTP 401");

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.ProviderApiUnauthorized);
        redemption.FailureContext.Should().Be("HTTP 401");
    }

    [Fact]
    public void MarkFailed_without_context_sets_null_context()
    {
        var redemption = CreatePendingRedemption();

        redemption.MarkFailed(SubmissionFailureCode.InputCoverageLost);

        redemption.FailureCode.Should().Be(SubmissionFailureCode.InputCoverageLost);
        redemption.FailureContext.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_from_completed_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se_1", Now);
        redemption.MarkCompleted(Now.AddSeconds(1));

        var act = () => redemption.MarkFailed(SubmissionFailureCode.Unknown);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_from_submission_failed_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkFailed(SubmissionFailureCode.TokenDecryptionFailed);

        var act = () => redemption.MarkFailed(SubmissionFailureCode.Unknown);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ScheduleRetry_updates_next_retry_and_increments_attempts()
    {
        var redemption = CreatePendingRedemption();
        var retryAt = Now.AddSeconds(4);

        redemption.ScheduleRetry(retryAt);

        redemption.Status.Should().Be(RedemptionStatus.Pending);
        redemption.NextRetryAt.Should().Be(retryAt);
        redemption.Attempts.Should().Be(1);
    }

    [Fact]
    public void ScheduleRetry_from_submitted_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se_1", Now);

        var act = () => redemption.ScheduleRetry(Now.AddSeconds(4));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkTimedOut_transitions_submitted_to_timed_out()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se_1", Now);

        redemption.MarkTimedOut();

        redemption.Status.Should().Be(RedemptionStatus.CompletionTimedOut);
    }

    [Fact]
    public void MarkTimedOut_from_pending_throws()
    {
        var redemption = CreatePendingRedemption();

        var act = () => redemption.MarkTimedOut();

        act.Should().Throw<InvalidOperationException>();
    }

    private static Redemption CreatePendingRedemption()
    {
        var entry = CreateEntry();
        var storeItem = CreateStoreItem(entry.StoreItemId);
        return entry.CreateRedemption(storeItem, null, "Item", "Store", Now);
    }

    private static WishlistEntry CreateEntry()
    {
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        return entry;
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
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow
        );
        return new StoreItem(id, Guid.NewGuid(), data);
    }
}
