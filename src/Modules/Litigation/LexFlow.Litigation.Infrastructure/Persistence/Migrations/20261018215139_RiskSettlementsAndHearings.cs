using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Litigation.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Etapa 5, PR 5C2: risco, acordos (rodadas e parcelas) e audiências (com as redesignações), com RLS nas seis
    /// tabelas. A avaliação de risco e a redesignação só recebem inserções: a aplicação perde o UPDATE e o DELETE nelas.
    /// </summary>
    public partial class RiskSettlementsAndHearings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hearings",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    meeting_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    lawyer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    representative = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    witness_person_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    outcome = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hearings", x => x.id);
                    table.ForeignKey(
                        name: "fk_hearings_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessments",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: true),
                    probability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estimated_loss = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    assessed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_risk_assessments_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "settlements",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    agreed_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    homologated_on = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_settlements_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hearing_reschedules",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hearing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    new_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hearing_reschedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_hearing_reschedules_hearings_hearing_id",
                        column: x => x.hearing_id,
                        principalSchema: "litigation",
                        principalTable: "hearings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "settlement_installments",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settlement_installments", x => x.id);
                    table.ForeignKey(
                        name: "fk_settlement_installments_settlements_settlement_id",
                        column: x => x.settlement_id,
                        principalSchema: "litigation",
                        principalTable: "settlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "settlement_offers",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round = table.Column<int>(type: "integer", nullable: false),
                    party = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    terms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    offered_on = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settlement_offers", x => x.id);
                    table.ForeignKey(
                        name: "fk_settlement_offers_settlements_settlement_id",
                        column: x => x.settlement_id,
                        principalSchema: "litigation",
                        principalTable: "settlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hearing_reschedules_hearing_id",
                schema: "litigation",
                table: "hearing_reschedules",
                column: "hearing_id");

            migrationBuilder.CreateIndex(
                name: "ix_hearings_legal_case_id",
                schema: "litigation",
                table: "hearings",
                column: "legal_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_hearings_organization_id_legal_case_id_scheduled_at",
                schema: "litigation",
                table: "hearings",
                columns: new[] { "organization_id", "legal_case_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_hearings_organization_id_scheduled_at",
                schema: "litigation",
                table: "hearings",
                columns: new[] { "organization_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessments_legal_case_id",
                schema: "litigation",
                table: "risk_assessments",
                column: "legal_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessments_organization_id_legal_case_id_assessed_at",
                schema: "litigation",
                table: "risk_assessments",
                columns: new[] { "organization_id", "legal_case_id", "assessed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_settlement_installments_organization_id_due_on",
                schema: "litigation",
                table: "settlement_installments",
                columns: new[] { "organization_id", "due_on" });

            migrationBuilder.CreateIndex(
                name: "ix_settlement_installments_settlement_id_number",
                schema: "litigation",
                table: "settlement_installments",
                columns: new[] { "settlement_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_settlement_offers_settlement_id_round",
                schema: "litigation",
                table: "settlement_offers",
                columns: new[] { "settlement_id", "round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_settlements_legal_case_id",
                schema: "litigation",
                table: "settlements",
                column: "legal_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_settlements_organization_id_legal_case_id",
                schema: "litigation",
                table: "settlements",
                columns: new[] { "organization_id", "legal_case_id" });

            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "risk_assessments"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "settlements"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "settlement_offers"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "settlement_installments"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "hearings"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "hearing_reschedules"));
            migrationBuilder.Sql($"REVOKE UPDATE, DELETE, TRUNCATE ON litigation.risk_assessments FROM {Security.ApplicationRole};");
            migrationBuilder.Sql($"REVOKE UPDATE, DELETE, TRUNCATE ON litigation.hearing_reschedules FROM {Security.ApplicationRole};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "hearing_reschedules"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "hearings"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "settlement_installments"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "settlement_offers"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "settlements"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "risk_assessments"));

            migrationBuilder.DropTable(
                name: "hearing_reschedules",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "risk_assessments",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "settlement_installments",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "settlement_offers",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "hearings",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "settlements",
                schema: "litigation");
        }
    }
}
