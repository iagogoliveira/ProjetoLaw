using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Organization.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Convites (Etapa 3, PR 3D). <c>invitations</c> e <c>invitation_roles</c> são dados de escritório, com RLS;
    /// <c>invitation_tokens</c> é o diretório token → convite, sem tenant (quem abre o link ainda não é membro).
    /// </summary>
    public partial class Invitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "org",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitation_roles",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitation_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitation_roles_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalSchema: "org",
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invitation_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "org",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitation_tokens",
                schema: "org",
                columns: table => new
                {
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitation_tokens", x => x.token_hash);
                    table.ForeignKey(
                        name: "fk_invitation_tokens_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalSchema: "org",
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invitation_tokens_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "org",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_roles_invitation_id",
                schema: "org",
                table: "invitation_roles",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitation_roles_organization_id_role_id",
                schema: "org",
                table: "invitation_roles",
                columns: new[] { "organization_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_roles_role_id",
                schema: "org",
                table: "invitation_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitation_tokens_invitation_id",
                schema: "org",
                table: "invitation_tokens",
                column: "invitation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitation_tokens_organization_id",
                schema: "org",
                table: "invitation_tokens",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_organization_id_email",
                schema: "org",
                table: "invitations",
                columns: new[] { "organization_id", "email" });

            migrationBuilder.Sql(Security.EnableTenantIsolation("org", "invitations"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("org", "invitation_roles"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.DisableTenantIsolation("org", "invitation_roles"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("org", "invitations"));

            migrationBuilder.DropTable(
                name: "invitation_roles",
                schema: "org");

            migrationBuilder.DropTable(
                name: "invitation_tokens",
                schema: "org");

            migrationBuilder.DropTable(
                name: "invitations",
                schema: "org");
        }
    }
}
