using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Outcome of resolving one item's user-input definitions against its store's input profile.
/// Callers decide what each outcome means to them: auto-redeem treats anything but
/// <see cref="Resolved"/> as "skip", while submission distinguishes lost coverage from a
/// decryption fault.
/// </summary>
public abstract record StoreInputResolution
{
    private StoreInputResolution() { }

    /// <summary>
    /// Every definition resolved and decrypted, in definition order, each value paired with the
    /// label the item declared it under.
    /// </summary>
    public sealed record Resolved(IReadOnlyList<ProviderInputValue> Values) : StoreInputResolution;

    /// <summary>
    /// The raw definitions the store profile does not cover — either the label resolved to nothing
    /// or the canonical label it resolved to holds no value.
    /// </summary>
    public sealed record Missing(IReadOnlyList<string> Labels) : StoreInputResolution;

    /// <summary>A stored value exists but could not be decrypted.</summary>
    public sealed record DecryptionFailed(string Label, Exception Exception) : StoreInputResolution;
}

public interface IStoreInputValueResolver
{
    /// <param name="store">
    /// The item's store. Passed explicitly rather than read from <see cref="StoreItem.WatchedStore"/>
    /// because a missing store means different things to different callers.
    /// </param>
    StoreInputResolution Resolve(StoreItem item, WatchedStore store);
}
