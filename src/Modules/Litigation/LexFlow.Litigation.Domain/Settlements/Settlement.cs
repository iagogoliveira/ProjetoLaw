using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Domain.Settlements;

/// <summary>Andamento do acordo (RF-16).</summary>
public enum SettlementStatus
{
    /// <summary>Propostas e contrapropostas sendo trocadas.</summary>
    Negotiating,

    /// <summary>Os termos finais aguardam a aprovação interna.</summary>
    AwaitingApproval,

    /// <summary>Aprovado internamente; falta a homologação.</summary>
    Approved,

    /// <summary>Homologado em juízo; as parcelas estão correndo.</summary>
    Homologated,

    /// <summary>Todas as parcelas pagas.</summary>
    Fulfilled,

    /// <summary>A negociação terminou sem acordo.</summary>
    Rejected,

    Cancelled,
}

/// <summary>Quem recebe o valor do acordo: o cliente do escritório (contas a receber) ou a outra parte (contas a pagar).</summary>
public enum SettlementDirection
{
    ClientReceives,
    ClientPays,
}

/// <summary>Quem fez a proposta da rodada.</summary>
public enum OfferParty
{
    Office,
    Counterparty,
}

public sealed record OfferInput(OfferParty Party, decimal Amount, string? Terms, DateOnly OfferedOn);

public sealed record InstallmentInput(DateOnly DueOn, decimal Amount);

