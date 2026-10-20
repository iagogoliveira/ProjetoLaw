using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficeCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_entries",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    state_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    municipality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    court_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    recurs_yearly = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_entries_organization_id_starts_on",
                schema: "scheduling",
                table: "calendar_entries",
                columns: new[] { "organization_id", "starts_on" });
            migrationBuilder.Sql(Security.EnableTenantIsolation("scheduling", "calendar_entries"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("scheduling", "calendar_entries"));
            migrationBuilder.DropTable(
                name: "calendar_entries",
                schema: "scheduling");
        }
    }
}
