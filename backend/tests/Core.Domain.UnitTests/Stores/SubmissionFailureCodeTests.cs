using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class SubmissionFailureCodeTests
{
    // If this test fails, it means enum values were reordered or renumbered.
    // NEVER change existing int→name mappings. Add new values with the next unused int.

    private static readonly IReadOnlyDictionary<int, string> ExpectedSnapshot = new Dictionary<
        int,
        string
    >
    {
        [0] = nameof(SubmissionFailureCode.Unknown),
        [1] = nameof(SubmissionFailureCode.TokenDecryptionFailed),
        [2] = nameof(SubmissionFailureCode.InputCoverageLost),
        [3] = nameof(SubmissionFailureCode.InputDecryptionFailed),
        [4] = nameof(SubmissionFailureCode.ProviderApiBadRequest),
        [5] = nameof(SubmissionFailureCode.ProviderApiUnauthorized),
        [6] = nameof(SubmissionFailureCode.ProviderApiForbidden),
        [7] = nameof(SubmissionFailureCode.ProviderApiNotFound),
        [8] = nameof(SubmissionFailureCode.ProviderApiServerError),
        [9] = nameof(SubmissionFailureCode.InsufficientPoints),
        [10] = nameof(SubmissionFailureCode.ProviderRejected),
        [11] = nameof(SubmissionFailureCode.CredentialExpired),
    };

    [Fact]
    public void SubmissionFailureCode_int_to_name_mapping_is_unchanged()
    {
        var actualSnapshot = Enum.GetValues<SubmissionFailureCode>()
            .ToDictionary(v => (int)v, v => v.ToString());

        actualSnapshot
            .Should()
            .BeEquivalentTo(
                ExpectedSnapshot,
                options => options.WithStrictOrdering(),
                "SubmissionFailureCode is append-only — existing int→name mappings must never change"
            );
    }

    [Fact]
    public void MarkFailed_with_enum_transitions_pending_to_submission_failed()
    {
        var redemption = CreatePendingRedemption();

        redemption.MarkFailed(SubmissionFailureCode.ProviderApiUnauthorized, "HTTP 401");

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.ProviderApiUnauthorized);
        redemption.FailureContext.Should().Be("HTTP 401");
    }

    /// <summary>A provider that names no redemption still records the submission.</summary>
    [Fact]
    public void MarkSubmitted_accepts_a_provider_that_returns_no_id()
    {
        var redemption = CreatePendingRedemption();

        redemption.MarkSubmitted(null, DateTimeOffset.UtcNow);

        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        redemption.ProviderRedemptionId.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_with_null_context_stores_null()
    {
        var redemption = CreatePendingRedemption();

        redemption.MarkFailed(SubmissionFailureCode.InputDecryptionFailed, null);

        redemption.FailureCode.Should().Be(SubmissionFailureCode.InputDecryptionFailed);
        redemption.FailureContext.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_enum_from_completed_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se_1", DateTimeOffset.UtcNow);
        redemption.MarkCompleted(DateTimeOffset.UtcNow.AddSeconds(1));

        var act = () => redemption.MarkFailed(SubmissionFailureCode.Unknown, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_enum_from_submission_failed_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkFailed(SubmissionFailureCode.Unknown, null);

        var act = () => redemption.MarkFailed(SubmissionFailureCode.ProviderApiNotFound, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_enum_from_se_completion_timed_out_throws()
    {
        var redemption = CreatePendingRedemption();
        redemption.MarkSubmitted("se_1", DateTimeOffset.UtcNow);
        redemption.MarkTimedOut();

        var act = () => redemption.MarkFailed(SubmissionFailureCode.Unknown, null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateRedemption_failure_code_and_context_are_null_initially()
    {
        var redemption = CreatePendingRedemption();

        redemption.FailureCode.Should().BeNull();
        redemption.FailureContext.Should().BeNull();
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Redemption CreatePendingRedemption()
    {
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
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
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow
        );
        return new StoreItem(id, Guid.NewGuid(), data);
    }
}
