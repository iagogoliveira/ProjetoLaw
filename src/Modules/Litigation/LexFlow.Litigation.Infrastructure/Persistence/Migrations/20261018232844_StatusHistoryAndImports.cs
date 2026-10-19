using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Litigation.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Etapa 5, PR 5D: histórico das mudanças de situação dos processos (só inserções: a aplicação perde o UPDATE e o
    /// DELETE) e as importações por planilha com os erros das linhas, com RLS nas três tabelas.
    /// </summary>
    public partial class StatusHistoryAndImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_imports",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    invalid_rows = table.Column<int>(type: "integer", nullable: false),
                    imported_rows = table.Column<int>(type: "integer", nullable: false),
                    last_imported_row = table.Column<int>(type: "integer", nullable: false),
                    validation_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    preview = table.Column<string>(type: "jsonb", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "case_status_changes",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_status_changes_cases_legal_case_id",
                        column: x => x.legal_case_id,
                        principalSchema: "litigation",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "case_import_errors",
                schema: "litigation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row = table.Column<int>(type: "integer", nullable: false),
                    column = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_import_errors", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_import_errors_case_imports_import_id",
                        column: x => x.import_id,
                        principalSchema: "litigation",
                        principalTable: "case_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_case_import_errors_import_id_row",
                schema: "litigation",
                table: "case_import_errors",
                columns: new[] { "import_id", "row" });

            migrationBuilder.CreateIndex(
                name: "ix_case_imports_organization_id_created_at",
                schema: "litigation",
                table: "case_imports",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_case_status_changes_legal_case_id",
                schema: "litigation",
                table: "case_status_changes",
                column: "legal_case_id");

            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "case_status_changes"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "case_imports"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("litigation", "case_import_errors"));
            migrationBuilder.Sql($"REVOKE UPDATE, DELETE, TRUNCATE ON litigation.case_status_changes FROM {Security.ApplicationRole};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "case_import_errors"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "case_imports"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("litigation", "case_status_changes"));

            migrationBuilder.DropTable(
                name: "case_import_errors",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "case_status_changes",
                schema: "litigation");

            migrationBuilder.DropTable(
                name: "case_imports",
                schema: "litigation");
        }
    }
}
