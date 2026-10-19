using PointLurker.Core.Application.Errors;
using PointLurker.Core.Domain.Errors;

namespace PointLurker.Core.Application.Stores;

public readonly record struct StoreError(
    string Code,
    ErrorKind Kind,
    string Title,
    IReadOnlyDictionary<string, object?>? Params = null
) : IApplicationError
{
    public static StoreError NotFound() =>
        new(ErrorCodes.Stores.NotFound, ErrorKind.NotFound, "Store not found.");

    public static StoreError AlreadyExists(string channelId) =>
        new(
            ErrorCodes.Stores.AlreadyExists,
            ErrorKind.Conflict,
            $"A store with channel ID '{channelId}' already exists.",
            new Dictionary<string, object?> { ["channelId"] = channelId }
        );
}
