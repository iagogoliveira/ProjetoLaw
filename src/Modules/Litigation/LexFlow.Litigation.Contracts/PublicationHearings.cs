using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Contracts;

public enum PublicationHearingType { Conciliation, Instruction, Combined, Judgment, Other }
public enum PublicationHearingFormat { InPerson, Virtual, Hybrid }
public sealed record PublicationHearingDraft(Guid CaseId, PublicationHearingType Type, PublicationHearingFormat Format,
    DateTimeOffset ScheduledAt, string? Location, string? MeetingLink, Guid LawyerUserId, string? Notes);

public interface IPublicationHearings
{
    Task<Result<Guid>> CreateAsync(PublicationHearingDraft draft, CancellationToken cancellationToken);
}
