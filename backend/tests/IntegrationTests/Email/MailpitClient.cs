using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Sdk;

namespace IntegrationTests.Email;

/// <summary>
/// Reads and empties the mailbox of the shared Mailpit container over its HTTP API.
/// </summary>
public sealed class MailpitClient(HttpClient http, string baseUrl)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Deletes every captured message. The container is shared by the whole collection, so a test
    /// that asserts on "the" message has to start from an empty mailbox to mean anything.
    /// </summary>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"{baseUrl}/api/v1/messages", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<MailpitMessage>> ListAsync(CancellationToken ct = default)
    {
        var list = await http.GetFromJsonAsync<MailpitMessageList>(
            $"{baseUrl}/api/v1/messages",
            Json,
            ct
        );
        return list?.Messages ?? [];
    }

    /// <summary>Fetches one message by id, which is the only form carrying the bodies.</summary>
    public async Task<MailpitMessage> GetAsync(string id, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<MailpitMessage>($"{baseUrl}/api/v1/message/{id}", Json, ct)
        ?? throw new XunitException($"Mailpit returned no message for id '{id}'.");

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        (await ListAsync(ct)).Count;

    /// <summary>
    /// Returns the single captured message, waiting only as long as it takes for one to arrive.
    /// <para>
    /// The wait is insurance rather than synchronisation: MailKit's send returns once the server
    /// has answered 250, by which point Mailpit has already stored the message, so the first poll
    /// normally answers. The loop is what makes a loaded machine produce a slower pass instead of
    /// a flake.
    /// </para>
    /// <para>
    /// It re-asks a real question on a timer rather than sleeping for a guessed interval, which is
    /// both the more honest wait and the one the repository's test-shortcut analyzer permits.
    /// </para>
    /// </summary>
    public async Task<MailpitMessage> WaitForSingleMessageAsync(CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(WaitTimeout);
        using var ticker = new PeriodicTimer(PollInterval);

        try
        {
            do
            {
                var messages = await ListAsync(deadline.Token);
                if (messages.Count > 1)
                {
                    throw new XunitException(
                        $"Expected a single captured message but Mailpit holds {messages.Count}."
                    );
                }

                if (messages.Count == 1)
                {
                    return await GetAsync(messages[0].Id, ct);
                }
            } while (await ticker.WaitForNextTickAsync(deadline.Token));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new XunitException($"Mailpit captured no message within {WaitTimeout}.");
        }

        throw new XunitException($"Mailpit captured no message within {WaitTimeout}.");
    }
}
