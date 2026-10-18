using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Litigation.Domain.Cases;

/// <summary>Tipo do caso (RF-09): processo judicial (com CNJ) ou caso sem processo, no mesmo modelo.</summary>
public enum CaseKind
{
    Judicial,

    /// <summary>Administrativo (órgãos públicos, PROCON, agências).</summary>
    Administrative,

    /// <summary>Extrajudicial (negociação, notificação, consultivo com contraparte).</summary>
    Extrajudicial,
}

public enum CaseInstance
{
    First,
    Second,
    Superior,
    Supreme,
}

public enum CaseStatus
{
    Active,
    Suspended,
    Closed,
}

/// <summary>Resultado do encerramento (RF-09).</summary>
public enum CaseOutcome
{
    Granted,
    PartiallyGranted,
    Denied,
    Settlement,
    Dismissed,
    Withdrawn,
    Other,
}

/// <summary>Polo da parte no processo.</summary>
public enum PartyPole
{
    /// <summary>Autor, reclamante, requerente.</summary>
    Active,

    /// <summary>Réu, reclamado, requerido.</summary>
    Passive,

    /// <summary>Terceiro interessado, assistente, litisconsorte.</summary>
    Third,
}

/// <summary>Papel de um responsável pelo caso.</summary>
public enum AssignmentRole
{
    Partner,
    Lawyer,
    Team,
}

/// <summary>Restrição de caso sigiloso (muralha ética, seção 6.3; decisão de 2026-10-18).</summary>
public enum RestrictionMode
{
    /// <summary>Com algum permitido, só os permitidos veem o caso (e quem gerencia restrições).</summary>
    Allow,

    /// <summary>Bloqueado nunca vê o caso, nem com escopo nem gerenciando restrições.</summary>
    Block,
}

/// <summary>Um usuário ou uma equipe numa das listas do caso sigiloso.</summary>
public sealed record RestrictionInput(RestrictionMode Mode, Guid? UserId, Guid? TeamId);

/// <summary>Parte como enviada pela tela: a pessoa do cadastro, o polo, se é cliente e o advogado da parte contrária.</summary>
public sealed record PartyInput(Guid PersonId, PartyPole Pole, bool IsClient, string? CounselName, string? CounselOab);

/// <summary>Responsável: sócio e advogado são usuários; equipe é uma equipe do escritório.</summary>
public sealed record AssignmentInput(AssignmentRole Role, Guid? UserId, Guid? TeamId);

/// <summary>Dados editáveis do processo ou caso (o tipo e a pasta interna não mudam depois do cadastro).</summary>
public sealed record CaseDetails(
    string? CnjNumber,
    Guid? CourtId,
    CaseInstance? Instance,
    string? District,
    string? CourtUnit,
    string? Rite,
    string? ProceduralClass,
    string? Subject,
    Guid? ActionTypeId,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? PhaseId,
    decimal? ClaimValue,
    DateOnly? FiledOn,
    DateOnly? ServedOn,
    string? Description,
    Guid? PayerPersonId,
    IReadOnlyList<PartyInput> Parties,
    IReadOnlyList<AssignmentInput> Assignments);

/// <summary>Encerramento: motivo (tabela auxiliar), resultado, valor final e data.</summary>
public sealed record ClosingInput(Guid ReasonId, CaseOutcome Outcome, decimal? FinalAmount, DateOnly ClosedOn, string? Note);

