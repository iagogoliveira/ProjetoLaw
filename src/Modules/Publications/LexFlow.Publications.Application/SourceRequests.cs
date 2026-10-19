using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public sealed record CaptureSourceResponse(Guid Id, CaptureSourceKind Kind, string Label, string Value, bool IsActive)
{
    public static CaptureSourceResponse Of(CaptureSource source) => new(source.Id, source.Kind, source.Label, source.Value, source.IsActive);
}
public sealed record ListCaptureSourcesQuery : IQuery<IReadOnlyList<CaptureSourceResponse>>;
public sealed record GetCaptureSourceQuery(Guid Id) : IQuery<CaptureSourceResponse>;
public sealed record CreateCaptureSourceCommand(CaptureSourceInput Input) : ICommand<CaptureSourceResponse>;
public sealed record UpdateCaptureSourceCommand(Guid Id, CaptureSourceInput Input) : ICommand<CaptureSourceResponse>;

internal sealed class CreateSourceValidator : AbstractValidator<CreateCaptureSourceCommand>
{
    public CreateSourceValidator() => RuleFor(c => c.Input).NotNull();
}
internal sealed class UpdateSourceValidator : AbstractValidator<UpdateCaptureSourceCommand>
{
    public UpdateSourceValidator() => RuleFor(c => c.Input).NotNull();
}

internal sealed class ListCaptureSourcesHandler(IPublicationsDbContext db) : IQueryHandler<ListCaptureSourcesQuery, IReadOnlyList<CaptureSourceResponse>>
{
    public async Task<Result<IReadOnlyList<CaptureSourceResponse>>> Handle(ListCaptureSourcesQuery query, CancellationToken cancellationToken) =>
        await db.Sources.AsNoTracking().OrderBy(s => s.Label).Select(s => new CaptureSourceResponse(s.Id, s.Kind, s.Label, s.Value, s.IsActive)).ToListAsync(cancellationToken);
}
internal sealed class GetCaptureSourceHandler(IPublicationsDbContext db) : IQueryHandler<GetCaptureSourceQuery, CaptureSourceResponse>
{
    public async Task<Result<CaptureSourceResponse>> Handle(GetCaptureSourceQuery query, CancellationToken cancellationToken)
    {
        var source = await db.Sources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == query.Id, cancellationToken);
        return source is null ? PublicationErrors.SourceNotFound : CaptureSourceResponse.Of(source);
    }
}
internal sealed class CreateCaptureSourceHandler(IPublicationsDbContext db, IClock clock, IIntegrationEventPublisher events, ICaptureLocks locks) : ICommandHandler<CreateCaptureSourceCommand, CaptureSourceResponse>
{
    public async Task<Result<CaptureSourceResponse>> Handle(CreateCaptureSourceCommand command, CancellationToken cancellationToken)
    {
        await locks.ControlAsync(cancellationToken);
        var result = CaptureSource.Create(command.Input);
        if (result.IsFailure) { return result.Error; }
        if (result.Value.IsActive)
        {
            result.Value.ActivateAt(clock.UtcNow);
            db.SourcePeriods.Add(CaptureSourcePeriod.Start(result.Value.Id, clock.UtcNow));
            events.Publish(new SourceActivatedIntegrationEvent(result.Value.Id));
        }
        db.Sources.Add(result.Value);
        await db.SaveChangesAsync(cancellationToken);
        return CaptureSourceResponse.Of(result.Value);
    }
}
internal sealed class UpdateCaptureSourceHandler(IPublicationsDbContext db, IClock clock, IIntegrationEventPublisher events, ICaptureLocks locks) : ICommandHandler<UpdateCaptureSourceCommand, CaptureSourceResponse>
{
    public async Task<Result<CaptureSourceResponse>> Handle(UpdateCaptureSourceCommand command, CancellationToken cancellationToken)
    {
        await locks.ControlAsync(cancellationToken);
        var source = await db.Sources.SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (source is null) { return PublicationErrors.SourceNotFound; }
        var wasActive = source.IsActive;
        var oldKind = source.Kind;
        var oldValue = source.Value;
        var result = source.Update(command.Input);
        if (result.IsFailure) { return result.Error; }
        var newActivation = !wasActive || oldKind != source.Kind || !string.Equals(oldValue, source.Value, StringComparison.Ordinal);
        if (wasActive && (!source.IsActive || newActivation))
        {
            foreach (var period in await db.SourcePeriods.Where(p => p.SourceId == source.Id && p.DeactivatedAt == null).ToListAsync(cancellationToken))
            { period.End(clock.UtcNow); }
        }
        if (source.IsActive && newActivation)
        {
            source.ActivateAt(clock.UtcNow);
            db.SourcePeriods.Add(CaptureSourcePeriod.Start(source.Id, clock.UtcNow));
            events.Publish(new SourceActivatedIntegrationEvent(source.Id));
        }
        await db.SaveChangesAsync(cancellationToken);
        return CaptureSourceResponse.Of(source);
    }
}
