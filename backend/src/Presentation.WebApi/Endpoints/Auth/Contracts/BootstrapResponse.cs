namespace PointLurker.Presentation.WebApi.Endpoints.Auth.Contracts;

/// <summary>
/// Anonymous first-run gate. Projects a single <see cref="bool"/>, so it declares no conversion
/// operator — there is no application result record behind it.
/// </summary>
internal sealed record BootstrapResponse(bool UserExists);
