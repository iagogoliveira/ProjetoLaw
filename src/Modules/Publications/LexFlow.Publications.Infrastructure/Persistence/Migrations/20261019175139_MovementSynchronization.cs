using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Publications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovementSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_id",
                schema: "publications",
                table: "case_movements",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "movement_integrations",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<Guid>(type: "uuid", nullable: false),
                    protected_api_key = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movement_integrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movement_syncs",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tribunal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    configuration_revision = table.Column<Guid>(type: "uuid", nullable: false),
                    cursor = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_slot = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movement_syncs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_case_movements_organization_id_legal_case_id_origin_externa",
                schema: "publications",
                table: "case_movements",
                columns: new[] { "organization_id", "legal_case_id", "origin", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movement_integrations_organization_id",
                schema: "publications",
                table: "movement_integrations",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movement_syncs_organization_id_legal_case_id",
                schema: "publications",
                table: "movement_syncs",
                columns: new[] { "organization_id", "legal_case_id" },
                unique: true);
            migrationBuilder.Sql(Security.EnableTenantIsolation("publications", "movement_integrations"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("publications", "movement_syncs"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("publications", "movement_integrations"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("publications", "movement_syncs"));
            migrationBuilder.DropTable(
                name: "movement_integrations",
                schema: "publications");

            migrationBuilder.DropTable(
                name: "movement_syncs",
                schema: "publications");

            migrationBuilder.DropIndex(
                name: "ix_case_movements_organization_id_legal_case_id_origin_externa",
                schema: "publications",
                table: "case_movements");

            migrationBuilder.DropColumn(
                name: "external_id",
                schema: "publications",
                table: "case_movements");
        }
    }
}
