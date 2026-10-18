using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Organization.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration de dados (Etapa 5, PR 5C2): a permissão nova <c>cases.risk.assess</c> (avaliação de risco, decisão de
    /// 2026-10-18) vai para os perfis padrão Administrador do escritório e Sócio dos escritórios já criados, como no
    /// onboarding. Roda como dono das tabelas, fora da RLS. Perfis criados pelo escritório não mudam.
    /// </summary>
    public partial class RiskAssessPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO org.role_permissions (role_id, permission, organization_id)
                SELECT r.id, 'cases.risk.assess', r.organization_id
                FROM org.roles r
                WHERE r.system_key IN ('office_admin', 'partner') AND NOT r.is_deleted
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM org.role_permissions WHERE permission = 'cases.risk.assess';");
        }
    }
}
