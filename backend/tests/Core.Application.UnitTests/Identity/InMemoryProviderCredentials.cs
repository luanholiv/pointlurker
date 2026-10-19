using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Identity;

/// <summary>
/// A credential store backed by a list. Changes land immediately rather than at a commit, so a test
/// asserting on <see cref="Held"/> sees what the use case staged; whether it committed is a separate
/// assertion, on <see cref="ISingleUserProvider.SaveAsync"/>.
/// </summary>
internal sealed class InMemoryProviderCredentials : IProviderCredentialProvider
{
    private readonly List<ProviderCredential> _held = [];

    public IReadOnlyList<ProviderCredential> Held => _held;

    public InMemoryProviderCredentials With(ProviderCredential credential)
    {
        _held.Add(credential);
        return this;
    }

    public Task<ProviderCredential?> GetAsync(
        Guid userId,
        StoreProvider provider,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            _held.SingleOrDefault(credential =>
                credential.UserId == userId && credential.Provider == provider
            )
        );

    public Task<IReadOnlyList<ProviderCredential>> ListAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ProviderCredential>>(
            _held
                .Where(credential => credential.UserId == userId)
                .OrderBy(credential => credential.Provider)
                .ToList()
        );

    public void Add(ProviderCredential credential) => _held.Add(credential);

    public void Remove(ProviderCredential credential) => _held.Remove(credential);
}
