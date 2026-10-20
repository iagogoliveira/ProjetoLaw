using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Organization.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleCalendarPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO org.role_permissions (role_id, permission, organization_id)
                SELECT r.id, 'schedule.calendar.manage', r.organization_id
                FROM org.roles r
                WHERE r.system_key IN ('office_admin', 'partner', 'legal_controller') AND NOT r.is_deleted
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM org.role_permissions WHERE permission = 'schedule.calendar.manage';");
        }
    }
}
