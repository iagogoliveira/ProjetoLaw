using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Crm.Infrastructure.Persistence.Migrations
{
    /// <summary>Etapa 4, PR 4C: funil de oportunidades, propostas com itens e modelos DOCX, todos com RLS por organização.</summary>
    public partial class OpportunitiesAndProposals : Migration
    {
        private static readonly string[] TenantTables = ["opportunities", "proposal_templates", "proposals", "proposal_items"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "opportunities",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prospect_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prospect_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    prospect_document = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    prospect_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    prospect_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    origin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estimated_value = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    expected_close_date = table.Column<DateOnly>(type: "date", nullable: true),
                    responsible_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stage_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    loss_reason_id = table.Column<Guid>(type: "uuid", nullable: true),
                    loss_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    conflict_check_id = table.Column<Guid>(type: "uuid", nullable: true),
                    won_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_opportunities", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunities_conflict_checks_conflict_check_id",
                        column: x => x.conflict_check_id,
                        principalSchema: "crm",
                        principalTable: "conflict_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opportunities_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "crm",
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposal_templates",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_proposal_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proposals",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scope = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    conditions = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    total = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    return_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    declined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decline_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_proposals", x => x.id);
                    table.ForeignKey(
                        name: "fk_proposals_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalSchema: "crm",
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_proposals_proposal_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "crm",
                        principalTable: "proposal_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposal_items",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proposal_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_proposal_items_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalSchema: "crm",
                        principalTable: "proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_conflict_check_id",
                schema: "crm",
                table: "opportunities",
                column: "conflict_check_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_organization_id_person_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "organization_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_organization_id_responsible_user_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "organization_id", "responsible_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_organization_id_stage_created_at",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "organization_id", "stage", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_person_id",
                schema: "crm",
                table: "opportunities",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposal_items_proposal_id",
                schema: "crm",
                table: "proposal_items",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposal_templates_organization_id_name",
                schema: "crm",
                table: "proposal_templates",
                columns: new[] { "organization_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_proposals_opportunity_id",
                schema: "crm",
                table: "proposals",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_organization_id_opportunity_id",
                schema: "crm",
                table: "proposals",
                columns: new[] { "organization_id", "opportunity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_proposals_organization_id_status_created_at",
                schema: "crm",
                table: "proposals",
                columns: new[] { "organization_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_proposals_template_id",
                schema: "crm",
                table: "proposals",
                column: "template_id");

            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.EnableTenantIsolation("crm", table));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.DisableTenantIsolation("crm", table));
            }

            migrationBuilder.DropTable(
                name: "proposal_items",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "proposals",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "opportunities",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "proposal_templates",
                schema: "crm");
        }
    }
}
