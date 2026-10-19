using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Contracts;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

/// <summary>Comando interno de captura, sem endpoint: executado pelo sistema no tenant da fonte.</summary>
public sealed record ReceivePublicationCommand(PublicationInput Input) : ICommand<Guid>;

internal sealed class ReceivePublicationHandler(IPublicationsDbContext db, ICaseDirectory cases, IClock clock, ICaptureLocks locks, IIntegrationEventPublisher events) : ICommandHandler<ReceivePublicationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ReceivePublicationCommand command, CancellationToken cancellationToken)
    {
        await locks.ControlAsync(cancellationToken);
        var received = Publication.Receive(command.Input, clock.UtcNow);
        if (received.IsFailure) { return received.Error; }
        var publication = received.Value;
        if (!await db.Sources.AnyAsync(s => s.Id == publication.SourceId, cancellationToken))
        {
            return PublicationErrors.SourceNotFound;
        }

        await locks.PublicationAsync(publication.Hash, cancellationToken);
        var existing = await db.Publications.Where(p => p.Hash == publication.Hash).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(cancellationToken);
        if (existing is { } id) { return id; }
        if (publication.ExtractedCnj is { } cnj && await cases.FindByCnjAsync(cnj, cancellationToken) is { } caseId)
        {
            publication.Link(caseId);
        }

        db.Publications.Add(publication);
        events.Publish(new PublicationCapturedIntegrationEvent(publication.Id, publication.LegalCaseId));
        await db.SaveChangesAsync(cancellationToken);
        return publication.Id;
    }
}
