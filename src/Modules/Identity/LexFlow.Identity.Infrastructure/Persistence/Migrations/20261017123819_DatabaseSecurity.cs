using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Privilégios do papel da aplicação no schema <c>identity</c> (Etapa 3, ADR 0019). Sem RLS: contas, sessões,
    /// tokens e chaves são da plataforma, não de um escritório.
    /// </summary>
    public partial class DatabaseSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.CreateApplicationRole());
            migrationBuilder.Sql(Security.GrantSchema("identity"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.RevokeSchema("identity"));
        }
    }
}
