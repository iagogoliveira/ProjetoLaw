using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Litigation.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Etapa 5, PR 5C1: pedidos do processo, valores por instância e o histórico das alterações de valor, com RLS nas
    /// três tabelas. O histórico só recebe inserções: a aplicação perde o UPDATE e o DELETE nele.
    /// </summary>
    public partial class ClaimsAndValuations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claims",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    correction_index = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    correct_from = table.Column<DateOnly>(type: "date", nullable: true),
                    interest_from = table.Column<DateOnly>(type: "date", nullable: true),
                    monthly_interest_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
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
                    table.PrimaryKey("pk_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_claims_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_valuations",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    estimated = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    awarded = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_valuations", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_valuations_claims_claim_id",
                        column: x => x.claim_id,
                        principalSchema: "litigation",
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "claim_value_changes",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    old_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    new_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_value_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_value_changes_claims_claim_id",
                        column: x => x.claim_id,
                        principalSchema: "litigation",
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_valuations_claim_id_instance",
                schema: "litigation",
                table: "claim_valuations",
                columns: new[] { "claim_id", "instance" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_claim_value_changes_claim_id",
                schema: "litigation",
                table: "claim_value_changes",
                column: "claim_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_legal_case_id",
                schema: "litigation",
                table: "claims",
                column: "legal_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_organization_id_legal_case_id",
                schema: "litigation",
                table: "claims",
                columns: new[] { "organization_id", "legal_case_id" });

            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "claims"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "claim_valuations"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "claim_value_changes"));
            migrationBuilder.Sql($"REVOKE UPDATE, DELETE, TRUNCATE ON litigation.claim_value_changes FROM {Security.ApplicationRole};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "claim_value_changes"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "claim_valuations"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "claims"));

            migrationBuilder.DropTable(
                name: "claim_valuations",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "claim_value_changes",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "claims",
                schema: "litigation");
        }
    }
}
