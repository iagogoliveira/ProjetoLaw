using LexFlow.SharedKernel.Domain;

namespace LexFlow.Litigation.Domain.Cases;

/// <summary>
/// Uma mudança de situação do processo (suspensão, reativação, encerramento, reabertura), com quem mudou, quando e a
/// observação. Só recebe inserções; alimenta a linha do tempo.
/// </summary>
public sealed class CaseStatusChange : ITenantOwned, ICaseScoped
{
    public const int MaxNoteLength = 500;

    private CaseStatusChange()
    {
    }

    private CaseStatusChange(Guid caseId, CaseStatus from, CaseStatus to, string? note, Guid changedBy, DateTimeOffset changedAt)
    {
        Id = Guid.CreateVersion7();
        LegalCaseId = caseId;
        From = from;
        To = to;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, MaxNoteLength)];
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public CaseStatus From { get; private set; }

    public CaseStatus To { get; private set; }

    public string? Note { get; private set; }

    public Guid ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;

    /// <summary>A mudança, ou nada quando a situação não mudou.</summary>
    public static CaseStatusChange? Between(Guid caseId, CaseStatus from, CaseStatus to, string? note, Guid changedBy, DateTimeOffset changedAt) =>
        from == to ? null : new CaseStatusChange(caseId, from, to, note, changedBy, changedAt);
}
