using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Stores;
using PointLurker.Infrastructure.Database.Configurations;

namespace PointLurker.Infrastructure.Database;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<WatchedStore> WatchedStores => Set<WatchedStore>();
    public DbSet<StoreItem> StoreItems => Set<StoreItem>();
    public DbSet<WishlistEntry> WishlistEntries => Set<WishlistEntry>();
    public DbSet<Redemption> Redemptions => Set<Redemption>();
    public DbSet<FulfillmentCode> FulfillmentCodes => Set<FulfillmentCode>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationItem> NotificationItems => Set<NotificationItem>();
    public DbSet<StoreInputResolutionLogEntry> StoreInputResolutionLog =>
        Set<StoreInputResolutionLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Item search needs both: `unaccent` supplies the dictionary the `item_search` text search
        // configuration folds accents with, and `pg_trgm` supplies the near-miss comparison that
        // catches a misspelled term. Declared on the model rather than only in the migration so the
        // snapshot carries them and a rebuilt database is not quietly missing them.
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfiguration(new WatchedStoreConfiguration());
        modelBuilder.ApplyConfiguration(new StoreItemConfiguration());
        modelBuilder.ApplyConfiguration(new WishlistEntryConfiguration());
        modelBuilder.ApplyConfiguration(new RedemptionConfiguration());
        modelBuilder.ApplyConfiguration(new FulfillmentCodeConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationItemConfiguration());
        modelBuilder.ApplyConfiguration(new StoreInputResolutionLogEntryConfiguration());
    }
}
