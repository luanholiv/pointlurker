using IntegrationTests.Email;

namespace IntegrationTests;

/// <summary>
/// The one collection for every container-backed integration test.
/// <para>
/// xUnit lets a class join a single collection, so a test that needs both Postgres and Mailpit can
/// only receive them from the same collection definition. Splitting them across two definitions is
/// not an option either: a collection fixture is created once per <em>collection</em>, not once per
/// assembly, so a second definition naming <see cref="DatabaseFixture"/> would start a second
/// Postgres container rather than share the first.
/// </para>
/// <para>
/// Tests inside a collection run sequentially, which every member already requires: the Respawn
/// reset in <see cref="DatabaseFixture"/> and the mailbox clear in <see cref="MailpitFixture"/> both
/// wipe shared state between tests.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class ContainersCollection
    : ICollectionFixture<DatabaseFixture>,
        ICollectionFixture<MailpitFixture>
{
    public const string Name = "Containers";
}
