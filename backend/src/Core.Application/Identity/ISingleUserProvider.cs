using PointLurker.Core.Domain.Identity;

namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Resolves and persists the single application user. Keeps the
/// <c>Microsoft.EntityFrameworkCore</c> dependency out of the application layer (arch-test
/// enforced); the concrete provider lives in the infrastructure layer.
/// </summary>
public interface ISingleUserProvider
{
    Task<ApplicationUser?> GetAsync(CancellationToken ct = default);

    Task SaveAsync(ApplicationUser user, CancellationToken ct = default);
}
