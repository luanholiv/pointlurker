namespace PointLurker.Core.Application.Notifications;

/// <summary>
/// Marker message posted after a fulfillment code is committed, to deliver it without waiting for
/// the next sweep. It carries nothing: the delivery reads what is owed from the database, so a lost
/// signal costs latency and never the code.
/// </summary>
public sealed record FulfillmentCodeReady
{
    public static readonly FulfillmentCodeReady Instance = new();
}
