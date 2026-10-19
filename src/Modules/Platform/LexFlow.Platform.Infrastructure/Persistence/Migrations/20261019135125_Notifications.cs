using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_receipts",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_receipts_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "platform",
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_receipts_notification_id",
                schema: "platform",
                table: "notification_receipts",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_receipts_organization_id_notification_id_user_",
                schema: "platform",
                table: "notification_receipts",
                columns: new[] { "organization_id", "notification_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_organization_id_created_at_id",
                schema: "platform",
                table: "notifications",
                columns: new[] { "organization_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_organization_id_key",
                schema: "platform",
                table: "notifications",
                columns: new[] { "organization_id", "key" },
                unique: true);
            migrationBuilder.Sql(Security.EnableTenantIsolation("platform", "notifications"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("platform", "notification_receipts"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("platform", "notifications"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("platform", "notification_receipts"));

            migrationBuilder.DropTable(
                name: "notification_receipts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "platform");
        }
    }
}
