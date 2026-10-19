using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialAppSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Channel = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    Status = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    FailureReason = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WishlistEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    StreamElementsItemId = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    InputSnapshot = table.Column<string>(type: "text", nullable: false),
                    MessageSnapshot = table.Column<string>(type: "text", nullable: true),
                    StreamElementsRedemptionId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    FailureReason = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    AttemptedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_redemptions", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "watched_stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    DisplayName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: true
                    ),
                    LastSyncedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watched_stores", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "wishlist_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotifyOnAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    AutoRedeem = table.Column<bool>(type: "boolean", nullable: false),
                    UserInputValues = table.Column<string>(type: "text", nullable: false),
                    RedemptionMessage = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlist_entries", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "notification_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    ItemName = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_items_notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "store_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchedStoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamElementsId = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    ChannelId = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    Name = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    Cost = table.Column<long>(type: "bigint", nullable: false),
                    CategoryName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Featured = table.Column<bool>(type: "boolean", nullable: false),
                    Public = table.Column<bool>(type: "boolean", nullable: false),
                    SubscriberOnly = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMessages = table.Column<bool>(type: "boolean", nullable: false),
                    UserInputDefinitions = table.Column<string>(type: "jsonb", nullable: false),
                    Sources = table.Column<string>(type: "jsonb", nullable: false),
                    Quantity = table.Column<string>(type: "jsonb", nullable: false),
                    Cooldown = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousAvailability = table.Column<bool>(type: "boolean", nullable: true),
                    SeCreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    SeUpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastFetchedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_items_watched_stores_WatchedStoreId",
                        column: x => x.WatchedStoreId,
                        principalTable: "watched_stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_notification_items_NotificationId",
                table: "notification_items",
                column: "NotificationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_store_items_WatchedStoreId",
                table: "store_items",
                column: "WatchedStoreId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_watched_stores_UserId_ChannelId",
                table: "watched_stores",
                columns: new[] { "UserId", "ChannelId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_entries_UserId_StoreItemId",
                table: "wishlist_entries",
                columns: new[] { "UserId", "StoreItemId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "notification_items");

            migrationBuilder.DropTable(name: "redemptions");

            migrationBuilder.DropTable(name: "store_items");

            migrationBuilder.DropTable(name: "wishlist_entries");

            migrationBuilder.DropTable(name: "notifications");

            migrationBuilder.DropTable(name: "watched_stores");
        }
    }
}
