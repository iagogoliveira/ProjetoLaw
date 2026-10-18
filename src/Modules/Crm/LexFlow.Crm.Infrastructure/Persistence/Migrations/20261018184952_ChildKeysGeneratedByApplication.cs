using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Crm.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Etapa 5, PR 5B: endereços, contatos e itens de proposta têm o id gerado no domínio; o modelo passa a dizer isso
    /// (sem SQL), para que uma filha nova numa edição seja inserida e não tratada como alteração.
    /// </summary>
    public partial class ChildKeysGeneratedByApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
