using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Stores;

[Collection(ContainersCollection.Name)]
public sealed class StoreInputResolutionLogRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public StoreInputResolutionLogRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ListUnresolvedAsync_groups_by_raw_label_and_aggregates_against_postgres()
    {
        // Regression: the grouped projection must translate to a SQL GROUP BY.
        // Projecting aggregates straight into a record constructor made EF Core
        // fall back to client evaluation and throw at runtime (500), which the
        // mocked-repository unit test could never catch.
        var storeId = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        await using (var db = CreateDbContext())
        {
            db.WatchedStores.Add(
                new WatchedStore(
                    storeId,
                    StoreProvider.StreamElements,
                    new StoreExternalId("ch-unresolved"),
                    "Test",
                    Guid.NewGuid(),
                    t0
                )
            );
            db.StoreInputResolutionLog.AddRange(
                Unresolved(storeId, "discord tag", 60, t0),
                Unresolved(storeId, "discord tag", 72, t0.AddHours(2)),
                Unresolved(storeId, "steam id", 40, t0.AddHours(1)),
                // Non-unresolved rows must be excluded from the result.
                new StoreInputResolutionLogEntry(
                    Guid.NewGuid(),
                    storeId,
                    "twitch",
                    "Twitch Username",
                    95,
                    ResolutionSource.Fuzzy,
                    t0.AddHours(3)
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var repository = new StoreInputResolutionLogRepository(db);

            var result = await repository.ListUnresolvedAsync(storeId);

            result.Should().HaveCount(2);
            // Ordered by most-recent observation: "discord tag" (t0+2h) first.
            result[0].RawLabel.Should().Be("discord tag");
            result[0].BestScore.Should().Be(72);
            result[0].OccurrenceCount.Should().Be(2);
            result[0].LastObservedAt.Should().Be(t0.AddHours(2));
            result[1].RawLabel.Should().Be("steam id");
            result[1].OccurrenceCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task ListUnresolvedAsync_returns_empty_when_no_unresolved_rows()
    {
        await using var db = CreateDbContext();
        var repository = new StoreInputResolutionLogRepository(db);

        var result = await repository.ListUnresolvedAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestPerLabelAsync_returns_newest_outcome_per_label()
    {
        var storeId = Guid.NewGuid();
        var otherStoreId = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        await using (var db = CreateDbContext())
        {
            db.WatchedStores.Add(
                new WatchedStore(
                    storeId,
                    StoreProvider.StreamElements,
                    new StoreExternalId("ch-latest"),
                    "Test",
                    Guid.NewGuid(),
                    t0
                )
            );
            db.WatchedStores.Add(
                new WatchedStore(
                    otherStoreId,
                    StoreProvider.StreamElements,
                    new StoreExternalId("ch-other"),
                    "Other",
                    Guid.NewGuid(),
                    t0
                )
            );
            db.StoreInputResolutionLog.AddRange(
                Unresolved(storeId, "discord tag", 60, t0),
                // newer row supersedes: outcome flipped to Fuzzy
                new StoreInputResolutionLogEntry(
                    Guid.NewGuid(),
                    storeId,
                    "discord tag",
                    "Discord Tag",
                    91,
                    ResolutionSource.Fuzzy,
                    t0.AddHours(2)
                ),
                Unresolved(storeId, "steam id", 40, t0.AddHours(1)),
                // other store must not bleed in
                Unresolved(otherStoreId, "discord tag", 10, t0.AddHours(5))
            );
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var repository = new StoreInputResolutionLogRepository(db);

            var result = await repository.GetLatestPerLabelAsync(storeId);

            result.Should().HaveCount(2);
            result["discord tag"]
                .Should()
                .Be(new LatestLabelOutcome("Discord Tag", ResolutionSource.Fuzzy, 91));
            result["steam id"]
                .Should()
                .Be(new LatestLabelOutcome(null, ResolutionSource.Unresolved, 40));
        }
    }

    [Fact]
    public async Task GetLatestPerLabelAsync_returns_empty_for_unknown_store()
    {
        await using var db = CreateDbContext();
        var repository = new StoreInputResolutionLogRepository(db);

        var result = await repository.GetLatestPerLabelAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PruneAsync_deletes_old_superseded_rows_but_keeps_latest_per_label()
    {
        var storeId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
        var cutoff = now.AddDays(-30);

        await using (var db = CreateDbContext())
        {
            db.WatchedStores.Add(
                new WatchedStore(
                    storeId,
                    StoreProvider.StreamElements,
                    new StoreExternalId("ch-prune"),
                    "Test",
                    Guid.NewGuid(),
                    now.AddDays(-100)
                )
            );
            db.StoreInputResolutionLog.AddRange(
                // "steam id": superseded old row (delete) + newer old row (keep: latest of group)
                Unresolved(storeId, "steam id", 30, now.AddDays(-60)),
                Unresolved(storeId, "steam id", 42, now.AddDays(-40)),
                // "discord tag": single ancient row — latest of its group, must survive
                Unresolved(storeId, "discord tag", 10, now.AddDays(-90)),
                // "twitch": superseded old row (delete) + recent row (keep)
                Unresolved(storeId, "twitch", 50, now.AddDays(-45)),
                Unresolved(storeId, "twitch", 55, now.AddDays(-1))
            );
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var repository = new StoreInputResolutionLogRepository(db);

            var deleted = await repository.PruneAsync(cutoff);

            deleted.Should().Be(2);
        }

        await using (var db = CreateDbContext())
        {
            var survivors = await db
                .StoreInputResolutionLog.AsNoTracking()
                .Where(e => e.StoreId == storeId)
                .Select(e => new { e.RawLabel, e.Confidence })
                .ToListAsync();

            survivors
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        new { RawLabel = "steam id", Confidence = 42 },
                        new { RawLabel = "discord tag", Confidence = 10 },
                        new { RawLabel = "twitch", Confidence = 55 },
                    }
                );
        }
    }

    [Fact]
    public async Task PruneAsync_returns_zero_when_nothing_qualifies()
    {
        await using var db = CreateDbContext();
        var repository = new StoreInputResolutionLogRepository(db);

        var deleted = await repository.PruneAsync(DateTimeOffset.UtcNow.AddDays(-30));

        deleted.Should().Be(0);
    }

    private static StoreInputResolutionLogEntry Unresolved(
        Guid storeId,
        string rawLabel,
        int confidence,
        DateTimeOffset observedAt
    ) =>
        new(
            Guid.NewGuid(),
            storeId,
            rawLabel,
            null,
            confidence,
            ResolutionSource.Unresolved,
            observedAt
        );

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
