using FluentAssertions;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Domain.UnitTests.Stores;

public sealed class StoreProviderTests
{
    // If this test fails, it means enum values were reordered or renumbered.
    // NEVER change existing int→name mappings. Add new values with the next unused int.

    private static readonly IReadOnlyDictionary<int, string> ExpectedSnapshot = new Dictionary<
        int,
        string
    >
    {
        [0] = nameof(StoreProvider.StreamElements),
        [1] = nameof(StoreProvider.GauPoints),
    };

    [Fact]
    public void StoreProvider_int_to_name_mapping_is_unchanged()
    {
        var actualSnapshot = Enum.GetValues<StoreProvider>()
            .ToDictionary(v => (int)v, v => v.ToString());

        actualSnapshot
            .Should()
            .BeEquivalentTo(
                ExpectedSnapshot,
                options => options.WithStrictOrdering(),
                "StoreProvider is append-only — existing int→name mappings must never change"
            );
    }
}
