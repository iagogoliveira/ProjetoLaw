using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeadlineLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "publication_id",
                schema: "scheduling",
                table: "deadlines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "calculation_snapshot",
                schema: "scheduling",
                table: "deadlines",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "scheduling",
                table: "deadlines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "closed_by",
                schema: "scheduling",
                table: "deadlines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completion_evidence",
                schema: "scheduling",
                table: "deadlines",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "court_id",
                schema: "scheduling",
                table: "deadlines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_calendar_alert_at",
                schema: "scheduling",
                table: "deadlines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "missed_reason",
                schema: "scheduling",
                table: "deadlines",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "municipality",
                schema: "scheduling",
                table: "deadlines",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "require_review",
                schema: "scheduling",
                table: "deadlines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                schema: "scheduling",
                table: "deadlines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by",
                schema: "scheduling",
                table: "deadlines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "rule_consulted_on",
                schema: "scheduling",
                table: "deadlines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rule_days",
                schema: "scheduling",
                table: "deadlines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "rule_doubled",
                schema: "scheduling",
                table: "deadlines",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rule_internal_lead_days",
                schema: "scheduling",
                table: "deadlines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rule_mode",
                schema: "scheduling",
                table: "deadlines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rule_start_kind",
                schema: "scheduling",
                table: "deadlines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "rule_trigger_date",
                schema: "scheduling",
                table: "deadlines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state_code",
                schema: "scheduling",
                table: "deadlines",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "scheduling",
                table: "deadlines",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "procedural");

            migrationBuilder.CreateTable(
                name: "deadline_activities",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deadline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calendar_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deadline_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_deadline_activities_deadlines_deadline_id",
                        column: x => x.deadline_id,
                        principalSchema: "scheduling",
                        principalTable: "deadlines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deadline_activities_deadline_id",
                schema: "scheduling",
                table: "deadline_activities",
                column: "deadline_id");

            migrationBuilder.CreateIndex(
                name: "ix_deadline_activities_organization_id_legal_case_id_occurred_",
                schema: "scheduling",
                table: "deadline_activities",
                columns: new[] { "organization_id", "legal_case_id", "occurred_at" });
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "deadline_activities"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "deadline_activities"));
            migrationBuilder.DropTable(
                name: "deadline_activities",
                schema: "scheduling");

            migrationBuilder.DropColumn(
                name: "calculation_snapshot",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "closed_by",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "completion_evidence",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "court_id",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "last_calendar_alert_at",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "missed_reason",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "municipality",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "require_review",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "rule_consulted_on",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "rule_days",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "rule_doubled",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "rule_internal_lead_days",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "rule_mode",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "rule_start_kind",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "rule_trigger_date",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "state_code",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "scheduling",
                table: "deadlines");

            migrationBuilder.AlterColumn<Guid>(
                name: "publication_id",
                schema: "scheduling",
                table: "deadlines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
