using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgendaAndTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tenant policies are installed after all new tables have been created below.
            migrationBuilder.AlterColumn<Guid>(
                name: "publication_id",
                schema: "scheduling",
                table: "tasks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "legal_case_id",
                schema: "scheduling",
                table: "tasks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "scheduling",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "closed_by",
                schema: "scheduling",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "scheduling",
                table: "tasks",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_task_id",
                schema: "scheduling",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                schema: "scheduling",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.CreateTable(
                name: "calendar_events",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_checklist_items",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_checklist_items_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "scheduling",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_comments",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_comments_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "scheduling",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_organization_id_assignee_user_id_due_on",
                schema: "scheduling",
                table: "tasks",
                columns: new[] { "organization_id", "assignee_user_id", "due_on" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_parent_task_id",
                schema: "scheduling",
                table: "tasks",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_organization_id_assignee_user_id_starts_at",
                schema: "scheduling",
                table: "calendar_events",
                columns: new[] { "organization_id", "assignee_user_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_organization_id_starts_at",
                schema: "scheduling",
                table: "calendar_events",
                columns: new[] { "organization_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_task_checklist_items_organization_id_task_id",
                schema: "scheduling",
                table: "task_checklist_items",
                columns: new[] { "organization_id", "task_id" });

            migrationBuilder.CreateIndex(
                name: "ix_task_checklist_items_task_id",
                schema: "scheduling",
                table: "task_checklist_items",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_comments_organization_id_task_id_occurred_at",
                schema: "scheduling",
                table: "task_comments",
                columns: new[] { "organization_id", "task_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_task_comments_task_id",
                schema: "scheduling",
                table: "task_comments",
                column: "task_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_tasks_parent_task_id",
                schema: "scheduling",
                table: "tasks",
                column: "parent_task_id",
                principalSchema: "scheduling",
                principalTable: "tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "calendar_events"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "task_checklist_items"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "task_comments"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "task_comments"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "task_checklist_items"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "calendar_events"));
            migrationBuilder.DropForeignKey(
                name: "fk_tasks_tasks_parent_task_id",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "calendar_events",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "task_checklist_items",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "task_comments",
                schema: "scheduling");

            migrationBuilder.DropIndex(
                name: "ix_tasks_organization_id_assignee_user_id_due_on",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "ix_tasks_parent_task_id",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "closed_by",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "parent_task_id",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "scheduling",
                table: "tasks");

            migrationBuilder.AlterColumn<Guid>(
                name: "publication_id",
                schema: "scheduling",
                table: "tasks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "legal_case_id",
                schema: "scheduling",
                table: "tasks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
