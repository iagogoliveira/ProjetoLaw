using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>Campos customizados e tabelas auxiliares (Etapa 3, PR 3E, RF-03): dados de escritório, com RLS.</summary>
    public partial class CustomFieldsAndLookupTables : Migration
    {
        private static readonly string[] TenantTables = ["custom_field_definitions", "lookup_tables", "lookup_items"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lookup_tables",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    system_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lookup_tables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custom_field_definitions",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    help_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    filterable = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    max_length = table.Column<int>(type: "integer", nullable: true),
                    minimum = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    maximum = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    decimals = table.Column<int>(type: "integer", nullable: true),
                    lookup_table_id = table.Column<Guid>(type: "uuid", nullable: true),
                    allow_multiple = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_field_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_custom_field_definitions_lookup_tables_lookup_table_id",
                        column: x => x.lookup_table_id,
                        principalSchema: "platform",
                        principalTable: "lookup_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lookup_items",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lookup_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_lookup_items_lookup_tables_table_id",
                        column: x => x.table_id,
                        principalSchema: "platform",
                        principalTable: "lookup_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_field_definitions_lookup_table_id",
                schema: "platform",
                table: "custom_field_definitions",
                column: "lookup_table_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_field_definitions_organization_id_entity_key",
                schema: "platform",
                table: "custom_field_definitions",
                columns: new[] { "organization_id", "entity", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lookup_items_organization_id_table_id",
                schema: "platform",
                table: "lookup_items",
                columns: new[] { "organization_id", "table_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lookup_items_table_id",
                schema: "platform",
                table: "lookup_items",
                column: "table_id");

            migrationBuilder.CreateIndex(
                name: "ix_lookup_tables_organization_id_name",
                schema: "platform",
                table: "lookup_tables",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lookup_tables_organization_id_system_key",
                schema: "platform",
                table: "lookup_tables",
                columns: new[] { "organization_id", "system_key" },
                unique: true,
                filter: "system_key IS NOT NULL");

            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.EnableTenantIsolation("platform", table));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.DisableTenantIsolation("platform", table));
            }

            migrationBuilder.DropTable(
                name: "custom_field_definitions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "lookup_items",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "lookup_tables",
                schema: "platform");
        }
    }
}
