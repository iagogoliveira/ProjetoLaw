using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Claims;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Claims;

/// <summary>Valores de uma instância e, com a data de atualização, os mesmos valores corrigidos com juros.</summary>
public sealed record ClaimValuationResponse(
    CaseInstance Instance,
    decimal? Requested,
    decimal? Estimated,
    decimal? Awarded,
    decimal? UpdatedRequested,
    decimal? UpdatedEstimated,
    decimal? UpdatedAwarded);

/// <summary>
/// Pedido com os valores por instância. <see cref="CorrectionError"/> traz o código do erro quando a atualização foi
/// pedida e não pôde ser feita (ex.: <c>platform.monetary_index_missing</c>); sem correção configurada, os valores
/// atualizados ficam nulos.
/// </summary>
public sealed record ClaimResponse(
    Guid Id,
    Guid CaseId,
    Guid ClaimTypeId,
    string? Description,
    CorrectionSettings? Correction,
    IReadOnlyList<ClaimValuationResponse> Valuations,
    string? CorrectionError,
    DateTimeOffset CreatedAt);

/// <summary>Soma dos pedidos numa instância. Os totais atualizados contam pelo valor original os pedidos sem correção.</summary>
public sealed record ClaimTotalsResponse(
    CaseInstance Instance,
    decimal Requested,
    decimal Estimated,
    decimal Awarded,
    decimal? UpdatedRequested,
    decimal? UpdatedEstimated,
    decimal? UpdatedAwarded);

public sealed record CaseClaimsResponse(DateOnly? UpdatedTo, IReadOnlyList<ClaimResponse> Items, IReadOnlyList<ClaimTotalsResponse> Totals);

/// <summary>Uma alteração de valor do pedido, para a auditoria (RF-13).</summary>
public sealed record ClaimValueChangeResponse(CaseInstance Instance, ClaimValueKind Kind, decimal? OldAmount, decimal? NewAmount, Guid ChangedBy, DateTimeOffset ChangedAt);

/// <summary>Os pedidos do processo; com <see cref="UpdateTo"/>, os valores atualizados até essa data.</summary>
public sealed record ListCaseClaimsQuery(Guid CaseId, DateOnly? UpdateTo) : IQuery<CaseClaimsResponse>;

public sealed record CreateClaimCommand(Guid CaseId, ClaimInput Claim) : ICommand<ClaimResponse>;

public sealed record UpdateClaimCommand(Guid CaseId, Guid ClaimId, ClaimInput Claim) : ICommand<ClaimResponse>;

public sealed record DeleteClaimCommand(Guid CaseId, Guid ClaimId) : ICommand;

public sealed record GetClaimHistoryQuery(Guid CaseId, Guid ClaimId) : IQuery<IReadOnlyList<ClaimValueChangeResponse>>;

internal sealed class ClaimInputValidator : AbstractValidator<ClaimInput>
{
    public ClaimInputValidator()
    {
        RuleFor(c => c.ClaimTypeId).NotEmpty().WithErrorCode(LitigationErrors.ClaimTypeNotFound.Code);
        RuleFor(c => c.Description).MaximumLength(Claim.MaxDescriptionLength).WithErrorCode("litigation.text_too_long");
        RuleFor(c => c.Correction!.Index).IsInEnum().When(c => c.Correction is not null).WithErrorCode(LitigationErrors.ClaimCorrectionInvalid.Code);
        RuleFor(c => c.Valuations).Must(v => v is null || v.Count <= Enum.GetValues<CaseInstance>().Length)
            .WithErrorCode(LitigationErrors.ValuationInstanceDuplicated.Code);
        RuleForEach(c => c.Valuations).ChildRules(v => v.RuleFor(x => x.Instance).IsInEnum().WithErrorCode("litigation.instance_invalid"));
    }
}

internal sealed class CreateClaimValidator : AbstractValidator<CreateClaimCommand>
{
    public CreateClaimValidator()
    {
        RuleFor(c => c.Claim).NotNull().SetValidator(new ClaimInputValidator());
    }
}

internal sealed class UpdateClaimValidator : AbstractValidator<UpdateClaimCommand>
{
    public UpdateClaimValidator()
    {
        RuleFor(c => c.Claim).NotNull().SetValidator(new ClaimInputValidator());
    }
}

internal sealed class ListCaseClaimsHandler(ILitigationDbContext db, ClaimResponseBuilder responses) : IQueryHandler<ListCaseClaimsQuery, CaseClaimsResponse>
{
    public async Task<Result<CaseClaimsResponse>> Handle(ListCaseClaimsQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Cases.AnyAsync(c => c.Id == query.CaseId, cancellationToken))
        {
            return LitigationErrors.CaseNotFound;
        }

