using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Crm.Domain.Proposals;

/// <summary>
/// Situação da proposta (decisão de 2026-10-17: aprovação interna sempre). Rascunho → aguardando aprovação →
/// aprovada → enviada → aceita ou recusada. Aceita uma proposta, as outras abertas da mesma oportunidade ficam
/// substituídas.
/// </summary>
public enum ProposalStatus
{
    Draft,
    PendingApproval,
    Approved,
    Sent,
    Accepted,
    Declined,
    Superseded,
}

/// <summary>Modalidade de honorários de um item (RF-30, só o necessário para a proposta; o contrato é da Etapa 10).</summary>
public enum FeeKind
{
    /// <summary>Valor fixo, pago uma vez (quantidade × valor).</summary>
    Fixed,

    /// <summary>Valor mensal (quantidade de meses × valor).</summary>
    Monthly,

    /// <summary>Por hora (horas estimadas × valor da hora).</summary>
    Hourly,

    /// <summary>Êxito: percentual sobre o benefício econômico, fora do total da proposta.</summary>
    Success,

    /// <summary>Por ato ou fase (quantidade de atos × valor).</summary>
    PerAct,
}

/// <summary>Item como enviado pela tela.</summary>
public sealed record ProposalItemInput(string Description, FeeKind Kind, decimal Quantity, decimal? UnitAmount, decimal? Percentage);

/// <summary>Dados editáveis da proposta, só no rascunho.</summary>
public sealed record ProposalDetails(
    string Title,
    string? Scope,
    string? Conditions,
    DateOnly? ValidUntil,
    Guid? TemplateId,
    IReadOnlyList<ProposalItemInput> Items);

