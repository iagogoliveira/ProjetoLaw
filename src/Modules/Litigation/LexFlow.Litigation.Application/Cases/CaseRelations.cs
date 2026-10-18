using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>
/// Liga um processo ao principal (RF-09: incidente, recurso, cumprimento de sentença, apenso). Cada processo tem no
/// máximo um principal, e a hierarquia não pode formar ciclo.
/// </summary>
public sealed record LinkCaseCommand(Guid ParentCaseId, Guid ChildCaseId, RelationKind Kind) : ICommand<CaseResponse>;

internal sealed class LinkCaseValidator : AbstractValidator<LinkCaseCommand>
{
    public LinkCaseValidator()
    {
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("litigation.relation_kind_invalid");
        RuleFor(c => c.ChildCaseId).NotEqual(c => c.ParentCaseId).WithErrorCode(LitigationErrors.RelationToItself.Code);
    }
}

internal sealed class LinkCaseHandler(ILitigationDbContext db, CaseResponseBuilder responses, IIdGenerator ids) : ICommandHandler<LinkCaseCommand, CaseResponse>
{
    /// <summary>Limite de níveis subidos ao procurar ciclo (hierarquias reais têm poucos).</summary>
    private const int MaxDepth = 50;

    public async Task<Result<CaseResponse>> Handle(LinkCaseCommand command, CancellationToken cancellationToken)
    {
        var both = await db.Cases.Include(c => c.Parties).Include(c => c.Assignments)
            .Where(c => c.Id == command.ParentCaseId || c.Id == command.ChildCaseId)
            .ToListAsync(cancellationToken);
        var parent = both.SingleOrDefault(c => c.Id == command.ParentCaseId);
        if (parent is null || both.Count != 2)
        {
            return LitigationErrors.CaseNotFound;
        }

        if (await db.Relations.AnyAsync(r => r.ChildCaseId == command.ChildCaseId, cancellationToken))
        {
            return LitigationErrors.RelationExists;
        }

        // Ciclo: o novo vinculado não pode estar acima do principal.
        var ancestor = (Guid?)command.ParentCaseId;
        for (var depth = 0; ancestor is { } current && depth < MaxDepth; depth++)
        {
            if (current == command.ChildCaseId)
            {
                return LitigationErrors.RelationCycle;
            }

            ancestor = await db.Relations.Where(r => r.ChildCaseId == current).Select(r => (Guid?)r.ParentCaseId).FirstOrDefaultAsync(cancellationToken);
        }

        db.Relations.Add(CaseRelation.Link(ids.NewId(), command.ParentCaseId, command.ChildCaseId, command.Kind));
        await db.SaveChangesAsync(cancellationToken);
        return await responses.BuildAsync(parent, cancellationToken);
    }
}

public sealed record UnlinkCaseCommand(Guid CaseId, Guid RelationId) : ICommand;

internal sealed class UnlinkCaseHandler(ILitigationDbContext db) : ICommandHandler<UnlinkCaseCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UnlinkCaseCommand command, CancellationToken cancellationToken)
    {
        if (!await db.Cases.AnyAsync(c => c.Id == command.CaseId, cancellationToken))
        {
            return LitigationErrors.CaseNotFound;
        }

        var relation = await db.Relations.SingleOrDefaultAsync(
            r => r.Id == command.RelationId && (r.ParentCaseId == command.CaseId || r.ChildCaseId == command.CaseId), cancellationToken);
        if (relation is null)
        {
            return LitigationErrors.RelationNotFound;
        }

        db.Relations.Remove(relation);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
