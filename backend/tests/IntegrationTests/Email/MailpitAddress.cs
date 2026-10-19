namespace IntegrationTests.Email;

/// <summary>A mailbox on a captured message. Mailpit spells these <c>Name</c> and <c>Address</c>.</summary>
public sealed record MailpitAddress(string Name, string Address);
