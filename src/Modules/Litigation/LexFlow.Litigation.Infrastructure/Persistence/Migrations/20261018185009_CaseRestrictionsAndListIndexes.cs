using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Litigation.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Etapa 5, PR 5B: listas de permitidos e bloqueados dos casos sigilosos (com RLS), índices da lista por área e por
    /// filial na ordem de cadastro, e as chaves das partes e responsáveis marcadas como geradas pela aplicação (só no
    /// modelo, sem SQL).
    /// </summary>
    public partial class CaseRestrictionsAndListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_restrictions",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_restrictions", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_restrictions_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_branch_id_created_at",
                schema: "litigation",
                table: "cases",
                columns: new[] { "organization_id", "branch_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_practice_area_id_created_at",
                schema: "litigation",
                table: "cases",
                columns: new[] { "organization_id", "practice_area_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_case_restrictions_legal_case_id",
                schema: "litigation",
                table: "case_restrictions",
                column: "legal_case_id");

            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "case_restrictions"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "case_restrictions"));

            migrationBuilder.DropTable(
                name: "case_restrictions",
                schema: "litigation");

            migrationBuilder.DropIndex(
                name: "ix_cases_organization_id_branch_id_created_at",
                schema: "litigation",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "ix_cases_organization_id_practice_area_id_created_at",
                schema: "litigation",
                table: "cases");
        }
    }
}
