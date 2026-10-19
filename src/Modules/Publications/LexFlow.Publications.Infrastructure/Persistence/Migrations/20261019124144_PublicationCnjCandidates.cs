using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Publications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicationCnjCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "candidate_cnjs",
                schema: "publications",
                table: "publications",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "candidate_cnjs",
                schema: "publications",
                table: "publications");
        }
    }
}