/// <summary>
/// Processo ou caso (RF-09): judicial com número CNJ validado, ou administrativo e extrajudicial sem CNJ. Pasta interna
/// gerada pela numeração do escritório; partes (pessoas do Crm, com polo e se são clientes), responsáveis, valores,
/// datas e o encerramento com motivo, resultado e valor final.
/// </summary>
public sealed class LegalCase : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxTextLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxNoteLength = 1000;

    private readonly List<CaseParty> _parties = [];
    private readonly List<CaseAssignment> _assignments = [];
    private readonly List<CaseRestriction> _restrictions = [];

    private LegalCase()
    {
    }

    private LegalCase(Guid id, CaseKind kind, string internalFolder)
        : base(id)
    {
        Kind = kind;
        InternalFolder = internalFolder;
        Status = CaseStatus.Active;
    }

    public Guid OrganizationId { get; private set; }

    public CaseKind Kind { get; private set; }

    /// <summary>Pasta interna gerada pela numeração do escritório (ex.: <c>TRAB-2026-00001</c>).</summary>
    public string InternalFolder { get; private set; } = string.Empty;

    /// <summary>Número CNJ só com os 20 dígitos (processos judiciais).</summary>
    public string? CnjNumber { get; private set; }

    public Guid? CourtId { get; private set; }

    public CaseInstance? Instance { get; private set; }

    /// <summary>Comarca, seção ou subseção.</summary>
    public string? District { get; private set; }

    /// <summary>Vara, juízo ou órgão julgador.</summary>
    public string? CourtUnit { get; private set; }

    public string? Rite { get; private set; }

    public string? ProceduralClass { get; private set; }

    public string? Subject { get; private set; }

    /// <summary>Item da tabela auxiliar "Tipos de ação".</summary>
    public Guid? ActionTypeId { get; private set; }

    public Guid? PracticeAreaId { get; private set; }

    public Guid? BranchId { get; private set; }

    /// <summary>Item da tabela auxiliar "Fases".</summary>
    public Guid? PhaseId { get; private set; }

    public CaseStatus Status { get; private set; }

    /// <summary>Valor da causa, em reais.</summary>
    public decimal? ClaimValue { get; private set; }

    /// <summary>Distribuição.</summary>
    public DateOnly? FiledOn { get; private set; }

    /// <summary>Citação.</summary>
    public DateOnly? ServedOn { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Cliente pagador (uma das partes clientes); nulo usa o primeiro cliente.</summary>
    public Guid? PayerPersonId { get; private set; }

    public DateOnly? ClosedOn { get; private set; }

    /// <summary>Item da tabela auxiliar "Motivos de encerramento".</summary>
    public Guid? ClosingReasonId { get; private set; }

    public CaseOutcome? Outcome { get; private set; }

    public decimal? FinalAmount { get; private set; }

    public string? ClosingNote { get; private set; }

    public string CustomFields { get; private set; } = "{}";

    public IReadOnlyCollection<CaseParty> Parties => _parties.AsReadOnly();

    public IReadOnlyCollection<CaseAssignment> Assignments => _assignments.AsReadOnly();

    /// <summary>Listas de permitidos e bloqueados do caso sigiloso (vazias: o caso segue o escopo de dados).</summary>
    public IReadOnlyCollection<CaseRestriction> Restrictions => _restrictions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Número CNJ normalizado e conferido, conforme o tipo (obrigatório no judicial, proibido nos outros).</summary>
    public static Result<string?> NormalizeCnj(CaseKind kind, string? number)
    {
        if (kind != CaseKind.Judicial)
        {
            return string.IsNullOrWhiteSpace(number) ? Result.Ok<string?>(null) : LitigationErrors.CnjNotAllowed;
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            return LitigationErrors.CnjRequired;
        }

        var cnj = SharedKernel.ValueObjects.CnjNumber.Create(number);
        return cnj.IsSuccess ? cnj.Value.Digits : LitigationErrors.CnjInvalid;
    }

    public static Result<LegalCase> Create(Guid id, CaseKind kind, string internalFolder, CaseDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(internalFolder);
        var legalCase = new LegalCase(id, kind, internalFolder);
        var updated = legalCase.Update(details);
        return updated.IsFailure ? updated.Error : legalCase;
    }

    /// <summary>Substitui os dados (a tela envia o caso inteiro). Encerrado, o caso não muda até ser reaberto.</summary>
    public Result Update(CaseDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (Status == CaseStatus.Closed)
        {
            return LitigationErrors.CaseClosed;
        }

        var cnj = NormalizeCnj(Kind, details.CnjNumber);
        if (cnj.IsFailure)
        {
            return cnj.Error;
        }

        if (details.ClaimValue is < 0)
        {
            return LitigationErrors.ValueInvalid;
        }

        var parties = CheckParties(details);
        if (parties.IsFailure)
        {
            return parties.Error;
        }

        if (details.Assignments.Any(a => a.Role == AssignmentRole.Team ? a.TeamId is null || a.UserId is not null : a.UserId is null || a.TeamId is not null))
        {
            return LitigationErrors.AssignmentInvalid;
        }

        CnjNumber = cnj.Value;
        CourtId = details.CourtId;
        Instance = details.Instance;
        District = Clean(details.District);
        CourtUnit = Clean(details.CourtUnit);
        Rite = Clean(details.Rite);
        ProceduralClass = Clean(details.ProceduralClass);
        Subject = Clean(details.Subject);
        ActionTypeId = details.ActionTypeId;
        PracticeAreaId = details.PracticeAreaId;
        BranchId = details.BranchId;
        PhaseId = details.PhaseId;
        ClaimValue = details.ClaimValue is { } value ? decimal.Round(value, 2, MidpointRounding.ToEven) : null;
        FiledOn = details.FiledOn;
        ServedOn = details.ServedOn;
        Description = Clean(details.Description);
        PayerPersonId = details.PayerPersonId;

        _parties.Clear();
        _parties.AddRange(details.Parties.Select(p => new CaseParty(Id, p)));
        _assignments.Clear();
        _assignments.AddRange(details.Assignments
            .DistinctBy(a => (a.Role, a.UserId, a.TeamId))
            .Select(a => new CaseAssignment(Id, a)));
        return Result.Success;
    }

    public void SetCustomFields(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        CustomFields = json;
    }

    /// <summary>Suspende ou reativa (o encerramento é pela ação própria).</summary>
    public Result SetStatus(CaseStatus status)
    {
        if (status == CaseStatus.Closed || Status == CaseStatus.Closed)
        {
            return LitigationErrors.CaseClosed;
        }

        Status = status;
        return Result.Success;
    }

    public Result Close(ClosingInput closing)
    {
        ArgumentNullException.ThrowIfNull(closing);
        if (Status == CaseStatus.Closed)
        {
            return LitigationErrors.CaseClosed;
        }

        if (closing.FinalAmount is < 0)
        {
            return LitigationErrors.ValueInvalid;
        }

        Status = CaseStatus.Closed;
        ClosedOn = closing.ClosedOn;
        ClosingReasonId = closing.ReasonId;
        Outcome = closing.Outcome;
        FinalAmount = closing.FinalAmount is { } amount ? decimal.Round(amount, 2, MidpointRounding.ToEven) : null;
        ClosingNote = Clean(closing.Note);
        return Result.Success;
    }

    /// <summary>Reabre o caso encerrado; os dados do encerramento saem (ficam na auditoria).</summary>
    public Result Reopen()
    {
        if (Status != CaseStatus.Closed)
        {
            return LitigationErrors.CaseNotClosed;
        }

        Status = CaseStatus.Active;
        ClosedOn = null;
        ClosingReasonId = null;
        Outcome = null;
        FinalAmount = null;
        ClosingNote = null;
        return Result.Success;
    }

    /// <summary>
    /// Troca as listas do caso sigiloso: cada entrada é um usuário ou uma equipe, sem repetir, e ninguém fica nas duas
    /// listas. Listas vazias tiram o sigilo.
    /// </summary>
    public Result SetRestrictions(IReadOnlyCollection<RestrictionInput> restrictions)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        if (restrictions.Any(r => (r.UserId is null) == (r.TeamId is null)))
        {
            return LitigationErrors.RestrictionInvalid;
        }

        if (restrictions.DistinctBy(r => (r.UserId, r.TeamId)).Count() != restrictions.Count)
        {
            return LitigationErrors.RestrictionDuplicated;
        }

        _restrictions.Clear();
        _restrictions.AddRange(restrictions.Select(r => new CaseRestriction(Id, r)));
        return Result.Success;
    }

    /// <summary>Fusão de cadastros no Crm: a pessoa unificada passa a ser a mantida (sem repetir a mesma pessoa).</summary>
    public void ReplacePerson(Guid mergedPersonId, Guid keptPersonId)
    {
        var merged = _parties.Where(p => p.PersonId == mergedPersonId).ToList();
        foreach (var party in merged)
        {
            if (_parties.Exists(p => p.PersonId == keptPersonId))
            {
                _parties.Remove(party);
            }
            else
            {
                party.PointTo(keptPersonId);
            }
        }

        if (PayerPersonId == mergedPersonId)
        {
            PayerPersonId = keptPersonId;
        }
    }

    private static Result CheckParties(CaseDetails details)
    {
        if (!details.Parties.Any(p => p.IsClient))
        {
            return LitigationErrors.ClientRequired;
        }

        if (details.Parties.DistinctBy(p => p.PersonId).Count() != details.Parties.Count)
        {
            return LitigationErrors.PartyDuplicated;
        }

        if (details.PayerPersonId is { } payer && !details.Parties.Any(p => p.IsClient && p.PersonId == payer))
        {
            return LitigationErrors.PayerNotClient;
        }

        return Result.Success;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Parte do processo: a pessoa do cadastro (Crm), o polo, se é cliente do escritório e o advogado contrário.</summary>
public sealed class CaseParty : ITenantOwned, ICaseScoped
{
    public const int MaxCounselLength = 200;

    private CaseParty()
    {
    }

    internal CaseParty(Guid caseId, PartyInput input)
    {
        Id = Guid.CreateVersion7();
        LegalCaseId = caseId;
        PersonId = input.PersonId;
        Pole = input.Pole;
        IsClient = input.IsClient;
        CounselName = string.IsNullOrWhiteSpace(input.CounselName) ? null : input.CounselName.Trim();
        CounselOab = string.IsNullOrWhiteSpace(input.CounselOab) ? null : input.CounselOab.Trim().ToUpperInvariant();
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public Guid PersonId { get; private set; }

    public PartyPole Pole { get; private set; }

    public bool IsClient { get; private set; }

    /// <summary>Advogado da parte (o contrário, quando a parte não é cliente).</summary>
    public string? CounselName { get; private set; }

    /// <summary>OAB do advogado (ex.: <c>SP 123456</c>).</summary>
    public string? CounselOab { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;

    internal void PointTo(Guid personId) => PersonId = personId;
}

/// <summary>Responsável pelo caso: sócio, advogado (usuários) ou equipe.</summary>
public sealed class CaseAssignment : ITenantOwned, ICaseScoped
{
    private CaseAssignment()
    {
    }

    internal CaseAssignment(Guid caseId, AssignmentInput input)
    {
        Id = Guid.CreateVersion7();
        LegalCaseId = caseId;
        Role = input.Role;
        UserId = input.UserId;
        TeamId = input.TeamId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public AssignmentRole Role { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid? TeamId { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;
}

/// <summary>Entrada da lista de permitidos ou bloqueados de um caso sigiloso: um usuário ou uma equipe.</summary>
public sealed class CaseRestriction : ITenantOwned, ICaseScoped
{
    private CaseRestriction()
    {
    }

    internal CaseRestriction(Guid caseId, RestrictionInput input)
    {
        Id = Guid.CreateVersion7();
        LegalCaseId = caseId;
        Mode = input.Mode;
        UserId = input.UserId;
        TeamId = input.TeamId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public RestrictionMode Mode { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid? TeamId { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;
}
