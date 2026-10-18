using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Access;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>Ações em lote sobre processos selecionados (Etapa 5, tarefa 7).</summary>
public enum CaseBatchAction
{
    /// <summary>Inclui <c>ToUserId</c> como responsável; com <c>FromUserId</c>, substitui esse responsável.</summary>
    Redistribute,

    ChangePhase,
    Suspend,
    Reactivate,
}

public sealed record CaseBatchCommand(
    IReadOnlyList<Guid> CaseIds,
    CaseBatchAction Action,
    Guid? FromUserId,
    Guid? ToUserId,
    AssignmentRole? Role,
    Guid? PhaseId,
    string? Note) : ICommand<CaseBatchResult>;

/// <summary>Um processo que ficou de fora do lote e por quê (código estável).</summary>
public sealed record CaseBatchSkip(Guid CaseId, string Code);

public sealed record CaseBatchResult(int Updated, IReadOnlyList<CaseBatchSkip> Skipped);

internal sealed class CaseBatchValidator : AbstractValidator<CaseBatchCommand>
{
    public const int MaxCases = 1000;

    public CaseBatchValidator()
    {
        RuleFor(c => c.CaseIds).NotEmpty().Must(ids => ids.Count <= MaxCases).WithErrorCode(LitigationErrors.BatchInvalid.Code);
        RuleFor(c => c.Action).IsInEnum().WithErrorCode(LitigationErrors.BatchInvalid.Code);
        RuleFor(c => c.ToUserId).NotEmpty().When(c => c.Action == CaseBatchAction.Redistribute).WithErrorCode(LitigationErrors.BatchInvalid.Code);
        RuleFor(c => c.Role).IsInEnum().When(c => c.Role is not null).WithErrorCode(LitigationErrors.AssignmentInvalid.Code);
        RuleFor(c => c.Note).MaximumLength(CaseStatusChange.MaxNoteLength).WithErrorCode("litigation.text_too_long");
    }
}

/// <summary>
/// Aplica a ação aos processos ao alcance de quem edita (escopo e sigilo, com <c>cases.edit</c>). Os demais, os
/// encerrados e os que a ação não alcança ficam de fora, com o motivo; os outros são gravados juntos, na transação do
/// caso de uso. Mudanças de situação entram no histórico (linha do tempo).
/// </summary>
internal sealed class CaseBatchHandler(
    ILitigationDbContext db,
    CaseAccess access,
    CaseWrites writes,
    IMembershipLookup memberships,
    ITenantContext tenant,
    ILookupItemValidator lookups) : ICommandHandler<CaseBatchCommand, CaseBatchResult>
{
    public async Task<Result<CaseBatchResult>> Handle(CaseBatchCommand command, CancellationToken cancellationToken)
    {
        var target = await CheckTargetAsync(command, cancellationToken);
        if (target.IsFailure)
        {
            return target.Error!;
        }

        var ids = command.CaseIds.Distinct().ToList();
        var context = await access.ContextAsync(CasePermissions.Edit, cancellationToken);
        var cases = await CaseAccess.Visible(db.Cases, context).Include(c => c.Assignments)
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var skipped = ids.Except(cases.Select(c => c.Id)).Select(id => new CaseBatchSkip(id, LitigationErrors.CaseNotFound.Code)).ToList();
        var updated = 0;
        foreach (var legalCase in cases)
        {
            var before = legalCase.Status;
            var done = Apply(legalCase, command);
            if (done.IsFailure)
            {
                skipped.Add(new CaseBatchSkip(legalCase.Id, done.Error!.Code));
                continue;
            }

            writes.RecordStatus(legalCase.Id, before, legalCase.Status, command.Note);
            updated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new CaseBatchResult(updated, skipped);
    }

    private static Result Apply(LegalCase legalCase, CaseBatchCommand command) => command.Action switch
    {
        CaseBatchAction.Redistribute => legalCase.Redistribute(command.FromUserId, command.ToUserId!.Value, command.Role ?? AssignmentRole.Lawyer),
        CaseBatchAction.ChangePhase => legalCase.ChangePhase(command.PhaseId),
        CaseBatchAction.Suspend => legalCase.SetStatus(CaseStatus.Suspended),
        _ => legalCase.SetStatus(CaseStatus.Active),
    };

    /// <summary>O responsável novo precisa ser membro do escritório; a fase, um item ativo da tabela auxiliar.</summary>
    private async Task<Result> CheckTargetAsync(CaseBatchCommand command, CancellationToken cancellationToken)
    {
        if (command.Action == CaseBatchAction.Redistribute
            && !(await memberships.GetAsync(command.ToUserId!.Value, cancellationToken)).OrganizationIds.Contains(tenant.RequiredOrganizationId))
        {
            return LitigationErrors.ResponsibleNotMember;
        }

        if (command.Action == CaseBatchAction.ChangePhase && command.PhaseId is { } phase
            && !await lookups.IsActiveItemAsync(SystemLookupTables.CasePhases, phase, cancellationToken))
        {
            return LitigationErrors.PhaseNotFound;
        }

        return Result.Success;
    }
}
