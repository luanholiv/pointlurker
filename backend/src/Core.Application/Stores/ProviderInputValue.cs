namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One resolved input for a redemption: the item's declared label and the owner's decrypted value.
/// Labelled rather than positional, so each provider client lays them out the way its wire wants.
/// </summary>
public sealed record ProviderInputValue(string Label, string Value);
