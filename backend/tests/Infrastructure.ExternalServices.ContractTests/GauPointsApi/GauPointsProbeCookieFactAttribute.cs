namespace PointLurker.Infrastructure.ExternalServices.ContractTests.GauPointsApi;

/// <summary>
/// A fact that runs only when <c>GAUPOINTS_PROBE_COOKIE</c> holds a live session. CI never sets it,
/// so a test marked with this is skipped there even if a category filter lets it through.
/// </summary>
public sealed class GauPointsProbeCookieFactAttribute : FactAttribute
{
    public const string VariableName = "GAUPOINTS_PROBE_COOKIE";

    public GauPointsProbeCookieFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VariableName)))
        {
            Skip = $"{VariableName} is not set; this probe needs a live session.";
        }
    }
}
