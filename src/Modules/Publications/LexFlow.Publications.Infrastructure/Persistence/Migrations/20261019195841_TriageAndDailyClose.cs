using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Publications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TriageAndDailyClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assignee_user_id",
                schema: "publications",
                table: "publications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "legal_case_id",
                schema: "publications",
                table: "publication_triages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "action",
                schema: "publications",
                table: "publication_triages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NoAction");

            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "publications",
                table: "publication_triages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "capture_source_periods",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_source_periods", x => x.id);
                    table.ForeignKey(
                        name: "fk_capture_source_periods_sources_source_id",
                        column: x => x.source_id,
                        principalSchema: "publications",
                        principalTable: "capture_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_closes",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publication_count = table.Column<int>(type: "integer", nullable: false),
                    capture_fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_closes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capture_source_periods_organization_id_source_id_activated_",
                schema: "publications",
                table: "capture_source_periods",
                columns: new[] { "organization_id", "source_id", "activated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_capture_source_periods_source_id",
                schema: "publications",
                table: "capture_source_periods",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_closes_organization_id_day_revision",
                schema: "publications",
                table: "daily_closes",
                columns: new[] { "organization_id", "day", "revision" },
                unique: true);
            migrationBuilder.Sql(
                """
                WITH transitions AS (
                    SELECT s.id AS source_id, s.organization_id, a.id, a.occurred_at,
                           COALESCE((a.changes #>> '{isActive,new}')::boolean, true) AS is_start,
                           COALESCE((a.changes #>> '{activatedAt,new}')::timestamptz, a.occurred_at) AS started_at
                    FROM publications.capture_sources s
                    JOIN audit.audit_logs a ON a.entity_type = 'CaptureSource'
                        AND a.entity_id = s.id::text AND a.organization_id = s.organization_id
                    WHERE a.changes ? 'isActive' OR a.changes ? 'activatedAt'
                ), periods AS (
                    SELECT *, lead(occurred_at) OVER (PARTITION BY source_id ORDER BY occurred_at, id) AS ended_at
                    FROM transitions
                )
                INSERT INTO publications.capture_source_periods (id, organization_id, source_id, activated_at, deactivated_at)
                SELECT gen_random_uuid(), organization_id, source_id, started_at, ended_at
                FROM periods WHERE is_start AND (ended_at IS NULL OR ended_at > started_at);

                -- Apenas para fontes legadas sem trilha: preservar o último intervalo conhecido.
                INSERT INTO publications.capture_source_periods (id, organization_id, source_id, activated_at, deactivated_at)
                SELECT gen_random_uuid(), s.organization_id, s.id, COALESCE(s.activated_at, s.created_at),
                       CASE WHEN s.is_active THEN NULL ELSE COALESCE(s.updated_at, s.created_at) END
                FROM publications.capture_sources s
                WHERE NOT EXISTS (SELECT 1 FROM publications.capture_source_periods p WHERE p.source_id = s.id);
                """);
            migrationBuilder.Sql(Security.EnableTenantIsolation("publications", "daily_closes"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("publications", "capture_source_periods"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("publications", "daily_closes"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("publications", "capture_source_periods"));
            migrationBuilder.DropTable(
                name: "capture_source_periods",
                schema: "publications");

            migrationBuilder.DropTable(
                name: "daily_closes",
                schema: "publications");

            migrationBuilder.DropColumn(
                name: "assignee_user_id",
                schema: "publications",
                table: "publications");

            migrationBuilder.DropColumn(
                name: "action",
                schema: "publications",
                table: "publication_triages");

            migrationBuilder.DropColumn(
                name: "reason",
                schema: "publications",
                table: "publication_triages");

            migrationBuilder.AlterColumn<Guid>(
                name: "legal_case_id",
                schema: "publications",
                table: "publication_triages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
