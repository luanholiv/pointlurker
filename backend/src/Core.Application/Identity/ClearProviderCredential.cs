using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

/// <summary>Removes the owner's credential for one provider. Idempotent: clearing none succeeds.</summary>
public sealed class ClearProviderCredential(
    ISingleUserProvider userProvider,
    IProviderCredentialProvider credentials
)
{
    public async Task<Result<Unit, IdentityError>> ExecuteAsync(
        StoreProvider provider,
        CancellationToken ct = default
    )
    {
        if (!ProviderSecretProtectors.Supported.Contains(provider))
        {
            return Result<Unit, IdentityError>.Failure(
                IdentityError.CredentialProviderUnsupported(provider.ToSlug())
            );
        }

        var user = await userProvider.GetAsync(ct);
        if (user is null)
        {
            return Result<Unit, IdentityError>.Failure(IdentityError.UserNotFound());
        }

        var credential = await credentials.GetAsync(user.Id, provider, ct);
        if (credential is not null)
        {
            credentials.Remove(credential);
        }

        await userProvider.SaveAsync(user, ct);

        return Result<Unit, IdentityError>.Success(Unit.Value);
    }
}
