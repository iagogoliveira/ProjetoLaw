using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationPoliciesAndDailyDigest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "digest_deliveries",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_digest_deliveries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_digest_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    include_agenda = table.Column<bool>(type: "boolean", nullable: false),
                    include_deadlines = table.Column<bool>(type: "boolean", nullable: false),
                    include_publications = table.Column<bool>(type: "boolean", nullable: false),
                    muted_kinds = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_rules",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_kinds = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_digest_deliveries_organization_id_user_id_day",
                schema: "platform",
                table: "digest_deliveries",
                columns: new[] { "organization_id", "user_id", "day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_organization_id_user_id",
                schema: "platform",
                table: "notification_preferences",
                columns: new[] { "organization_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_rules_organization_id",
                schema: "platform",
                table: "notification_rules",
                column: "organization_id",
                unique: true);
            migrationBuilder.Sql(Security.EnableTenantIsolation("platform", "digest_deliveries"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("platform", "notification_preferences"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("platform", "notification_rules"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("platform", "digest_deliveries"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("platform", "notification_preferences"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("platform", "notification_rules"));
            migrationBuilder.DropTable(
                name: "digest_deliveries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "notification_preferences",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "notification_rules",
                schema: "platform");
        }
    }
}
