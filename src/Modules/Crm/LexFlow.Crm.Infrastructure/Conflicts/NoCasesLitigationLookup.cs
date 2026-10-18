using LexFlow.Crm.Application.Abstractions;

namespace LexFlow.Crm.Infrastructure.Conflicts;

/// <summary>
/// Stub até a Etapa 5 (decisão de 2026-10-17): ainda não há processos, então nenhuma parte é encontrada. O módulo de
/// processos registra a implementação real no lugar desta.
/// </summary>
internal sealed class NoCasesLitigationLookup : ILitigationLookup
{
    public Task<IReadOnlyList<CaseParty>> FindPartiesAsync(string searchName, string? document, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CaseParty>>([]);
}
