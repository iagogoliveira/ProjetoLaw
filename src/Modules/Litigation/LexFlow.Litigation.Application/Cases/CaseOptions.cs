using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>Uma opção de cadastro: o id e o nome.</summary>
public sealed record CaseOption(Guid Id, string Name);

/// <summary>Um membro do escritório que pode ser responsável: o usuário, o nome e o e-mail.</summary>
public sealed record CaseMemberOption(Guid UserId, string Name, string Email);

/// <summary>
/// As opções das telas de processos (5E1): áreas, filiais, equipes e membros do escritório, e os itens ativos das
/// tabelas auxiliares dos processos. Só id e nome: quem cadastra processos não precisa das permissões de configuração.
/// </summary>
public sealed record CaseOptionsResponse(
    IReadOnlyList<CaseOption> PracticeAreas,
    IReadOnlyList<CaseOption> Branches,
    IReadOnlyList<CaseOption> Teams,
    IReadOnlyList<CaseMemberOption> Members,
    IReadOnlyList<CaseOption> Phases,
    IReadOnlyList<CaseOption> ActionTypes,
    IReadOnlyList<CaseOption> ClosingReasons,
    IReadOnlyList<CaseOption> ClaimTypes);

public sealed record GetCaseOptionsQuery : IQuery<CaseOptionsResponse>;

internal sealed class GetCaseOptionsHandler(IOrganizationDirectory organization, IUserDirectory users, ILookupItems lookups)
    : IQueryHandler<GetCaseOptionsQuery, CaseOptionsResponse>
{
    public async Task<Result<CaseOptionsResponse>> Handle(GetCaseOptionsQuery query, CancellationToken cancellationToken)
    {
        async Task<IReadOnlyList<CaseOption>> Units(OrgUnitKind kind) =>
            [.. (await organization.ListAsync(kind, cancellationToken)).OrderBy(u => u.Name, StringComparer.CurrentCulture).Select(u => new CaseOption(u.Id, u.Name))];

        async Task<IReadOnlyList<CaseOption>> Items(string table) =>
            [.. (await lookups.ActiveItemsAsync(table, cancellationToken)).Select(i => new CaseOption(i.Id, i.Name))];

        var members = await users.GetManyAsync(await organization.MemberUserIdsAsync(cancellationToken), cancellationToken);
        return new CaseOptionsResponse(
            await Units(OrgUnitKind.PracticeArea),
            await Units(OrgUnitKind.Branch),
            await Units(OrgUnitKind.Team),
            [.. members.OrderBy(m => m.FullName, StringComparer.CurrentCulture).Select(m => new CaseMemberOption(m.Id, m.FullName, m.Email))],
            await Items(SystemLookupTables.CasePhases),
            await Items(SystemLookupTables.ActionTypes),
            await Items(SystemLookupTables.ClosingReasons),
            await Items(SystemLookupTables.Claims));
    }
}
