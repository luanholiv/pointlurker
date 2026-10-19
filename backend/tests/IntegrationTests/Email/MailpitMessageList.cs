namespace IntegrationTests.Email;

/// <summary>The envelope Mailpit wraps a message listing in.</summary>
public sealed record MailpitMessageList(IReadOnlyList<MailpitMessage> Messages);
