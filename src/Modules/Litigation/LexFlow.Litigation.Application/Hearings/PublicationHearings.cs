using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Litigation.Application.Hearings;

internal sealed class PublicationHearings([FromKeyedServices("Litigation")] IUnitOfWork work,
    ICommandHandler<ScheduleHearingCommand, HearingResponse> handler) : IPublicationHearings
{
    public async Task<Result<Guid>> CreateAsync(PublicationHearingDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!work.HasActiveTransaction) { throw new InvalidOperationException("Audiência da triagem exige transação local compartilhada."); }
        if (!Enum.IsDefined(draft.Type) || !Enum.IsDefined(draft.Format) || draft.ScheduledAt == default || draft.LawyerUserId == Guid.Empty)
        { return Error.Validation("publications.hearing_invalid", "Confirme os dados e o advogado da audiência."); }
        var input = new HearingInput((HearingType)draft.Type, (HearingFormat)draft.Format, draft.ScheduledAt,
            draft.Location, draft.MeetingLink, draft.LawyerUserId, null, null, draft.Notes);
        var result = await handler.Handle(new ScheduleHearingCommand(draft.CaseId, input), cancellationToken);
        return result.IsFailure ? result.Error : result.Value.Id;
    }
}
