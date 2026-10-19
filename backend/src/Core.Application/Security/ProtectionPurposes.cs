namespace PointLurker.Core.Application.Security;

/// <summary>
/// Canonical Data Protection purpose strings. Values MUST remain stable: changing a string
/// re-derives the sub-key and renders existing ciphertext unreadable.
/// </summary>
public static class ProtectionPurposes
{
    public const string StreamElementsToken = "StreamElementsToken";
    public const string StoreInputProfile = "StoreInputProfile";
    public const string RedemptionMessage = "RedemptionMessage";
    public const string GauPointsSession = "GauPointsSession";

    /// <summary>
    /// Gift-card codes. Unlike every other purpose, what it protects cannot be re-entered: losing
    /// the key ring loses the codes.
    /// </summary>
    public const string FulfillmentCode = "FulfillmentCode";
}