/// <summary>
/// Acordo do processo (RF-16; decisões de 2026-10-18): a negociação é livre, com as rodadas registradas; os termos
/// finais (valor e parcelas) vão à aprovação interna antes de serem assinados e homologados; depois da homologação,
/// cada parcela recebe baixa manual (até o financeiro da Etapa 10) e, com todas pagas, o acordo está cumprido.
/// </summary>
public sealed class Settlement : AggregateRoot<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    public const int MaxTextLength = 2000;
    public const int MaxInstallments = 360;

    private readonly List<SettlementOffer> _offers = [];
    private readonly List<SettlementInstallment> _installments = [];

    private Settlement()
    {
    }

    private Settlement(Guid id, Guid caseId, SettlementDirection direction, string? notes)
        : base(id)
    {
        LegalCaseId = caseId;
        Direction = direction;
        Notes = Clean(notes);
        Status = SettlementStatus.Negotiating;
    }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public SettlementDirection Direction { get; private set; }

    public SettlementStatus Status { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Valor final enviado à aprovação.</summary>
    public decimal? AgreedAmount { get; private set; }

    public Guid? SubmittedBy { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public DateOnly? HomologatedOn { get; private set; }

    /// <summary>Por que voltou à negociação, terminou sem acordo ou foi cancelado.</summary>
    public string? Reason { get; private set; }

    public IReadOnlyCollection<SettlementOffer> Offers => _offers.AsReadOnly();

    public IReadOnlyCollection<SettlementInstallment> Installments => _installments.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;

    public bool IsOpen => Status is SettlementStatus.Negotiating or SettlementStatus.AwaitingApproval or SettlementStatus.Approved;

    public static Result<Settlement> Start(Guid id, Guid caseId, SettlementDirection direction, string? notes, OfferInput? firstOffer)
    {
        var settlement = new Settlement(id, caseId, direction, notes);
        if (firstOffer is not null)
        {
            var offered = settlement.AddOffer(firstOffer);
            if (offered.IsFailure)
            {
                return offered.Error!;
            }
        }

        return settlement;
    }

    /// <summary>Uma proposta ou contraproposta. Só durante a negociação.</summary>
    public Result AddOffer(OfferInput offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (Status != SettlementStatus.Negotiating)
        {
            return LitigationErrors.SettlementNotNegotiating;
        }

        if (offer.Amount < 0)
        {
            return LitigationErrors.ValueInvalid;
        }

        _offers.Add(new SettlementOffer(Id, LegalCaseId, _offers.Count + 1, offer));
        return Result.Success;
    }

    /// <summary>Envia os termos finais à aprovação: o valor e as parcelas, cuja soma precisa ser o valor.</summary>
    public Result Submit(decimal agreedAmount, IReadOnlyList<InstallmentInput> installments, Guid userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(installments);
        if (Status != SettlementStatus.Negotiating)
        {
            return LitigationErrors.SettlementNotNegotiating;
        }

        var amount = decimal.Round(agreedAmount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0 || installments.Any(i => i.Amount <= 0))
        {
            return LitigationErrors.ValueInvalid;
        }

        if (installments.Count is 0 or > MaxInstallments
            || installments.Sum(i => decimal.Round(i.Amount, 2, MidpointRounding.AwayFromZero)) != amount)
        {
            return LitigationErrors.SettlementInstallmentsMismatch;
        }

        AgreedAmount = amount;
        _installments.Clear();
        var number = 1;
        foreach (var installment in installments.OrderBy(i => i.DueOn))
        {
            _installments.Add(new SettlementInstallment(Id, LegalCaseId, number++, installment));
        }

        Status = SettlementStatus.AwaitingApproval;
        SubmittedBy = userId;
        SubmittedAt = now;
        Reason = null;
        return Result.Success;
    }

    /// <summary>Aprovação interna (permissão <c>cases.settlements.approve</c>; quem enviou também pode aprovar).</summary>
    public Result Approve(Guid userId, DateTimeOffset now)
    {
        if (Status != SettlementStatus.AwaitingApproval)
        {
            return LitigationErrors.SettlementNotAwaitingApproval;
        }

        Status = SettlementStatus.Approved;
        ApprovedBy = userId;
        ApprovedAt = now;
        return Result.Success;
    }

    /// <summary>Devolve à negociação (os termos não foram aprovados ou a outra parte desistiu deles).</summary>
    public Result ReturnToNegotiation(string? reason)
    {
        if (Status is not (SettlementStatus.AwaitingApproval or SettlementStatus.Approved))
        {
            return LitigationErrors.SettlementNotAwaitingApproval;
        }

        Status = SettlementStatus.Negotiating;
        ApprovedBy = null;
        ApprovedAt = null;
        Reason = Clean(reason);
        return Result.Success;
    }

    /// <summary>Homologação em juízo dos termos aprovados.</summary>
    public Result Homologate(DateOnly homologatedOn)
    {
        if (Status != SettlementStatus.Approved)
        {
            return LitigationErrors.SettlementNotApproved;
        }

        Status = SettlementStatus.Homologated;
        HomologatedOn = homologatedOn;
        return Result.Success;
    }

    /// <summary>A negociação terminou sem acordo.</summary>
    public Result Reject(string? reason) => End(SettlementStatus.Rejected, reason);

    public Result Cancel(string? reason) => End(SettlementStatus.Cancelled, reason);

    /// <summary>Baixa manual de uma parcela do acordo homologado; com todas pagas, o acordo está cumprido.</summary>
    public Result PayInstallment(Guid installmentId, DateOnly paidOn, decimal paidAmount)
    {
        if (Status != SettlementStatus.Homologated)
        {
            return LitigationErrors.SettlementNotHomologated;
        }

        var installment = _installments.SingleOrDefault(i => i.Id == installmentId);
        if (installment is null)
        {
            return LitigationErrors.InstallmentNotFound;
        }

        if (installment.PaidOn is not null)
        {
            return LitigationErrors.InstallmentAlreadyPaid;
        }

        if (paidAmount <= 0)
        {
            return LitigationErrors.ValueInvalid;
        }

        installment.Pay(paidOn, paidAmount);
        if (_installments.All(i => i.PaidOn is not null))
        {
            Status = SettlementStatus.Fulfilled;
        }

        return Result.Success;
    }

    private Result End(SettlementStatus status, string? reason)
    {
        if (!IsOpen)
        {
            return LitigationErrors.SettlementClosed;
        }

        Status = status;
        Reason = Clean(reason);
        return Result.Success;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Uma rodada da negociação: quem propôs, o valor, as condições e a data.</summary>
public sealed class SettlementOffer : ITenantOwned, ICaseScoped
{
    private SettlementOffer()
    {
    }

    internal SettlementOffer(Guid settlementId, Guid caseId, int round, OfferInput input)
    {
        Id = Guid.CreateVersion7();
        SettlementId = settlementId;
        LegalCaseId = caseId;
        Round = round;
        Party = input.Party;
        Amount = decimal.Round(input.Amount, 2, MidpointRounding.AwayFromZero);
        Terms = string.IsNullOrWhiteSpace(input.Terms) ? null : input.Terms.Trim();
        OfferedOn = input.OfferedOn;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid SettlementId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public int Round { get; private set; }

    public OfferParty Party { get; private set; }

    public decimal Amount { get; private set; }

    public string? Terms { get; private set; }

    public DateOnly OfferedOn { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;
}

/// <summary>Parcela do acordo, com a baixa manual (data e valor pagos).</summary>
public sealed class SettlementInstallment : ITenantOwned, ICaseScoped
{
    private SettlementInstallment()
    {
    }

    internal SettlementInstallment(Guid settlementId, Guid caseId, int number, InstallmentInput input)
    {
        Id = Guid.CreateVersion7();
        SettlementId = settlementId;
        LegalCaseId = caseId;
        Number = number;
        DueOn = input.DueOn;
        Amount = decimal.Round(input.Amount, 2, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid SettlementId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public int Number { get; private set; }

    public DateOnly DueOn { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly? PaidOn { get; private set; }

    public decimal? PaidAmount { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;

    /// <summary>Vencida: sem baixa e com o vencimento antes de <paramref name="today"/>.</summary>
    public bool IsOverdue(DateOnly today) => PaidOn is null && DueOn < today;

    internal void Pay(DateOnly paidOn, decimal paidAmount)
    {
        PaidOn = paidOn;
        PaidAmount = decimal.Round(paidAmount, 2, MidpointRounding.AwayFromZero);
    }
}
