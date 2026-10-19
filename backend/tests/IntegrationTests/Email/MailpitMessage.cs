namespace IntegrationTests.Email;

/// <summary>
/// A message captured by Mailpit.
/// <para>
/// One shape serves both endpoints the tests use: the list at <c>/api/v1/messages</c> omits the
/// bodies, and the detail at <c>/api/v1/message/{id}</c> carries them. Members absent from either
/// response simply stay null, and the deserializer is configured to ignore members it does not
/// know, so neither endpoint needs a type of its own.
/// </para>
/// <para>
/// Mailpit spells these <c>ID</c>, <c>HTML</c> and <c>Text</c>; case-insensitive matching is what
/// binds them here, which is why no property carries a name attribute.
/// </para>
/// </summary>
public sealed record MailpitMessage(
    string Id,
    MailpitAddress From,
    IReadOnlyList<MailpitAddress> To,
    string Subject,
    string? Html,
    string? Text
);
