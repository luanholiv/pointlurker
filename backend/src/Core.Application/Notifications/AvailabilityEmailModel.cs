namespace PointLurker.Core.Application.Notifications;

/// <summary>
/// Everything one availability notification renders, grouped the way the email lays it out.
/// </summary>
public sealed record AvailabilityEmailModel(IReadOnlyList<AvailabilityEmailStoreGroup> Stores);
