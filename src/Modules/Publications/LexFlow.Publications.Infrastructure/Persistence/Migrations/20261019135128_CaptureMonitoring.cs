using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Publications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaptureMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "activated_at",
                schema: "publications",
                table: "capture_sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "source_activated_at",
                schema: "publications",
                table: "capture_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "capture_alerts",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_alerts", x => x.id);
                    table.ForeignKey(
                        name: "fk_capture_alerts_sources_source_id",
                        column: x => x.source_id,
                        principalSchema: "publications",
                        principalTable: "capture_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capture_alerts_organization_id_source_id_day",
                schema: "publications",
                table: "capture_alerts",
                columns: new[] { "organization_id", "source_id", "day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_capture_alerts_source_id",
                schema: "publications",
                table: "capture_alerts",
                column: "source_id");
            migrationBuilder.Sql(Security.EnableTenantIsolation("publications", "capture_alerts"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("publications", "capture_alerts"));

            migrationBuilder.DropTable(
                name: "capture_alerts",
                schema: "publications");

            migrationBuilder.DropColumn(
                name: "activated_at",
                schema: "publications",
                table: "capture_sources");

            migrationBuilder.DropColumn(
                name: "source_activated_at",
                schema: "publications",
                table: "capture_runs");
        }
    }
}
