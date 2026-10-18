using LexFlow.Litigation.Domain.Cases;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Domain.Claims;

/// <summary>Índice da correção do pedido (os mesmos da tabela da plataforma).</summary>
public enum ClaimCorrectionIndex
{
    IpcaE,
    Inpc,
    Selic,
    Tr,
}

/// <summary>Qual dos valores do pedido mudou.</summary>
public enum ClaimValueKind
{
    Requested,
    Estimated,
    Awarded,
}

/// <summary>
/// Como corrigir os valores do pedido: o índice e a data de início da correção (ex.: o vencimento) e, se houver, os
/// juros simples em % ao mês a partir de uma data própria (ex.: a citação). Com a SELIC não há juros extras.
/// </summary>
public sealed record CorrectionSettings(ClaimCorrectionIndex Index, DateOnly CorrectFrom, DateOnly? InterestFrom, decimal? MonthlyInterestPercent);

/// <summary>Valores do pedido numa instância: pedido, estimado (expectativa do escritório) e de condenação.</summary>
public sealed record ValuationInput(CaseInstance Instance, decimal? Requested, decimal? Estimated, decimal? Awarded);

/// <summary>Dados do pedido: o objeto (tabela auxiliar "Objetos/pedidos"), a descrição, a correção e os valores.</summary>
public sealed record ClaimInput(Guid ClaimTypeId, string? Description, CorrectionSettings? Correction, IReadOnlyList<ValuationInput>? Valuations);

/// <summary>
/// Pedido do processo (RF-13): o objeto (ex.: horas extras, danos morais), com valor pedido, estimado e de condenação
/// por instância e o histórico de cada alteração de valor, que só cresce. Os valores ficam em reais com 2 casas.
/// </summary>
public sealed class Claim : AggregateRoot<Guid>, ITenantOwned, ICaseScoped, IAuditable, ISoftDeletable
{
    public const int MaxDescriptionLength = 2000;

    private readonly List<ClaimValuation> _valuations = [];
    private readonly List<ClaimValueChange> _changes = [];

    private Claim()
    {
    }

    private Claim(Guid id, Guid caseId)
        : base(id)
    {
        LegalCaseId = caseId;
    }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    /// <summary>Item da tabela auxiliar "Objetos/pedidos".</summary>
    public Guid ClaimTypeId { get; private set; }

    public string? Description { get; private set; }

    public ClaimCorrectionIndex? CorrectionIndex { get; private set; }

    public DateOnly? CorrectFrom { get; private set; }

    public DateOnly? InterestFrom { get; private set; }

    public decimal? MonthlyInterestPercent { get; private set; }

    public IReadOnlyCollection<ClaimValuation> Valuations => _valuations.AsReadOnly();

    /// <summary>Histórico das alterações de valor, da criação em diante.</summary>
    public IReadOnlyCollection<ClaimValueChange> Changes => _changes.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;

    /// <summary>Os valores do pedido carregam a correção, então todos ficam no histórico desde a criação.</summary>
    public static Result<Claim> Create(Guid id, Guid caseId, ClaimInput input, Guid userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        var claim = new Claim(id, caseId);
        var applied = claim.Apply(input, userId, now);
        return applied.IsFailure ? applied.Error : claim;
    }

    public Result Update(ClaimInput input, Guid userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Apply(input, userId, now);
    }

    public static Error? Validate(ClaimInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var valuations = input.Valuations ?? [];
        if (valuations.Select(v => v.Instance).Distinct().Count() != valuations.Count)
        {
            return LitigationErrors.ValuationInstanceDuplicated;
        }

        if (valuations.Any(v => v.Requested < 0 || v.Estimated < 0 || v.Awarded < 0))
        {
            return LitigationErrors.ValueInvalid;
        }

        return input.Correction is { } correction && !IsValid(correction) ? LitigationErrors.ClaimCorrectionInvalid : null;
    }

    private static bool IsValid(CorrectionSettings correction)
    {
        var rate = correction.MonthlyInterestPercent ?? 0m;
        if (rate is < 0m or > 100m)
        {
            return false;
        }

        return rate == 0m || (correction.Index != ClaimCorrectionIndex.Selic && correction.InterestFrom is not null);
    }

