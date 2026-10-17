using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Organization.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration de dados (Etapa 3, PR 3E): a permissão nova <c>settings.custom_fields.read</c> vai para todos os perfis
    /// padrão dos escritórios já criados, como nos perfis do onboarding (decisão de 2026-10-17). Roda como dono das
    /// tabelas, fora da RLS. Perfis criados pelo escritório não mudam.
    /// </summary>
    public partial class CustomFieldsReadPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO org.role_permissions (role_id, permission, organization_id)
                SELECT r.id, 'settings.custom_fields.read', r.organization_id
                FROM org.roles r
                WHERE r.system_key IS NOT NULL AND NOT r.is_deleted
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM org.role_permissions WHERE permission = 'settings.custom_fields.read';");
        }
    }
}
