using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MfaMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mfa_method",
                schema: "identity",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mfa_method",
                schema: "identity",
                table: "users");
        }
    }
}