    private Result Apply(ClaimInput input, Guid userId, DateTimeOffset now)
    {
        if (Validate(input) is { } error)
        {
            return error;
        }

        ClaimTypeId = input.ClaimTypeId;
        Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        CorrectionIndex = input.Correction?.Index;
        CorrectFrom = input.Correction?.CorrectFrom;
        var hasInterest = (input.Correction?.MonthlyInterestPercent ?? 0m) > 0m;
        InterestFrom = hasInterest ? input.Correction!.InterestFrom : null;
        MonthlyInterestPercent = hasInterest ? input.Correction!.MonthlyInterestPercent : null;

        var wanted = (input.Valuations ?? []).ToDictionary(v => v.Instance);
        foreach (var gone in _valuations.Where(v => !wanted.ContainsKey(v.Instance)).ToList())
        {
            Record(gone.Instance, gone, null, userId, now);
            _valuations.Remove(gone);
        }

        foreach (var valuation in wanted.Values)
        {
            var current = _valuations.SingleOrDefault(v => v.Instance == valuation.Instance);
            var next = new ClaimValuation(Id, LegalCaseId, valuation);
            Record(valuation.Instance, current, next, userId, now);
            if (current is null)
            {
                _valuations.Add(next);
            }
            else
            {
                current.Set(next);
            }
        }

        return Result.Success;
    }

    private void Record(CaseInstance instance, ClaimValuation? before, ClaimValuation? after, Guid userId, DateTimeOffset now)
    {
        foreach (var kind in Enum.GetValues<ClaimValueKind>())
        {
            var old = before?.Amount(kind);
            var value = after?.Amount(kind);
            if (old != value)
            {
                _changes.Add(new ClaimValueChange(Id, LegalCaseId, instance, kind, old, value, userId, now));
            }
        }
    }
}

/// <summary>Valores do pedido numa instância, em reais com 2 casas.</summary>
public sealed class ClaimValuation : ITenantOwned, ICaseScoped
{
    private ClaimValuation()
    {
    }

    internal ClaimValuation(Guid claimId, Guid caseId, ValuationInput input)
    {
        Id = Guid.CreateVersion7();
        ClaimId = claimId;
        LegalCaseId = caseId;
        Instance = input.Instance;
        Requested = Round(input.Requested);
        Estimated = Round(input.Estimated);
        Awarded = Round(input.Awarded);
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ClaimId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public CaseInstance Instance { get; private set; }

    public decimal? Requested { get; private set; }

    public decimal? Estimated { get; private set; }

    public decimal? Awarded { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;

    public decimal? Amount(ClaimValueKind kind) => kind switch
    {
        ClaimValueKind.Requested => Requested,
        ClaimValueKind.Estimated => Estimated,
        _ => Awarded,
    };

    internal void Set(ClaimValuation values)
    {
        Requested = values.Requested;
        Estimated = values.Estimated;
        Awarded = values.Awarded;
    }

    private static decimal? Round(decimal? value) => value is { } v ? decimal.Round(v, 2, MidpointRounding.AwayFromZero) : null;
}

/// <summary>Uma alteração de valor do pedido: o valor anterior, o novo, quem mudou e quando. Nunca muda depois de gravada.</summary>
public sealed class ClaimValueChange : ITenantOwned, ICaseScoped
{
    private ClaimValueChange()
    {
    }

    internal ClaimValueChange(Guid claimId, Guid caseId, CaseInstance instance, ClaimValueKind kind, decimal? oldAmount, decimal? newAmount, Guid changedBy, DateTimeOffset changedAt)
    {
        Id = Guid.CreateVersion7();
        ClaimId = claimId;
        LegalCaseId = caseId;
        Instance = instance;
        Kind = kind;
        OldAmount = oldAmount;
        NewAmount = newAmount;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ClaimId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public CaseInstance Instance { get; private set; }

    public ClaimValueKind Kind { get; private set; }

    public decimal? OldAmount { get; private set; }

    public decimal? NewAmount { get; private set; }

    public Guid ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;
}
