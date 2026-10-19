using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace IntegrationTests.Email;

/// <summary>
/// Runs a Mailpit SMTP test server in a container. Mailpit accepts any auth over a
/// plaintext connection (MP_SMTP_AUTH_ACCEPT_ANY + MP_SMTP_AUTH_ALLOW_INSECURE) and
/// exposes an HTTP API to inspect captured messages.
/// </summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    // Pinned rather than floating on :latest, because one test asserts on how the server answers an
    // address the app should never have sent — behaviour a silent image bump could change under us.
    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:v1.31.1")
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(HttpPort, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(HttpPort))
        )
        .Build();

    private readonly HttpClient _http = new();

    public string SmtpHost => _container.Hostname;
    public int MappedSmtpPort => _container.GetMappedPublicPort(SmtpPort);
    public string ApiBaseUrl =>
        $"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}";

    /// <summary>
    /// Reads and empties the shared mailbox. One client serves the whole collection: the tests run
    /// sequentially, so there is nothing to gain from a connection pool per test class.
    /// </summary>
    public MailpitClient Messages { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Messages = new MailpitClient(_http, ApiBaseUrl);
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _container.DisposeAsync();
    }
}
