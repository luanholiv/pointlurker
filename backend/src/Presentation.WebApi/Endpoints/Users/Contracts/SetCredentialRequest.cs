namespace PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

/// <summary>
/// The body of <c>PUT /api/users/me/credentials/{provider}</c>. <see cref="Secret"/> is a live
/// credential: it is never echoed in a response, a log, or a validation failure's params.
/// </summary>
internal sealed record SetCredentialRequest(string Secret);
