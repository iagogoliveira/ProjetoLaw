using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

public sealed record CalendarEventResponse(Guid Id, Guid? CaseId, string Title, string? Description, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, Guid AssigneeUserId, string? Location, CalendarEventState Status)
{
    public static CalendarEventResponse Of(CalendarEvent entry) => new(entry.Id, entry.LegalCaseId, entry.Title, entry.Description,
        entry.StartsAt, entry.EndsAt, entry.AssigneeUserId, entry.Location, entry.Status);
}
public sealed record RegisterEventCommand(CalendarEventInput Input) : ICommand<CalendarEventResponse>;
public sealed record EditEventCommand(Guid EventId, CalendarEventInput Input) : ICommand<CalendarEventResponse>;
public sealed record CancelEventCommand(Guid EventId) : ICommand<CalendarEventResponse>;
public sealed record GetEventQuery(Guid EventId) : IQuery<CalendarEventResponse>;

internal sealed class EventAuthorizers(AgendaAccess access) : IResourceAuthorizer<RegisterEventCommand>, IResourceAuthorizer<EditEventCommand>,
    IResourceAuthorizer<CancelEventCommand>, IResourceAuthorizer<GetEventQuery>
{
    public Task<Result> AuthorizeAsync(RegisterEventCommand request, CancellationToken ct) =>
        access.AuthorizeAsync(request.Input.CaseId, request.Input.AssigneeUserId, "schedule.events.manage", SchedulingErrors.EventNotFound, ct);
    public Task<Result> AuthorizeAsync(EditEventCommand request, CancellationToken ct) => access.EventAsync(request.EventId, "schedule.events.manage", ct);
    public Task<Result> AuthorizeAsync(CancelEventCommand request, CancellationToken ct) => access.EventAsync(request.EventId, "schedule.events.manage", ct);
    public Task<Result> AuthorizeAsync(GetEventQuery request, CancellationToken ct) => access.EventAsync(request.EventId, "schedule.events.read", ct);
}

internal sealed class RegisterEventHandler(ISchedulingDbContext db, AgendaAccess access) : ICommandHandler<RegisterEventCommand, CalendarEventResponse>
{
    public async Task<Result<CalendarEventResponse>> Handle(RegisterEventCommand command, CancellationToken ct)
    {
        await db.LockDeadlinesAsync(ct);
        var entry = CalendarEvent.Create(command.Input);
        if (entry.IsFailure) { return entry.Error; }
        var allowed = await access.AssigneeAsync(command.Input.CaseId, command.Input.AssigneeUserId, "schedule.events.read", ct);
        if (allowed.IsFailure) { return allowed.Error; }
        db.Events.Add(entry.Value);
        await db.SaveChangesAsync(ct);
        return CalendarEventResponse.Of(entry.Value);
    }
}

internal sealed class GetEventHandler(ISchedulingDbContext db) : IQueryHandler<GetEventQuery, CalendarEventResponse>
{
    public async Task<Result<CalendarEventResponse>> Handle(GetEventQuery query, CancellationToken ct)
    {
        var entry = await db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == query.EventId, ct);
        return entry is null ? SchedulingErrors.EventNotFound : CalendarEventResponse.Of(entry);
    }
}

internal sealed class EventMutations(ISchedulingDbContext db, AgendaAccess access) : ICommandHandler<EditEventCommand, CalendarEventResponse>,
    ICommandHandler<CancelEventCommand, CalendarEventResponse>
{
    public async Task<Result<CalendarEventResponse>> Handle(EditEventCommand command, CancellationToken ct)
    {
        var entry = await LoadAsync(command.EventId, ct);
        if (entry.IsFailure) { return entry.Error; }
        var allowed = await access.AuthorizeAsync(command.Input.CaseId, command.Input.AssigneeUserId, "schedule.events.manage", SchedulingErrors.EventNotFound, ct);
        if (allowed.IsFailure) { return allowed.Error; }
        allowed = await access.AssigneeAsync(command.Input.CaseId, command.Input.AssigneeUserId, "schedule.events.read", ct);
        if (allowed.IsFailure) { return allowed.Error; }
        var result = entry.Value.Edit(command.Input);
        return result.IsFailure ? result.Error : await SaveAsync(entry.Value, ct);
    }

    public async Task<Result<CalendarEventResponse>> Handle(CancelEventCommand command, CancellationToken ct)
    {
        var entry = await LoadAsync(command.EventId, ct);
        if (entry.IsFailure) { return entry.Error; }
        var result = entry.Value.Cancel();
        return result.IsFailure ? result.Error : await SaveAsync(entry.Value, ct);
    }

    private async Task<Result<CalendarEvent>> LoadAsync(Guid id, CancellationToken ct)
    {
        await db.LockDeadlinesAsync(ct);
        var allowed = await access.EventAsync(id, "schedule.events.manage", ct);
        return allowed.IsFailure ? allowed.Error : await db.Events.SingleAsync(e => e.Id == id, ct);
    }

    private async Task<CalendarEventResponse> SaveAsync(CalendarEvent entry, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        return CalendarEventResponse.Of(entry);
    }
}
