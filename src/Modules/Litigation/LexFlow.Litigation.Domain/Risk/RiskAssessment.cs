using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Domain.Risk;

/// <summary>Probabilidade de perda (CPC 25 / IAS 37).</summary>
public enum RiskProbability
{
    Probable,
    Possible,
    Remote,
}

/// <summary>Como a avaliação entra na contabilidade: provável é provisão; possível, passivo contingente (só divulgado); remoto, nada.</summary>
public enum RiskAccounting
{
    Provision,
    ContingentLiability,
    None,
}

/// <summary>Avaliação do processo inteiro (sem <see cref="ClaimId"/>) ou de um pedido dele.</summary>
public sealed record RiskInput(Guid? ClaimId, RiskProbability Probability, decimal? EstimatedLoss, string? Rationale);

/// <summary>
/// Avaliação de risco (RF-14): probabilidade, valor de perda estimado e justificativa, com autor e data. Cada avaliação
/// é um registro novo e nunca muda; a mais recente de cada assunto (o processo ou o pedido) é a atual.
/// </summary>
public sealed class RiskAssessment : Entity<Guid>, ITenantOwned, ICaseScoped
{
    public const int MaxRationaleLength = 2000;

    private RiskAssessment()
    {
    }

    private RiskAssessment(Guid id, Guid caseId, RiskInput input, Guid assessedBy, DateTimeOffset assessedAt)
        : base(id)
    {
        LegalCaseId = caseId;
        ClaimId = input.ClaimId;
        Probability = input.Probability;
        EstimatedLoss = input.EstimatedLoss is { } loss ? decimal.Round(loss, 2, MidpointRounding.AwayFromZero) : null;
        Rationale = string.IsNullOrWhiteSpace(input.Rationale) ? null : input.Rationale.Trim();
        AssessedBy = assessedBy;
        AssessedAt = assessedAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    /// <summary>O pedido avaliado; nulo é o processo inteiro.</summary>
    public Guid? ClaimId { get; private set; }

    public RiskProbability Probability { get; private set; }

    /// <summary>Quanto o escritório estima que o cliente perde, em reais.</summary>
    public decimal? EstimatedLoss { get; private set; }

    public string? Rationale { get; private set; }

    public Guid AssessedBy { get; private set; }

    public DateTimeOffset AssessedAt { get; private set; }

    public RiskAccounting Accounting => AccountingOf(Probability);

    Guid? ICaseScoped.CaseId => LegalCaseId;

    public static RiskAccounting AccountingOf(RiskProbability probability) => probability switch
    {
        RiskProbability.Probable => RiskAccounting.Provision,
        RiskProbability.Possible => RiskAccounting.ContingentLiability,
        _ => RiskAccounting.None,
    };

    public static Result<RiskAssessment> Record(Guid id, Guid caseId, RiskInput input, Guid assessedBy, DateTimeOffset assessedAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.EstimatedLoss < 0
            ? LitigationErrors.ValueInvalid
            : new RiskAssessment(id, caseId, input, assessedBy, assessedAt);
    }
}
