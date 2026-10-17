using LexFlow.Application.Abstractions;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Domain.CustomFields;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.CustomFields;

public sealed record DefaultLookupTable(string SystemKey, string Name, IReadOnlyList<string> Items);

/// <summary>
/// Tabelas auxiliares do sistema com os itens sugeridos (RF-03; lista na ADR 0021, para aprovação). O escritório
/// renomeia, reordena, desativa e acrescenta itens à vontade.
/// </summary>
public static class DefaultLookupTables
{
    public static IReadOnlyList<DefaultLookupTable> All { get; } =
    [
        new(SystemLookupTables.ActionTypes, "Tipos de ação",
        [
            "Reclamação trabalhista", "Ação de cobrança", "Ação de indenização por danos morais", "Ação de indenização por danos materiais",
            "Execução de título extrajudicial", "Execução fiscal", "Embargos à execução", "Mandado de segurança", "Ação revisional de contrato",
            "Ação de despejo", "Divórcio", "Inventário", "Ação de alimentos",
        ]),
        new(SystemLookupTables.CasePhases, "Fases",
        [
            "Pré-processual", "Conhecimento", "Recursal", "Execução / cumprimento de sentença", "Acordo em cumprimento", "Arquivado",
        ]),
        new(SystemLookupTables.Claims, "Objetos/pedidos",
        [
            "Horas extras", "Verbas rescisórias", "Adicional de insalubridade", "Adicional de periculosidade", "Reconhecimento de vínculo empregatício",
            "Danos morais", "Danos materiais", "Lucros cessantes", "Repetição de indébito", "Obrigação de fazer",
        ]),
        new(SystemLookupTables.ClosingReasons, "Motivos de encerramento",
        [
            "Acordo", "Procedência", "Procedência parcial", "Improcedência", "Extinção sem resolução do mérito", "Desistência",
            "Prescrição", "Pagamento / satisfação da obrigação", "Revogação do mandato",
        ]),
        new(SystemLookupTables.ExpenseTypes, "Tipos de despesa",
        [
            "Custas iniciais", "Custas recursais (preparo)", "Depósito recursal", "Honorários periciais", "Diligência de oficial de justiça",
            "Correspondente", "Cópias e autenticações", "Deslocamento", "Postagem",
        ]),
        new(SystemLookupTables.TaskTypes, "Tipos de tarefa",
        [
            "Elaborar petição", "Protocolar", "Preparar audiência", "Reunião com cliente", "Analisar documentos", "Elaborar cálculo",
            "Diligência", "Contato telefônico",
        ]),
    ];
}

/// <summary>
/// Cria, na organização ativa, as tabelas do sistema que faltarem, com os itens sugeridos. Idempotente: tabela que já
/// existe (mesmo renomeada ou com outros itens) não é tocada.
/// </summary>
public sealed class LookupTableSeeder(IPlatformDbContext db, IIdGenerator ids)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var existing = await db.LookupTables.Where(t => t.SystemKey != null).Select(t => t.SystemKey!).ToListAsync(cancellationToken);
        foreach (var definition in DefaultLookupTables.All.Where(d => !existing.Contains(d.SystemKey, StringComparer.Ordinal)))
        {
            var table = LookupTable.Create(ids.NewId(), definition.Name, definition.SystemKey);
            db.LookupTables.Add(table);
            db.LookupItems.AddRange(definition.Items.Select((name, index) => LookupItem.Create(ids.NewId(), table.Id, name, index + 1)));
        }
    }
}
