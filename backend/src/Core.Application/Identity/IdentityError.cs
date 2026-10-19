using PointLurker.Core.Application.Errors;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

public readonly record struct IdentityError(
    string Code,
    ErrorKind Kind,
    string Title,
    IReadOnlyDictionary<string, object?>? Params = null
) : IApplicationError
{
    public static IdentityError UserNotFound() =>
        new(ErrorCodes.Users.NotFound, ErrorKind.NotFound, "User not found.");

    /// <param name="provider">The provider as the caller named it, which may name no provider at all.</param>
    public static IdentityError CredentialProviderUnsupported(string provider) =>
        new(
            ErrorCodes.Credentials.ProviderUnsupported,
            ErrorKind.NotFound,
            $"No credential can be stored for provider '{provider}'.",
            new Dictionary<string, object?>
            {
                ["provider"] = provider,
                ["allowed"] = ProviderSecretProtectors
                    .Supported.Select(supported => supported.ToSlug())
                    .ToArray(),
            }
        );

    public static IdentityError CredentialExpired(StoreProvider provider) =>
        new(
            ErrorCodes.Credentials.Expired,
            ErrorKind.Unprocessable,
            "This credential has already expired.",
            new Dictionary<string, object?> { ["provider"] = provider.ToSlug() }
        );

    public static IdentityError CredentialRejected(StoreProvider provider) =>
        new(
            ErrorCodes.Credentials.Rejected,
            ErrorKind.Unprocessable,
            "The provider did not accept this credential.",
            new Dictionary<string, object?> { ["provider"] = provider.ToSlug() }
        );
}
