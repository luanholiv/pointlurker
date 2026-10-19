namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>
/// The fixed redemption form. GauPoints declares no inputs per item, so this client declares them,
/// in prose, because the store input profile shows labels to the owner verbatim. Each label maps to
/// one wire key in <see cref="GauPointsRedeemRequest"/>; both sides live here and cannot drift.
/// </summary>
internal static class GauPointsInputs
{
    public const string FullName = "Full name";
    public const string Phone = "Phone";
    public const string Email = "E-mail";
    public const string Address = "Address";

    private static readonly IReadOnlyList<string> Digital = [FullName, Phone, Email];
    private static readonly IReadOnlyList<string> Shipped = [FullName, Phone, Email, Address];

    /// <summary>
    /// Three inputs for a digital item, four for anything shipped. An unknown type asks for the
    /// address too: an address sent and not needed is harmless, one needed and missing is not.
    /// </summary>
    public static IReadOnlyList<string> DefinitionsFor(string type) =>
        string.Equals(type, "digital", StringComparison.Ordinal) ? Digital : Shipped;
}
