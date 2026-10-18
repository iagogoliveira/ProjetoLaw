using LexFlow.Crm.Domain.People;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Crm.Domain.Opportunities;

/// <summary>Etapas do funil (RF-08): lead → reunião → proposta → aceita ou perdida.</summary>
public enum OpportunityStage
{
    Lead,
    Meeting,
    Proposal,

    /// <summary>Proposta aceita: só pela aceitação da proposta, que cria o cliente.</summary>
    Won,

    /// <summary>Perdida, com o motivo (tabela auxiliar) e uma observação.</summary>
    Lost,
}

/// <summary>
/// Interessado da oportunidade (decisão de 2026-10-17): dados soltos ou uma pessoa já cadastrada. Os dados ficam sempre
/// gravados na oportunidade (cópia dos da pessoa, quando houver), para o funil e para a verificação de conflito.
/// </summary>
public sealed record ProspectInput(PersonKind Kind, string Name, string? Document, string? Email, string? Phone);

/// <summary>Dados editáveis da oportunidade.</summary>
public sealed record OpportunityDetails(
    string Title,
    Guid? PersonId,
    ProspectInput Prospect,
    Guid? OriginId,
    decimal? EstimatedValue,
    DateOnly? ExpectedCloseDate,
    Guid? ResponsibleUserId,
    string? Notes);

