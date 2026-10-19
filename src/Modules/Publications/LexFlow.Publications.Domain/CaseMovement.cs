using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Domain;

public enum MovementOrigin { Manual, DataJud, Provider }

public sealed record MovementInput(DateTimeOffset OccurredAt, string Description);

public sealed class CaseMovement : Entity<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    private CaseMovement() { }
    public Guid OrganizationId { get; private set; }
    public Guid LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CollectedAt { get; private set; }
    public MovementOrigin Origin { get; private set; }
    [NotAudited]
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<CaseMovement> Create(Guid caseId, MovementInput input, DateTimeOffset collectedAt, MovementOrigin origin = MovementOrigin.Manual)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (caseId == Guid.Empty || !Enum.IsDefined(origin) || string.IsNullOrWhiteSpace(input.Description)
            || input.Description.Trim().Length > 20_000 || input.OccurredAt == default)
        {
            return PublicationErrors.InvalidMovement;
        }

        return new CaseMovement
        {
            Id = Guid.CreateVersion7(),
            LegalCaseId = caseId,
            Description = input.Description.Trim(),
            Origin = origin,
            OccurredAt = input.OccurredAt.ToUniversalTime(),
            CollectedAt = collectedAt.ToUniversalTime(),
        };
    }
}
