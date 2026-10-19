namespace PointLurker.Core.Domain.Stores;

/// <summary>
/// Whether applying an upstream payload actually moved a <see cref="StoreItem"/>. A sync pass
/// touches every item of every store on every tick, so "how many items did this pass look at" and
/// "how many items did this pass change" are different questions; this is the answer to the second
/// one, decided by the entity that owns the state rather than inferred downstream.
/// </summary>
public enum StoreItemChange
{
    /// <summary>The payload carried nothing the item did not already hold.</summary>
    Unchanged,

    /// <summary>At least one persisted field now differs from what was there before.</summary>
    Changed,
}
