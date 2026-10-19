using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Identity;

namespace PointLurker.Infrastructure.Database.Identity;

/// <summary>
/// Single-user lookup over <see cref="UserManager{TUser}"/>. Queries are deliberately left
/// tracking-enabled (<see cref="IdentityDbContext"/> keeps EF's default, unlike
/// <see cref="AppDbContext"/>) so callers can mutate the returned user and persist it via
/// <see cref="SaveAsync"/> within the same scope.
/// </summary>
public sealed class SingleUserProvider(UserManager<ApplicationUser> userManager)
    : ISingleUserProvider
{
    public Task<ApplicationUser?> GetAsync(CancellationToken ct = default) =>
        userManager.Users.FirstOrDefaultAsync(ct);

    /// <remarks>
    /// <see cref="UserManager{TUser}.UpdateAsync"/> exposes no cancellable overload — it feeds the
    /// store its own protected <c>CancellationToken</c>. The caller's token is therefore honoured
    /// at the only point we control, before the write starts.
    /// </remarks>
    public Task SaveAsync(ApplicationUser user, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return userManager.UpdateAsync(user);
    }
}
