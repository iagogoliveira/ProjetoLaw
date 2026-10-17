using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Organization.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration de dados (Etapa 4, PR 4B): a permissão nova <c>crm.people.anonymize</c> (anonimização LGPD) vai para os
    /// perfis padrão Administrador do escritório e Sócio dos escritórios já criados, como no onboarding (decisão de
    /// 2026-10-17). Roda como dono das tabelas, fora da RLS. Perfis criados pelo escritório não mudam.
    /// </summary>
    public partial class PeopleAnonymizePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO org.role_permissions (role_id, permission, organization_id)
                SELECT r.id, 'crm.people.anonymize', r.organization_id
                FROM org.roles r
                WHERE r.system_key IN ('office_admin', 'partner') AND NOT r.is_deleted
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM org.role_permissions WHERE permission = 'crm.people.anonymize';");
        }
    }
}
