using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

public abstract record ResolutionResult
{
    private ResolutionResult() { }

    public sealed record Resolved(string Canonical, int Confidence, ResolutionSource Source)
        : ResolutionResult;

    public sealed record Unresolved(int BestScore, string? BestCanonical = null) : ResolutionResult;
}

public interface IInputLabelResolver
{
    ResolutionResult Resolve(string rawLabel, WatchedStore store);
}
