using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalendarSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_connections",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    from = table.Column<DateOnly>(type: "date", nullable: false),
                    until = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "simulated_calendar_items",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    remote_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    link = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulated_calendar_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_sync_links",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    local_id = table.Column<Guid>(type: "uuid", nullable: true),
                    remote_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    remote_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    remote_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    local_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pending_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    pending_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    pending_local_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_sync_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_sync_links_calendar_connections_connection_id",
                        column: x => x.connection_id,
                        principalSchema: "scheduling",
                        principalTable: "calendar_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_connections_organization_id_user_id_provider",
                schema: "scheduling",
                table: "calendar_connections",
                columns: new[] { "organization_id", "user_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_sync_links_connection_id",
                schema: "scheduling",
                table: "calendar_sync_links",
                column: "connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_sync_links_organization_id_connection_id_kind_loca",
                schema: "scheduling",
                table: "calendar_sync_links",
                columns: new[] { "organization_id", "connection_id", "kind", "local_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_sync_links_organization_id_connection_id_remote_id",
                schema: "scheduling",
                table: "calendar_sync_links",
                columns: new[] { "organization_id", "connection_id", "remote_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_simulated_calendar_items_organization_id_user_id_provider_c",
                schema: "scheduling",
                table: "simulated_calendar_items",
                columns: new[] { "organization_id", "user_id", "provider", "client_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_simulated_calendar_items_organization_id_user_id_provider_r",
                schema: "scheduling",
                table: "simulated_calendar_items",
                columns: new[] { "organization_id", "user_id", "provider", "remote_id" },
                unique: true);
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "calendar_connections"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "calendar_sync_links"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "simulated_calendar_items"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "calendar_sync_links"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "simulated_calendar_items"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "calendar_connections"));
            migrationBuilder.DropTable(
                name: "calendar_sync_links",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "simulated_calendar_items",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "calendar_connections",
                schema: "scheduling");
        }
    }
}
