using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Organization.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_organizations",
                schema: "org",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_selected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_organizations", x => new { x.user_id, x.organization_id });
                    table.ForeignKey(
                        name: "fk_user_organizations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "org",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Pertenças criadas antes do diretório (onboarding do PR 3A).
            migrationBuilder.Sql(
                "INSERT INTO org.user_organizations (user_id, organization_id) " +
                "SELECT user_id, organization_id FROM org.members WHERE NOT is_deleted ON CONFLICT DO NOTHING;");

            migrationBuilder.CreateIndex(
                name: "ix_user_organizations_organization_id",
                schema: "org",
                table: "user_organizations",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_organizations",
                schema: "org");
        }
    }
}
