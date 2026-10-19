using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Publications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        private static readonly string[] TenantTables = ["capture_sources", "capture_runs", "publications", "publication_triages", "case_movements"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "publications");

            migrationBuilder.CreateTable(
                name: "capture_sources",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "case_movements",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_movements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_integration_events",
                schema: "publications",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_integration_events", x => new { x.event_id, x.consumer });
                });

            migrationBuilder.CreateTable(
                name: "capture_runs",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    count = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_capture_runs_capture_sources_source_id",
                        column: x => x.source_id,
                        principalSchema: "publications",
                        principalTable: "capture_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "publications",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    available_on = table.Column<DateOnly>(type: "date", nullable: false),
                    published_on = table.Column<DateOnly>(type: "date", nullable: false),
                    text = table.Column<string>(type: "character varying(200000)", maxLength: 200000, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    extracted_cnj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publications", x => x.id);
                    table.ForeignKey(
                        name: "fk_publications_capture_sources_source_id",
                        column: x => x.source_id,
                        principalSchema: "publications",
                        principalTable: "capture_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "publication_triages",
                schema: "publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classification = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    triaged_by = table.Column<Guid>(type: "uuid", nullable: false),
                    triaged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deadline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hearing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication_triages", x => x.id);
                    table.ForeignKey(
                        name: "fk_publication_triages_publications_publication_id",
                        column: x => x.publication_id,
                        principalSchema: "publications",
                        principalTable: "publications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capture_runs_organization_id_source_id_day",
                schema: "publications",
                table: "capture_runs",
                columns: new[] { "organization_id", "source_id", "day" });

            migrationBuilder.CreateIndex(
                name: "ix_capture_runs_source_id",
                schema: "publications",
                table: "capture_runs",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_capture_sources_organization_id_is_active",
                schema: "publications",
                table: "capture_sources",
                columns: new[] { "organization_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_case_movements_organization_id_legal_case_id_occurred_at_id",
                schema: "publications",
                table: "case_movements",
                columns: new[] { "organization_id", "legal_case_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_publication_triages_organization_id_publication_id",
                schema: "publications",
                table: "publication_triages",
                columns: new[] { "organization_id", "publication_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_publication_triages_publication_id",
                schema: "publications",
                table: "publication_triages",
                column: "publication_id");

            migrationBuilder.CreateIndex(
                name: "ix_publications_organization_id_hash",
                schema: "publications",
                table: "publications",
                columns: new[] { "organization_id", "hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_publications_organization_id_legal_case_id",
                schema: "publications",
                table: "publications",
                columns: new[] { "organization_id", "legal_case_id" });

            migrationBuilder.CreateIndex(
                name: "ix_publications_organization_id_status_received_at_id",
                schema: "publications",
                table: "publications",
                columns: new[] { "organization_id", "status", "received_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_publications_source_id",
                schema: "publications",
                table: "publications",
                column: "source_id");
            migrationBuilder.Sql(Security.CreateApplicationRole());
            migrationBuilder.Sql(Security.GrantSchema("publications"));
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.EnableTenantIsolation("publications", table));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.DisableTenantIsolation("publications", table));
            }
            migrationBuilder.Sql(Security.RevokeSchema("publications"));

            migrationBuilder.DropTable(
                name: "capture_runs",
                schema: "publications");

            migrationBuilder.DropTable(
                name: "case_movements",
                schema: "publications");

            migrationBuilder.DropTable(
                name: "processed_integration_events",
                schema: "publications");

            migrationBuilder.DropTable(
                name: "publication_triages",
                schema: "publications");

            migrationBuilder.DropTable(
                name: "publications",
                schema: "publications");

            migrationBuilder.DropTable(
                name: "capture_sources",
                schema: "publications");
        }
    }
}