/// <summary>
/// Oportunidade do funil comercial (RF-08). A aceitação de uma proposta exige uma verificação de conflito do lado
/// Cliente ligada a ela e já prosseguida (decisão de 2026-10-17, RF-07); mudar o nome ou o documento do interessado
/// desliga a verificação, que deixa de valer.
/// </summary>
public sealed class Opportunity : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxTitleLength = 200;
    public const int MaxNotesLength = 4000;
    public const int MaxLossNoteLength = 1000;

    private Opportunity()
    {
    }

    private Opportunity(Guid id, DateTimeOffset now)
        : base(id)
    {
        Stage = OpportunityStage.Lead;
        StageChangedAt = now;
    }

    public Guid OrganizationId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Pessoa já cadastrada, quando o interessado é conhecido (ou o cliente criado na aceitação).</summary>
    public Guid? PersonId { get; private set; }

    public PersonKind ProspectKind { get; private set; }

    public string ProspectName { get; private set; } = string.Empty;

    /// <summary>CPF ou CNPJ só com dígitos, conforme <see cref="ProspectKind"/>.</summary>
    public string? ProspectDocument { get; private set; }

    public string? ProspectEmail { get; private set; }

    public string? ProspectPhone { get; private set; }

    /// <summary>Item da tabela auxiliar "Origens de oportunidade".</summary>
    public Guid? OriginId { get; private set; }

    public decimal? EstimatedValue { get; private set; }

    public DateOnly? ExpectedCloseDate { get; private set; }

    /// <summary>Usuário responsável pela oportunidade (membro do escritório).</summary>
    public Guid? ResponsibleUserId { get; private set; }

    public string? Notes { get; private set; }

    public OpportunityStage Stage { get; private set; }

    public DateTimeOffset StageChangedAt { get; private set; }

    /// <summary>Item da tabela auxiliar "Motivos de perda".</summary>
    public Guid? LossReasonId { get; private set; }

    public string? LossNote { get; private set; }

    public Guid? ConflictCheckId { get; private set; }

    public DateTimeOffset? WonAt { get; private set; }

    public string CustomFields { get; private set; } = "{}";

    public bool IsClosed => Stage is OpportunityStage.Won or OpportunityStage.Lost;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Result<Opportunity> Create(Guid id, OpportunityDetails details, DateTimeOffset now)
    {
        var opportunity = new Opportunity(id, now);
        var updated = opportunity.Update(details);
        return updated.IsFailure ? updated.Error : opportunity;
    }

    /// <summary>Substitui os dados (a tela envia a oportunidade inteira). A etapa muda pelas ações próprias.</summary>
    public Result Update(OpportunityDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Title, nameof(details));
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Prospect.Name, nameof(details));
        if (Stage == OpportunityStage.Won)
        {
            return CrmErrors.OpportunityWon;
        }

        var document = Person.NormalizeDocument(details.Prospect.Kind, details.Prospect.Document);
        if (document.IsFailure)
        {
            return document.Error;
        }

        if (details.EstimatedValue is < 0)
        {
            return CrmErrors.OpportunityValueInvalid;
        }

        var name = details.Prospect.Name.Trim();
        if (ConflictCheckId is not null
            && (ProspectKind != details.Prospect.Kind
                || !string.Equals(ProspectName, name, StringComparison.Ordinal)
                || !string.Equals(ProspectDocument, document.Value, StringComparison.Ordinal)))
        {
            ConflictCheckId = null;
        }

        Title = details.Title.Trim();
        PersonId = details.PersonId;
        ProspectKind = details.Prospect.Kind;
        ProspectName = name;
        ProspectDocument = document.Value;
        ProspectEmail = Clean(details.Prospect.Email)?.ToLowerInvariant();
        ProspectPhone = Clean(details.Prospect.Phone);
        OriginId = details.OriginId;
        EstimatedValue = details.EstimatedValue is { } value ? decimal.Round(value, 2, MidpointRounding.ToEven) : null;
        ExpectedCloseDate = details.ExpectedCloseDate;
        ResponsibleUserId = details.ResponsibleUserId;
        Notes = Clean(details.Notes);
        return Result.Success;
    }

    public void SetCustomFields(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        CustomFields = json;
    }

    /// <summary>
    /// Move entre lead, reunião e proposta, em qualquer direção. Uma oportunidade perdida pode ser reaberta (o motivo
    /// da perda sai); a aceita não muda mais.
    /// </summary>
    public Result MoveTo(OpportunityStage stage, DateTimeOffset now)
    {
        if (stage is OpportunityStage.Won or OpportunityStage.Lost)
        {
            return CrmErrors.OpportunityStageInvalid;
        }

        if (Stage == OpportunityStage.Won)
        {
            return CrmErrors.OpportunityWon;
        }

        if (Stage != stage)
        {
            Stage = stage;
            StageChangedAt = now;
            LossReasonId = null;
            LossNote = null;
        }

        return Result.Success;
    }

    /// <summary>Envio de uma proposta: a oportunidade que ainda estava em lead ou reunião passa para proposta.</summary>
    public void ProposalSent(DateTimeOffset now)
    {
        if (Stage is OpportunityStage.Lead or OpportunityStage.Meeting)
        {
            Stage = OpportunityStage.Proposal;
            StageChangedAt = now;
        }
    }

    public Result Lose(Guid reasonId, string? note, DateTimeOffset now)
    {
        if (Stage == OpportunityStage.Won)
        {
            return CrmErrors.OpportunityWon;
        }

        if (Stage == OpportunityStage.Lost)
        {
            return CrmErrors.OpportunityLost;
        }

        Stage = OpportunityStage.Lost;
        StageChangedAt = now;
        LossReasonId = reasonId;
        LossNote = Clean(note);
        return Result.Success;
    }

    /// <summary>Liga a verificação de conflito feita com os dados do interessado (lado Cliente).</summary>
    public Result LinkConflictCheck(Guid checkId)
    {
        if (IsClosed)
        {
            return Stage == OpportunityStage.Won ? CrmErrors.OpportunityWon : CrmErrors.OpportunityLost;
        }

        ConflictCheckId = checkId;
        return Result.Success;
    }

    /// <summary>Proposta aceita: a oportunidade fecha ganha, apontando para o cliente.</summary>
    public Result Win(Guid clientId, DateTimeOffset now)
    {
        if (IsClosed)
        {
            return Stage == OpportunityStage.Won ? CrmErrors.OpportunityWon : CrmErrors.OpportunityLost;
        }

        Stage = OpportunityStage.Won;
        StageChangedAt = now;
        WonAt = now;
        PersonId = clientId;
        return Result.Success;
    }

    /// <summary>Fusão de cadastros: a oportunidade passa a apontar para o cadastro mantido.</summary>
    public void PointTo(Guid personId) => PersonId = personId;

    /// <summary>Anonimização LGPD do cadastro ligado: os dados do interessado copiados aqui também saem.</summary>
    public void Redact(string replacement)
    {
        ProspectName = replacement;
        ProspectDocument = null;
        ProspectEmail = null;
        ProspectPhone = null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
