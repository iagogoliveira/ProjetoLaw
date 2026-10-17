using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Crm.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Módulo Crm (Etapa 4, PR 4A): pessoas, papéis, endereços, contatos e grupos econômicos, com Row-Level Security
    /// em todas as tabelas de escritório e privilégios do papel da aplicação no schema.
    /// </summary>
    public partial class Initial : Migration
    {
        /// <summary>Toda tabela <c>ITenantOwned</c> do módulo (o teste de RLS confere que nenhuma ficou de fora).</summary>
        public static readonly string[] TenantTables = ["people", "person_roles", "addresses", "contacts", "economic_groups"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "economic_groups",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_economic_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_integration_events",
                schema: "crm",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_integration_events", x => new { x.event_id, x.consumer });
                });

            migrationBuilder.CreateTable(
                name: "people",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    alternate_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    search_name = table.Column<string>(type: "character varying(601)", maxLength: 601, nullable: false),
                    document = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    state_registration = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    municipal_registration = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    withholds_iss = table.Column<bool>(type: "boolean", nullable: false),
                    withholds_income_tax = table.Column<bool>(type: "boolean", nullable: false),
                    withholds_pis_cofins_csll = table.Column<bool>(type: "boolean", nullable: false),
                    economic_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_fields = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_people", x => x.id);
                    table.ForeignKey(
                        name: "fk_people_economic_groups_economic_group_id",
                        column: x => x.economic_group_id,
                        principalSchema: "crm",
                        principalTable: "economic_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "addresses",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    postal_code = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: false),
                    is_billing = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_addresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_addresses_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "crm",
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    preferred_channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_contacts_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "crm",
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "person_roles",
                schema: "crm",
                columns: table => new
                {
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_roles", x => new { x.person_id, x.role });
                    table.ForeignKey(
                        name: "fk_person_roles_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "crm",
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_addresses_organization_id_person_id",
                schema: "crm",
                table: "addresses",
                columns: new[] { "organization_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_addresses_person_id",
                schema: "crm",
                table: "addresses",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_organization_id_person_id",
                schema: "crm",
                table: "contacts",
                columns: new[] { "organization_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_contacts_person_id",
                schema: "crm",
                table: "contacts",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_economic_groups_organization_id_name",
                schema: "crm",
                table: "economic_groups",
                columns: new[] { "organization_id", "name" },
                unique: true,
                filter: "NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_people_economic_group_id",
                schema: "crm",
                table: "people",
                column: "economic_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_people_organization_id_document",
                schema: "crm",
                table: "people",
                columns: new[] { "organization_id", "document" },
                unique: true,
                filter: "document IS NOT NULL AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_people_organization_id_search_name",
                schema: "crm",
                table: "people",
                columns: new[] { "organization_id", "search_name" });

            migrationBuilder.CreateIndex(
                name: "ix_people_search_name",
                schema: "crm",
                table: "people",
                column: "search_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_person_roles_organization_id_role",
                schema: "crm",
                table: "person_roles",
                columns: new[] { "organization_id", "role" });

            migrationBuilder.Sql(Security.CreateApplicationRole());
            migrationBuilder.Sql(Security.GrantSchema("crm"));
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.EnableTenantIsolation("crm", table));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.DisableTenantIsolation("crm", table));
            }

            migrationBuilder.Sql(Security.RevokeSchema("crm"));

            migrationBuilder.DropTable(
                name: "addresses",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "contacts",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "person_roles",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "processed_integration_events",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "people",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "economic_groups",
                schema: "crm");
        }
    }
}