        var claims = await db.Claims.AsNoTracking().Include(c => c.Valuations)
            .Where(c => c.LegalCaseId == query.CaseId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        var items = await responses.BuildAsync(claims, query.UpdateTo, cancellationToken);
        return new CaseClaimsResponse(query.UpdateTo, items, ClaimResponseBuilder.Totals(items, query.UpdateTo is not null));
    }
}

internal sealed class CreateClaimHandler(ILitigationDbContext db, ClaimWrites writes, IIdGenerator ids, ClaimResponseBuilder responses)
    : ICommandHandler<CreateClaimCommand, ClaimResponse>
{
    public async Task<Result<ClaimResponse>> Handle(CreateClaimCommand command, CancellationToken cancellationToken)
    {
        var allowed = await writes.CheckAsync(command.CaseId, command.Claim, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        var claim = Claim.Create(ids.NewId(), command.CaseId, command.Claim, writes.UserId, writes.Now);
        if (claim.IsFailure)
        {
            return claim.Error;
        }

        db.Claims.Add(claim.Value);
        await db.SaveChangesAsync(cancellationToken);
        return (await responses.BuildAsync([claim.Value], null, cancellationToken))[0];
    }
}

internal sealed class UpdateClaimHandler(ILitigationDbContext db, ClaimWrites writes, ClaimResponseBuilder responses)
    : ICommandHandler<UpdateClaimCommand, ClaimResponse>
{
    public async Task<Result<ClaimResponse>> Handle(UpdateClaimCommand command, CancellationToken cancellationToken)
    {
        var allowed = await writes.CheckAsync(command.CaseId, command.Claim, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        var claim = await db.Claims.Include(c => c.Valuations)
            .SingleOrDefaultAsync(c => c.Id == command.ClaimId && c.LegalCaseId == command.CaseId, cancellationToken);
        if (claim is null)
        {
            return LitigationErrors.ClaimNotFound;
        }

        var updated = claim.Update(command.Claim, writes.UserId, writes.Now);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await responses.BuildAsync([claim], null, cancellationToken))[0];
    }
}

internal sealed class DeleteClaimHandler(ILitigationDbContext db, ClaimWrites writes) : ICommandHandler<DeleteClaimCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteClaimCommand command, CancellationToken cancellationToken)
    {
        var open = await writes.CheckCaseAsync(command.CaseId, cancellationToken);
        if (open.IsFailure)
        {
            return open.Error;
        }

        var claim = await db.Claims.SingleOrDefaultAsync(c => c.Id == command.ClaimId && c.LegalCaseId == command.CaseId, cancellationToken);
        if (claim is null)
        {
            return LitigationErrors.ClaimNotFound;
        }

        db.Claims.Remove(claim);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal sealed class GetClaimHistoryHandler(ILitigationDbContext db) : IQueryHandler<GetClaimHistoryQuery, IReadOnlyList<ClaimValueChangeResponse>>
{
    public async Task<Result<IReadOnlyList<ClaimValueChangeResponse>>> Handle(GetClaimHistoryQuery query, CancellationToken cancellationToken)
    {
        var claim = await db.Claims.AsNoTracking().Include(c => c.Changes)
            .SingleOrDefaultAsync(c => c.Id == query.ClaimId && c.LegalCaseId == query.CaseId, cancellationToken);
        if (claim is null)
        {
            return LitigationErrors.ClaimNotFound;
        }

        return claim.Changes.OrderBy(c => c.ChangedAt).ThenBy(c => c.Instance).ThenBy(c => c.Kind)
            .Select(c => new ClaimValueChangeResponse(c.Instance, c.Kind, c.OldAmount, c.NewAmount, c.ChangedBy, c.ChangedAt))
            .ToList();
    }
}

/// <summary>Conferência própria dos pedidos: além do processo aberto, o objeto do pedido precisa estar ativo.</summary>
internal sealed class ClaimWrites(CaseWrites cases, ILookupItemValidator lookups)
{
    public Guid UserId => cases.UserId;

    public DateTimeOffset Now => cases.Now;

    public Task<Result> CheckCaseAsync(Guid caseId, CancellationToken cancellationToken) => cases.CheckOpenAsync(caseId, cancellationToken);

    public async Task<Result> CheckAsync(Guid caseId, ClaimInput input, CancellationToken cancellationToken)
    {
        var open = await cases.CheckOpenAsync(caseId, cancellationToken);
        if (open.IsFailure)
        {
            return open;
        }

        return await lookups.IsActiveItemAsync(SystemLookupTables.Claims, input.ClaimTypeId, cancellationToken)
            ? Result.Success
            : LitigationErrors.ClaimTypeNotFound;
    }
}
