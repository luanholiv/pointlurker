using System.Globalization;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// The provider's answer to a redeem call it understood. Each client reads its own wire for these
/// outcomes, so the use case never matches one provider's wording. A failure that says nothing
/// about the redemption itself — no response, an error status — still arrives as
/// <see cref="ProviderApiException"/>.
/// </summary>
public abstract record ProviderRedemptionResult
{
    private ProviderRedemptionResult() { }

    /// <param name="RedemptionId">
    /// The provider's id for the redemption, to poll by later. Null for a provider that names none.
    /// </param>
    /// <param name="Completed">Fulfilled already, not merely accepted.</param>
    /// <param name="FulfillmentCode">
    /// A gift-card code the provider returned with the answer, or null. Returned once and never
    /// again, so it is persisted before anything else can go wrong. A bearer secret: never logged.
    /// </param>
    public sealed record Accepted(string? RedemptionId, bool Completed, string? FulfillmentCode)
        : ProviderRedemptionResult
    {
        // The generated ToString prints every member, and a record is exactly what ends up in a log
        // line or an assertion message. Only whether a code came back is printed.
        public override string ToString() =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"Accepted {{ RedemptionId = {RedemptionId}, Completed = {Completed}, FulfillmentCode = {(FulfillmentCode is null ? "null" : "<redacted>")} }}"
            );
    }

    /// <summary>Refused because the owner has too few points. Permanent: retrying spends nothing and changes nothing.</summary>
    /// <param name="Detail">What the provider said, for the failure context. Diagnostic only — never user-facing.</param>
    public sealed record InsufficientPoints(string Detail) : ProviderRedemptionResult;

    /// <summary>
    /// Refused for a reason of the provider's own that no more specific case covers. Permanent: the
    /// provider has looked at this redemption and said no.
    /// </summary>
    /// <param name="Detail">The status and the provider's message. Diagnostic only — never user-facing.</param>
    public sealed record Refused(string Detail) : ProviderRedemptionResult;
}