/// <summary>
/// Proposta de honorários (RF-08) de uma oportunidade: escopo, condições, validade e itens com a modalidade. O total
/// soma os itens com valor (êxito fica de fora, por depender do resultado). Quem elaborou ou enviou para aprovação não
/// aprova; ao ser enviada, o DOCX gerado fica congelado no armazenamento.
/// </summary>
public sealed class Proposal : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxTitleLength = 200;
    public const int MaxTextLength = 8000;
    public const int MaxItems = 50;
    public const int MaxDescriptionLength = 500;
    public const int MaxNoteLength = 1000;

    private readonly List<ProposalItem> _items = [];

    private Proposal()
    {
    }

    private Proposal(Guid id, Guid opportunityId)
        : base(id)
    {
        OpportunityId = opportunityId;
        Status = ProposalStatus.Draft;
    }

    public Guid OrganizationId { get; private set; }

    public Guid OpportunityId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Scope { get; private set; }

    public string? Conditions { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    /// <summary>Modelo DOCX do escritório; nulo usa o modelo padrão do sistema.</summary>
    public Guid? TemplateId { get; private set; }

    public string Currency { get; private set; } = "BRL";

    /// <summary>Soma dos itens com valor (fixo, mensal, por hora, por ato), em reais.</summary>
    public decimal Total { get; private set; }

    public ProposalStatus Status { get; private set; }

    public IReadOnlyCollection<ProposalItem> Items => _items.AsReadOnly();

    public Guid? SubmittedBy { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>Por que voltou para rascunho (pedido de ajuste do aprovador, por exemplo).</summary>
    public string? ReturnNote { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Id do DOCX congelado no envio (no armazenamento de objetos, módulo crm).</summary>
    public Guid? DocumentId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? DeclinedAt { get; private set; }

    public string? DeclineNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsOpen => Status is ProposalStatus.Draft or ProposalStatus.PendingApproval or ProposalStatus.Approved or ProposalStatus.Sent;

    public static Result<Proposal> Create(Guid id, Guid opportunityId, ProposalDetails details)
    {
        var proposal = new Proposal(id, opportunityId);
        var updated = proposal.Update(details);
        return updated.IsFailure ? updated.Error : proposal;
    }

    public Result Update(ProposalDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Title, nameof(details));
        if (Status != ProposalStatus.Draft)
        {
            return CrmErrors.ProposalNotDraft;
        }

        if (details.Items.Count > MaxItems)
        {
            return CrmErrors.ProposalTooManyItems;
        }

        var items = new List<ProposalItem>();
        foreach (var (input, index) in details.Items.Select((input, index) => (input, index)))
        {
            var item = ProposalItem.Create(Id, index + 1, input);
            if (item.IsFailure)
            {
                return item.Error;
            }

            items.Add(item.Value);
        }

        Title = details.Title.Trim();
        Scope = Clean(details.Scope);
        Conditions = Clean(details.Conditions);
        ValidUntil = details.ValidUntil;
        TemplateId = details.TemplateId;
        _items.Clear();
        _items.AddRange(items);
        Total = items.Sum(i => i.Amount ?? 0m);
        return Result.Success;
    }

    /// <summary>Envia para aprovação interna. Precisa de ao menos um item.</summary>
    public Result Submit(Guid userId, DateTimeOffset now)
    {
        if (Status != ProposalStatus.Draft)
        {
            return CrmErrors.ProposalNotDraft;
        }

        if (_items.Count == 0)
        {
            return CrmErrors.ProposalItemsRequired;
        }

        Status = ProposalStatus.PendingApproval;
        SubmittedBy = userId;
        SubmittedAt = now;
        ReturnNote = null;
        return Result.Success;
    }

    /// <summary>Aprovação interna: quem elaborou ou enviou para aprovação não aprova a própria proposta.</summary>
    public Result Approve(Guid userId, DateTimeOffset now)
    {
        if (Status != ProposalStatus.PendingApproval)
        {
            return CrmErrors.ProposalNotPendingApproval;
        }

        if (userId == CreatedBy || userId == SubmittedBy)
        {
            return CrmErrors.ProposalSelfApproval;
        }

        Status = ProposalStatus.Approved;
        ApprovedBy = userId;
        ApprovedAt = now;
        return Result.Success;
    }

    /// <summary>Volta para rascunho (ajuste pedido na aprovação, ou correção antes do envio); a aprovação sai.</summary>
    public Result ReturnToDraft(string? note)
    {
        if (Status is not (ProposalStatus.PendingApproval or ProposalStatus.Approved))
        {
            return CrmErrors.ProposalCannotReturn;
        }

        Status = ProposalStatus.Draft;
        SubmittedBy = null;
        SubmittedAt = null;
        ApprovedBy = null;
        ApprovedAt = null;
        ReturnNote = Clean(note);
        return Result.Success;
    }

    /// <summary>Registra o envio ao cliente, com o DOCX congelado (<paramref name="documentId"/>).</summary>
    public Result Send(Guid documentId, DateOnly today, DateTimeOffset now)
    {
        if (Status != ProposalStatus.Approved)
        {
            return CrmErrors.ProposalNotApproved;
        }

        if (ValidUntil is { } validUntil && validUntil < today)
        {
            return CrmErrors.ProposalExpired;
        }

        Status = ProposalStatus.Sent;
        SentAt = now;
        DocumentId = documentId;
        return Result.Success;
    }

    /// <summary>Aceite do cliente (registrado pelo escritório; o portal e a assinatura chegam depois).</summary>
    public Result Accept(DateOnly today, DateTimeOffset now)
    {
        if (Status != ProposalStatus.Sent)
        {
            return CrmErrors.ProposalNotSent;
        }

        if (ValidUntil is { } validUntil && validUntil < today)
        {
            return CrmErrors.ProposalExpired;
        }

        Status = ProposalStatus.Accepted;
        AcceptedAt = now;
        return Result.Success;
    }

    public Result Decline(string? note, DateTimeOffset now)
    {
        if (Status != ProposalStatus.Sent)
        {
            return CrmErrors.ProposalNotSent;
        }

        Status = ProposalStatus.Declined;
        DeclinedAt = now;
        DeclineNote = Clean(note);
        return Result.Success;
    }

    /// <summary>Outra proposta da mesma oportunidade foi aceita.</summary>
    public void Supersede()
    {
        if (IsOpen)
        {
            Status = ProposalStatus.Superseded;
        }
    }

    /// <summary>Só rascunhos são excluídos; o que foi enviado fica como histórico.</summary>
    public Result CanDelete() => Status is ProposalStatus.Draft or ProposalStatus.PendingApproval or ProposalStatus.Approved
        ? Result.Success
        : CrmErrors.ProposalCannotDelete;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Linha da proposta: descrição, modalidade e valor (ou percentual, no êxito).</summary>
public sealed class ProposalItem : ITenantOwned
{
    private ProposalItem()
    {
    }

    private ProposalItem(Guid proposalId, int position, ProposalItemInput input)
    {
        Id = Guid.CreateVersion7();
        ProposalId = proposalId;
        Position = position;
        Description = input.Description.Trim();
        Kind = input.Kind;
        Quantity = input.Quantity;
        UnitAmount = input.Kind == FeeKind.Success ? null : decimal.Round(input.UnitAmount!.Value, 2, MidpointRounding.ToEven);
        Percentage = input.Kind == FeeKind.Success ? input.Percentage : null;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProposalId { get; private set; }

    public int Position { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public FeeKind Kind { get; private set; }

    /// <summary>Meses, horas ou atos (1 no fixo e no êxito).</summary>
    public decimal Quantity { get; private set; }

    public decimal? UnitAmount { get; private set; }

    /// <summary>Percentual do êxito (0 a 100).</summary>
    public decimal? Percentage { get; private set; }

    /// <summary>Quantidade × valor; nulo no êxito.</summary>
    public decimal? Amount => UnitAmount is { } unit ? decimal.Round(Quantity * unit, 2, MidpointRounding.ToEven) : null;

    internal static Result<ProposalItem> Create(Guid proposalId, int position, ProposalItemInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Description, nameof(input));
        if (input.Kind == FeeKind.Success)
        {
            if (input.Percentage is not (> 0 and <= 100))
            {
                return CrmErrors.ProposalPercentageInvalid;
            }

            input = input with { Quantity = 1 };
        }
        else if (input.UnitAmount is not (>= 0) || input.Quantity <= 0)
        {
            return CrmErrors.ProposalAmountInvalid;
        }

        return new ProposalItem(proposalId, position, input);
    }
}
