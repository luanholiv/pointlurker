using System.Globalization;

namespace PointLurker.Core.Application.Notifications;

/// <summary>The email that delivers one gift-card code.</summary>
/// <param name="Code">The plaintext code. A bearer secret: never logged.</param>
public sealed record FulfillmentCodeEmailModel(string StoreName, string ItemName, string Code)
{
    // The generated ToString prints every member, and a record is exactly what ends up in a log
    // line or an assertion message.
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"FulfillmentCodeEmailModel {{ StoreName = {StoreName}, ItemName = {ItemName}, Code = <redacted> }}"
        );
}
