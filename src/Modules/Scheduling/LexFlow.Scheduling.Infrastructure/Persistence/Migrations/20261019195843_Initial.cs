using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        private static readonly string[] TenantTables = ["deadlines", "tasks"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.CreateTable(
                name: "deadlines",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    start_on = table.Column<DateOnly>(type: "date", nullable: false),
                    internal_due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    fatal_due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deadlines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_integration_events",
                schema: "scheduling",
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
                name: "tasks",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deadlines_organization_id_legal_case_id_fatal_due_on",
                schema: "scheduling",
                table: "deadlines",
                columns: new[] { "organization_id", "legal_case_id", "fatal_due_on" });

            migrationBuilder.CreateIndex(
                name: "ix_deadlines_organization_id_publication_id",
                schema: "scheduling",
                table: "deadlines",
                columns: new[] { "organization_id", "publication_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_organization_id_legal_case_id_due_on",
                schema: "scheduling",
                table: "tasks",
                columns: new[] { "organization_id", "legal_case_id", "due_on" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_organization_id_publication_id",
                schema: "scheduling",
                table: "tasks",
                columns: new[] { "organization_id", "publication_id" },
                unique: true);
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", table));
            }
            migrationBuilder.Sql(Security.GrantSchema("scheduling"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", table));
            }
            migrationBuilder.Sql(Security.RevokeSchema("scheduling"));
            migrationBuilder.DropTable(
                name: "deadlines",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "processed_integration_events",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "scheduling");
        }
    }
}
