using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Crm.Infrastructure.Persistence.Migrations
{
    /// <summary>Etapa 4, PR 4B: verificações de conflito (com RLS), fusão de cadastros e anonimização LGPD.</summary>
    public partial class ConflictChecksMergeAndAnonymization : Migration
    {
        private static readonly string[] TenantTables = ["conflict_checks", "conflict_check_hits"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "anonymization_reason",
                schema: "crm",
                table: "people",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "anonymized_at",
                schema: "crm",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merged_into_id",
                schema: "crm",
                table: "people",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "conflict_checks",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    query_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    query_document = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    side = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    risk = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proceeded = table.Column<bool>(type: "boolean", nullable: false),
                    proceeded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflict_checks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conflict_check_hits",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matched_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    case_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflict_check_hits", x => x.id);
                    table.ForeignKey(
                        name: "fk_conflict_check_hits_conflict_checks_check_id",
                        column: x => x.check_id,
                        principalSchema: "crm",
                        principalTable: "conflict_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conflict_check_hits_check_id",
                schema: "crm",
                table: "conflict_check_hits",
                column: "check_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_check_hits_organization_id_person_id",
                schema: "crm",
                table: "conflict_check_hits",
                columns: new[] { "organization_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_conflict_checks_organization_id_created_at",
                schema: "crm",
                table: "conflict_checks",
                columns: new[] { "organization_id", "created_at" });

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
                name: "conflict_check_hits",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "conflict_checks",
                schema: "crm");

            migrationBuilder.DropColumn(
                name: "anonymization_reason",
                schema: "crm",
                table: "people");

            migrationBuilder.DropColumn(
                name: "anonymized_at",
                schema: "crm",
                table: "people");

            migrationBuilder.DropColumn(
                name: "merged_into_id",
                schema: "crm",
                table: "people");
        }
    }
}
