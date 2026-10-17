using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Organization.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Row-Level Security nas tabelas de escritório do schema <c>org</c> (Etapa 3, seção 6.2, item 5) e privilégios do
    /// papel da aplicação. Ficam sem RLS as tabelas da plataforma: <c>organizations</c> (o id é o próprio tenant e o
    /// usuário lista os nomes dos seus escritórios) e o diretório <c>user_organizations</c>.
    /// </summary>
    public partial class RowLevelSecurity : Migration
    {
        /// <summary>Toda tabela <c>ITenantOwned</c> do módulo (o teste de RLS confere que nenhuma ficou de fora).</summary>
        public static readonly string[] TenantTables = ["members", "roles", "role_permissions", "member_role_assignments", "branches", "practice_areas", "teams", "team_members", "cost_centers", "partners"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.CreateApplicationRole());
            migrationBuilder.Sql(Security.GrantSchema("org"));
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.EnableTenantIsolation("org", table));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql(Security.DisableTenantIsolation("org", table));
            }

            migrationBuilder.Sql(Security.RevokeSchema("org"));
        }
    }
}
