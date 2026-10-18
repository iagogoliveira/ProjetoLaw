using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>Encerramento (RF-09) com motivo da tabela auxiliar "Motivos de encerramento", resultado e valor final.</summary>
public sealed record CloseCaseCommand(Guid Id, ClosingInput Closing) : ICommand<CaseResponse>;

internal sealed class CloseCaseValidator : AbstractValidator<CloseCaseCommand>
{
    public CloseCaseValidator()
    {
        RuleFor(c => c.Closing).NotNull();
        RuleFor(c => c.Closing.ReasonId).NotEmpty().WithErrorCode(LitigationErrors.ClosingReasonNotFound.Code);
        RuleFor(c => c.Closing.Outcome).IsInEnum().WithErrorCode("litigation.outcome_invalid");
        RuleFor(c => c.Closing.FinalAmount).GreaterThanOrEqualTo(0).WithErrorCode(LitigationErrors.ValueInvalid.Code);
        RuleFor(c => c.Closing.Note).MaximumLength(LegalCase.MaxNoteLength).WithErrorCode("litigation.text_too_long");
    }
}

internal sealed class CloseCaseHandler(ILitigationDbContext db, ILookupItemValidator lookups, CaseResponseBuilder responses) : ICommandHandler<CloseCaseCommand, CaseResponse>
{
    public async Task<Result<CaseResponse>> Handle(CloseCaseCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.Include(c => c.Parties).Include(c => c.Assignments).SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (legalCase is null)
        {
            return LitigationErrors.CaseNotFound;
        }

        if (!await lookups.IsActiveItemAsync(SystemLookupTables.ClosingReasons, command.Closing.ReasonId, cancellationToken))
        {
            return LitigationErrors.ClosingReasonNotFound;
        }

        var closed = legalCase.Close(command.Closing);
        if (closed.IsFailure)
        {
            return closed.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await responses.BuildAsync(legalCase, cancellationToken);
    }
}

/// <summary>Reabre, suspende ou reativa o caso.</summary>
public sealed record ChangeCaseStatusCommand(Guid Id, CaseStatus? Status, bool Reopen) : ICommand<CaseResponse>;

internal sealed class ChangeCaseStatusHandler(ILitigationDbContext db, CaseResponseBuilder responses) : ICommandHandler<ChangeCaseStatusCommand, CaseResponse>
{
    public async Task<Result<CaseResponse>> Handle(ChangeCaseStatusCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.Include(c => c.Parties).Include(c => c.Assignments).SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (legalCase is null)
        {
            return LitigationErrors.CaseNotFound;
        }

        var changed = command.Reopen ? legalCase.Reopen() : legalCase.SetStatus(command.Status ?? CaseStatus.Active);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await responses.BuildAsync(legalCase, cancellationToken);
    }
}

/// <summary>Exclusão lógica; as relações de hierarquia do caso saem com ele.</summary>
public sealed record DeleteCaseCommand(Guid Id) : ICommand;

internal sealed class DeleteCaseHandler(ILitigationDbContext db) : ICommandHandler<DeleteCaseCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteCaseCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (legalCase is null)
        {
            return LitigationErrors.CaseNotFound;
        }

        db.Relations.RemoveRange(await db.Relations.Where(r => r.ParentCaseId == legalCase.Id || r.ChildCaseId == legalCase.Id).ToListAsync(cancellationToken));
        db.Cases.Remove(legalCase);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
