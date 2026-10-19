using System.Linq.Expressions;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace IntegrationTests.Migrations;

/// <summary>
/// A credential read through a projection of the columns its first migration created. A test
/// that stops at an older migration cannot load the entity itself: the current model selects every
/// column mapped today, including ones a later migration adds, and the query fails on the missing
/// column.
/// </summary>
internal sealed record CredentialSnapshot(
    Guid UserId,
    StoreProvider Provider,
    string EncryptedSecret,
    DateTimeOffset SecretSetAt,
    string? AccountName,
    DateTimeOffset? AccountNameRefreshedAt,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationError
)
{
    public static Expression<Func<ProviderCredential, CredentialSnapshot>> Project { get; } =
        credential => new CredentialSnapshot(
            credential.UserId,
            credential.Provider,
            credential.EncryptedSecret,
            credential.SecretSetAt,
            credential.AccountName,
            credential.AccountNameRefreshedAt,
            credential.LastValidatedAt,
            credential.LastValidationError
        );
}
