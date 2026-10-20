using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Publications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaptureRunSourcePeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_period_id",
                schema: "publications",
                table: "capture_runs",
                type: "uuid",
                nullable: true);

            // Só associar históricos inequívocos; não alterar datas nem fechamentos assinados.
            migrationBuilder.Sql(
                """
                WITH candidates AS (
                    SELECT r.id AS run_id, p.id AS period_id,
                           count(*) OVER (PARTITION BY r.id) AS matches
                    FROM publications.capture_runs r
                    JOIN publications.capture_source_periods p
                        ON p.organization_id = r.organization_id AND p.source_id = r.source_id
                    JOIN publications.capture_sources s
                        ON s.organization_id = r.organization_id AND s.id = r.source_id
                    WHERE r.source_activated_at = p.activated_at
                        OR (p.deactivated_at IS NULL AND r.source_activated_at = s.activated_at)
                )
                UPDATE publications.capture_runs r SET source_period_id = c.period_id
                FROM candidates c WHERE c.run_id = r.id AND c.matches = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_capture_runs_source_period_id",
                schema: "publications",
                table: "capture_runs",
                column: "source_period_id");

            migrationBuilder.AddForeignKey(
                name: "fk_capture_runs_capture_source_periods_source_period_id",
                schema: "publications",
                table: "capture_runs",
                column: "source_period_id",
                principalSchema: "publications",
                principalTable: "capture_source_periods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_capture_runs_capture_source_periods_source_period_id",
                schema: "publications",
                table: "capture_runs");

            migrationBuilder.DropIndex(
                name: "ix_capture_runs_source_period_id",
                schema: "publications",
                table: "capture_runs");

            migrationBuilder.DropColumn(
                name: "source_period_id",
                schema: "publications",
                table: "capture_runs");
        }
    }
}
