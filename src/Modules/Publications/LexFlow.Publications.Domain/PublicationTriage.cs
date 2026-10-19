using LexFlow.SharedKernel.Domain;

namespace LexFlow.Publications.Domain;

public enum TriageClassification { DeadlineNotice, Routine, Hearing, Decision, Judgment, Other }

/// <summary>Estrutura persistente reservada à triagem do PR 6D; nenhum fluxo de baixa é exposto no 6A.</summary>
public sealed class PublicationTriage : Entity<Guid>, ITenantOwned, ICaseScoped
{
    private PublicationTriage() { }
    public Guid OrganizationId { get; private set; }
    public Guid PublicationId { get; private set; }
    public Guid LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public TriageClassification Classification { get; private set; }
    public Guid TriagedBy { get; private set; }
    public DateTimeOffset TriagedAt { get; private set; }
    public Guid? DeadlineId { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid? HearingId { get; private set; }
}
