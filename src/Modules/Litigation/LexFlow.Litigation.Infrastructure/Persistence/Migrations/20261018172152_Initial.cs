using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Litigation.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Módulo Litigation (Etapa 5, PR 5A): processos e casos, partes, responsáveis, hierarquia, numeração interna e o
    /// contador dela, com Row-Level Security em todas as tabelas de escritório e privilégios do papel da aplicação.
    /// </summary>
    public partial class Initial : Migration
    {
        /// <summary>Toda tabela <c>ITenantOwned</c> do módulo (o teste de RLS confere que nenhuma ficou de fora).</summary>
        public static readonly string[] TenantTables = ["cases", "case_parties", "case_assignments", "case_relations", "case_numberings", "case_counters"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "litigation");

            migrationBuilder.CreateTable(
                name: "case_counters",
                schema: "litigation",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    practice_area_key = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_value = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_counters", x => new { x.organization_id, x.practice_area_key, x.year });
                });

            migrationBuilder.CreateTable(
                name: "case_numberings",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    area_codes = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_numberings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cases",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    internal_folder = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    cnj_number = table.Column<string>(type: "character(20)", fixedLength: true, maxLength: 20, nullable: true),
                    court_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    district = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    court_unit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rite = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    procedural_class = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    action_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    practice_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    claim_value = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    filed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    served_on = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    payer_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    closing_reason_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    final_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    closing_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    custom_fields = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_integration_events",
                schema: "litigation",
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
                name: "case_assignments",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_assignments_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_parties",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_client = table.Column<bool>(type: "boolean", nullable: false),
                    counsel_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    counsel_oab = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_parties", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_parties_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_relations",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_relations", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_relations_cases_child_case_id",
                        column: x => x.child_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_case_relations_cases_parent_case_id",
                        column: x => x.parent_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_case_assignments_legal_case_id",
                schema: "litigation",
                table: "case_assignments",
                column: "legal_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_assignments_organization_id_team_id",
                schema: "litigation",
                table: "case_assignments",
                columns: new[] { "organization_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_case_assignments_organization_id_user_id",
                schema: "litigation",
                table: "case_assignments",
                columns: new[] { "organization_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_case_numberings_organization_id",
                schema: "litigation",
                table: "case_numberings",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_parties_legal_case_id",
                schema: "litigation",
                table: "case_parties",
                column: "legal_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_parties_organization_id_person_id",
                schema: "litigation",
                table: "case_parties",
                columns: new[] { "organization_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_case_relations_child_case_id",
                schema: "litigation",
                table: "case_relations",
                column: "child_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_relations_organization_id_child_case_id",
                schema: "litigation",
                table: "case_relations",
                columns: new[] { "organization_id", "child_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_relations_organization_id_parent_case_id",
                schema: "litigation",
                table: "case_relations",
                columns: new[] { "organization_id", "parent_case_id" });

            migrationBuilder.CreateIndex(
                name: "ix_case_relations_parent_case_id",
                schema: "litigation",
                table: "case_relations",
                column: "parent_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_cnj_number",
                schema: "litigation",
                table: "cases",
                columns: new[] { "organization_id", "cnj_number" },
                unique: true,
                filter: "cnj_number IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_created_at",
                schema: "litigation",
                table: "cases",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_internal_folder",
                schema: "litigation",
                table: "cases",
                columns: new[] { "organization_id", "internal_folder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_status_practice_area_id",
                schema: "litigation",
                table: "cases",
                columns: new[] { "organization_id", "status", "practice_area_id" });

            migrationBuilder.Sql(Security.CreateApplicationRole());
            migrationBuilder.Sql(Security.GrantSchema("litigation"));
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", table));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", table));
            }

            migrationBuilder.Sql(Security.RevokeSchema("litigation"));

            migrationBuilder.DropTable(
                name: "case_assignments",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "case_counters",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "case_numberings",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "case_parties",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "case_relations",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "processed_integration_events",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "cases",
                schema: "litigation");
        }
    }
}
