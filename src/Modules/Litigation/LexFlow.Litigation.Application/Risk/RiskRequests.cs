using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Risk;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Risk;

public sealed record RiskAssessmentResponse(
    Guid Id,
    Guid? ClaimId,
    RiskProbability Probability,
    RiskAccounting Accounting,
    decimal? EstimatedLoss,
    string? Rationale,
    Guid AssessedBy,
    DateTimeOffset AssessedAt);

/// <summary>
/// Risco atual do processo: a avaliação mais recente do processo inteiro e a de cada pedido. Os totais somam as
/// avaliações dos pedidos; sem nenhuma, valem a do processo. Provável entra em provisão e possível em passivo
/// contingente (CPC 25).
/// </summary>
public sealed record CaseRiskResponse(
    RiskAssessmentResponse? Case,
    IReadOnlyList<RiskAssessmentResponse> Claims,
    decimal Provision,
    decimal ContingentLiability);

public sealed record GetCaseRiskQuery(Guid CaseId) : IQuery<CaseRiskResponse>;

/// <summary>Todas as avaliações do processo, da mais recente para a mais antiga.</summary>
public sealed record GetRiskHistoryQuery(Guid CaseId) : IQuery<IReadOnlyList<RiskAssessmentResponse>>;

public sealed record AssessRiskCommand(Guid CaseId, RiskInput Assessment) : ICommand<RiskAssessmentResponse>;

internal sealed class AssessRiskValidator : AbstractValidator<AssessRiskCommand>
{
    public AssessRiskValidator()
    {
        RuleFor(c => c.Assessment).NotNull();
        RuleFor(c => c.Assessment.Probability).IsInEnum().WithErrorCode("litigation.risk_probability_invalid");
        RuleFor(c => c.Assessment.EstimatedLoss).GreaterThanOrEqualTo(0).WithErrorCode(LitigationErrors.ValueInvalid.Code);
        RuleFor(c => c.Assessment.Rationale).MaximumLength(RiskAssessment.MaxRationaleLength).WithErrorCode("litigation.text_too_long");
    }
}

internal sealed class AssessRiskHandler(ILitigationDbContext db, CaseWrites cases, IIdGenerator ids) : ICommandHandler<AssessRiskCommand, RiskAssessmentResponse>
{
    public async Task<Result<RiskAssessmentResponse>> Handle(AssessRiskCommand command, CancellationToken cancellationToken)
    {
        var open = await cases.CheckOpenAsync(command.CaseId, cancellationToken);
        if (open.IsFailure)
        {
            return open.Error!;
        }

        if (command.Assessment.ClaimId is { } claimId
            && !await db.Claims.AnyAsync(c => c.Id == claimId && c.LegalCaseId == command.CaseId, cancellationToken))
        {
            return LitigationErrors.ClaimNotFound;
        }

        var assessment = RiskAssessment.Record(ids.NewId(), command.CaseId, command.Assessment, cases.UserId, cases.Now);
        if (assessment.IsFailure)
        {
            return assessment.Error!;
        }

        db.RiskAssessments.Add(assessment.Value);
        await db.SaveChangesAsync(cancellationToken);
        return assessment.Value.ToResponse();
    }
}

internal sealed class GetCaseRiskHandler(ILitigationDbContext db, CaseWrites cases) : IQueryHandler<GetCaseRiskQuery, CaseRiskResponse>
{
    public async Task<Result<CaseRiskResponse>> Handle(GetCaseRiskQuery query, CancellationToken cancellationToken)
    {
        if (!await cases.ExistsAsync(query.CaseId, cancellationToken))
        {
            return LitigationErrors.CaseNotFound;
        }

        // Pedidos excluídos saem do risco atual (o histórico continua com eles).
        var liveClaims = db.Claims.Where(c => c.LegalCaseId == query.CaseId).Select(c => (Guid?)c.Id);
        var assessments = await db.RiskAssessments.AsNoTracking()
            .Where(r => r.LegalCaseId == query.CaseId && (r.ClaimId == null || liveClaims.Contains(r.ClaimId)))
            .ToListAsync(cancellationToken);
        var current = assessments.GroupBy(r => r.ClaimId)
            .Select(g => g.OrderByDescending(r => r.AssessedAt).ThenByDescending(r => r.Id).First())
            .ToList();
        var caseLevel = current.SingleOrDefault(r => r.ClaimId is null);
        var claims = current.Where(r => r.ClaimId is not null).OrderBy(r => r.AssessedAt).ToList();
        var counted = claims.Count > 0 ? claims : caseLevel is null ? [] : [caseLevel];

        return new CaseRiskResponse(
            caseLevel?.ToResponse(),
            [.. claims.Select(r => r.ToResponse())],
            counted.Where(r => r.Accounting == RiskAccounting.Provision).Sum(r => r.EstimatedLoss ?? 0m),
            counted.Where(r => r.Accounting == RiskAccounting.ContingentLiability).Sum(r => r.EstimatedLoss ?? 0m));
    }
}

internal sealed class GetRiskHistoryHandler(ILitigationDbContext db, CaseWrites cases) : IQueryHandler<GetRiskHistoryQuery, IReadOnlyList<RiskAssessmentResponse>>
{
    public async Task<Result<IReadOnlyList<RiskAssessmentResponse>>> Handle(GetRiskHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await cases.ExistsAsync(query.CaseId, cancellationToken))
        {
            return LitigationErrors.CaseNotFound;
        }

        var list = await db.RiskAssessments.AsNoTracking()
            .Where(r => r.LegalCaseId == query.CaseId)
            .OrderByDescending(r => r.AssessedAt).ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
        return list.Select(r => r.ToResponse()).ToList();
    }
}

internal static class RiskMapping
{
    public static RiskAssessmentResponse ToResponse(this RiskAssessment r) =>
        new(r.Id, r.ClaimId, r.Probability, r.Accounting, r.EstimatedLoss, r.Rationale, r.AssessedBy, r.AssessedAt);
}
