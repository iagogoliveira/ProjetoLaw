using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Crm.Domain.Conflicts;

/// <summary>Lado da pessoa consultada no novo trabalho (decisão de 2026-10-17: a consulta informa o lado).</summary>
public enum ConflictSide
{
    /// <summary>Vai ser cliente do escritório (novo cliente ou novo caso a favor dela).</summary>
    Client,

    /// <summary>Vai ser parte contrária num novo caso.</summary>
    Opponent,
}

/// <summary>Risco do conflito (RF-07). A ordem importa: o resultado da verificação é o maior risco encontrado.</summary>
public enum ConflictRisk
{
    None,
    Indirect,
    Direct,
}

/// <summary>Por que um cadastro ou uma parte de processo foi apontado.</summary>
public enum ConflictReason
{
    /// <summary>Mesmo CPF/CNPJ de uma pessoa do lado oposto.</summary>
    SameDocument,

    /// <summary>Mesmo nome (sem acentos e pontuação) de uma pessoa do lado oposto.</summary>
    SameName,

    /// <summary>Já é parte, do lado oposto, num processo de outro cliente.</summary>
    CaseParty,

    /// <summary>Do mesmo grupo econômico de uma pessoa do lado oposto.</summary>
    SameEconomicGroup,

    /// <summary>Nome parecido com o de uma pessoa do lado oposto.</summary>
    SimilarName,

    /// <summary>Compartilha e-mail ou telefone de contato com uma pessoa do lado oposto.</summary>
    SharedContact,
}

/// <summary>
/// Verificação de conflito de interesses (RF-07) registrada para auditoria e para o relatório da OAB: o que foi
/// consultado, o lado, o risco e os achados. Com risco, prosseguir exige justificativa, que fica gravada.
/// </summary>
public sealed class ConflictCheck : AggregateRoot<Guid>, ITenantOwned, IAuditable
{
    public const int MaxJustificationLength = 2000;

    private readonly List<ConflictCheckHit> _hits = [];

    private ConflictCheck()
    {
    }

    private ConflictCheck(Guid id, string name, string? document, ConflictSide side)
        : base(id)
    {
        QueryName = name;
        QueryDocument = document;
        Side = side;
    }

    public Guid OrganizationId { get; private set; }

    public string QueryName { get; private set; } = string.Empty;

    /// <summary>Só dígitos, quando informado.</summary>
    public string? QueryDocument { get; private set; }

    public ConflictSide Side { get; private set; }

    public ConflictRisk Risk { get; private set; }

    public bool Proceeded { get; private set; }

    public DateTimeOffset? ProceededAt { get; private set; }

    public string? Justification { get; private set; }

    public IReadOnlyCollection<ConflictCheckHit> Hits => _hits.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static ConflictCheck Record(Guid id, string name, string? document, ConflictSide side, IEnumerable<ConflictHitInput> hits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(hits);
        var check = new ConflictCheck(id, name.Trim(), string.IsNullOrWhiteSpace(document) ? null : document, side);
        check._hits.AddRange(hits.Select(h => new ConflictCheckHit(id, h)));
        check.Risk = check._hits.Count == 0 ? ConflictRisk.None : check._hits.Max(h => h.Risk);
        return check;
    }

    /// <summary>Segue com o novo cliente ou caso. Com conflito (direto ou indireto), a justificativa é obrigatória.</summary>
    public Result Proceed(string? justification, DateTimeOffset now)
    {
        if (Proceeded)
        {
            return CrmErrors.ConflictAlreadyDecided;
        }

        if (Risk != ConflictRisk.None && string.IsNullOrWhiteSpace(justification))
        {
            return CrmErrors.ConflictJustificationRequired;
        }

        Proceeded = true;
        ProceededAt = now;
        Justification = string.IsNullOrWhiteSpace(justification) ? null : justification.Trim();
        return Result.Success;
    }

    /// <summary>Anonimização LGPD: tira o nome e o documento consultados (quando eram os da pessoa anonimizada).</summary>
    public void RedactQuery(string replacement)
    {
        QueryName = replacement;
        QueryDocument = null;
    }
}

/// <summary>Achado como calculado pela detecção, antes de gravado.</summary>
public sealed record ConflictHitInput(
    ConflictRisk Risk,
    ConflictReason Reason,
    Guid? PersonId,
    string MatchedName,
    string? CaseNumber,
    double? Score);

/// <summary>Achado de uma verificação: o cadastro (ou a parte de processo) apontado e o motivo.</summary>
public sealed class ConflictCheckHit : ITenantOwned
{
    private ConflictCheckHit()
    {
    }

    internal ConflictCheckHit(Guid checkId, ConflictHitInput input)
    {
        Id = Guid.CreateVersion7();
        CheckId = checkId;
        Risk = input.Risk;
        Reason = input.Reason;
        PersonId = input.PersonId;
        MatchedName = input.MatchedName;
        CaseNumber = input.CaseNumber;
        Score = input.Score;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CheckId { get; private set; }

    public ConflictRisk Risk { get; private set; }

    public ConflictReason Reason { get; private set; }

    /// <summary>Pessoa do cadastro apontada (nula quando o achado veio só de uma parte de processo).</summary>
    public Guid? PersonId { get; private set; }

    public string MatchedName { get; private set; } = string.Empty;

    /// <summary>Número do processo, quando o achado veio de uma parte de processo.</summary>
    public string? CaseNumber { get; private set; }

    /// <summary>Semelhança do nome (0 a 1), nos achados por nome parecido.</summary>
    public double? Score { get; private set; }

    /// <summary>Fusão de cadastros: o achado passa a apontar para o cadastro mantido.</summary>
    public void PointTo(Guid personId, string name)
    {
        PersonId = personId;
        MatchedName = name;
    }

    /// <summary>Anonimização LGPD: o achado mantém o vínculo, mas não o nome.</summary>
    public void Redact(string replacement) => MatchedName = replacement;
}
