using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexFlow.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration de dados (Etapa 4, PR 4C): as tabelas auxiliares novas do sistema, "Origens de oportunidade" e "Motivos
    /// de perda", com os itens sugeridos, nos escritórios que já passaram pelo onboarding (os novos as recebem do
    /// <c>LookupTableSeeder</c>). Roda como dono das tabelas, fora da RLS. Escritório que já tenha uma tabela com o mesmo
    /// nome fica sem a do sistema (o nome é único): renomeie a dele e rode o <c>LookupTableSeeder</c>.
    /// </summary>
    public partial class OpportunityLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH organizations AS (
                    SELECT DISTINCT organization_id FROM platform.lookup_tables WHERE system_key IS NOT NULL
                ),
                new_tables AS (
                    INSERT INTO platform.lookup_tables (id, organization_id, name, system_key, is_active, created_at, created_by)
                    SELECT gen_random_uuid(), o.organization_id, d.name, d.system_key, TRUE, now(), '00000000-0000-0000-0000-000000000000'
                    FROM organizations o
                    CROSS JOIN (VALUES ('opportunity_origins', 'Origens de oportunidade'), ('loss_reasons', 'Motivos de perda')) AS d (system_key, name)
                    WHERE NOT EXISTS (
                        SELECT 1 FROM platform.lookup_tables t WHERE t.organization_id = o.organization_id AND t.system_key = d.system_key)
                    ON CONFLICT DO NOTHING
                    RETURNING id, organization_id, system_key
                )
                INSERT INTO platform.lookup_items (id, organization_id, table_id, name, position, is_active, created_at, created_by)
                SELECT gen_random_uuid(), n.organization_id, n.id, i.name, i.position, TRUE, now(), '00000000-0000-0000-0000-000000000000'
                FROM new_tables n
                JOIN (VALUES
                    ('opportunity_origins', 'Indicação de cliente', 1),
                    ('opportunity_origins', 'Indicação de parceiro', 2),
                    ('opportunity_origins', 'Site', 3),
                    ('opportunity_origins', 'Redes sociais', 4),
                    ('opportunity_origins', 'Evento ou palestra', 5),
                    ('opportunity_origins', 'Prospecção ativa', 6),
                    ('opportunity_origins', 'Cliente atual (novo caso)', 7),
                    ('loss_reasons', 'Preço', 1),
                    ('loss_reasons', 'Escolheu outro escritório', 2),
                    ('loss_reasons', 'Desistiu da demanda', 3),
                    ('loss_reasons', 'Sem retorno do interessado', 4),
                    ('loss_reasons', 'Conflito de interesses', 5),
                    ('loss_reasons', 'Fora da área de atuação', 6)
                ) AS i (system_key, name, position) ON i.system_key = n.system_key;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM platform.lookup_items WHERE table_id IN (
                    SELECT id FROM platform.lookup_tables WHERE system_key IN ('opportunity_origins', 'loss_reasons'));
                DELETE FROM platform.lookup_tables WHERE system_key IN ('opportunity_origins', 'loss_reasons');
                """);
        }
    }
}
