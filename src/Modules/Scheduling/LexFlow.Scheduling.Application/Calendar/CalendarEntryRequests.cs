using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.Calendar;

public sealed record CalendarEntryResponse(Guid Id, string Name, CalendarEntryKind Kind, CalendarEntryScope Scope, string? StateCode,
    string? Municipality, Guid? CourtId, DateOnly StartsOn, DateOnly EndsOn, bool RecursYearly, string? Note)
{
    public static CalendarEntryResponse Of(CalendarEntry entry) => new(entry.Id, entry.Name, entry.Kind, entry.Scope, entry.StateCode,
        entry.Municipality, entry.CourtId, entry.StartsOn, entry.EndsOn, entry.RecursYearly, entry.Note);
}

public sealed record ListCalendarEntriesQuery : IQuery<IReadOnlyList<CalendarEntryResponse>>;
public sealed record CreateCalendarEntryCommand(CalendarEntryInput Input) : ICommand<CalendarEntryResponse>;
public sealed record UpdateCalendarEntryCommand(Guid Id, CalendarEntryInput Input) : ICommand<CalendarEntryResponse>;
public sealed record DeleteCalendarEntryCommand(Guid Id) : ICommand;

internal sealed class CreateCalendarEntryValidator : AbstractValidator<CreateCalendarEntryCommand>
{
    public CreateCalendarEntryValidator() => RuleFor(c => c.Input).NotNull();
}

internal sealed class UpdateCalendarEntryValidator : AbstractValidator<UpdateCalendarEntryCommand>
{
    public UpdateCalendarEntryValidator() => RuleFor(c => c.Input).NotNull();
}

internal sealed class ListCalendarEntriesHandler(ISchedulingDbContext db) : IQueryHandler<ListCalendarEntriesQuery, IReadOnlyList<CalendarEntryResponse>>
{
    public async Task<Result<IReadOnlyList<CalendarEntryResponse>>> Handle(ListCalendarEntriesQuery query, CancellationToken cancellationToken) =>
        await db.CalendarEntries.AsNoTracking().OrderBy(e => e.StartsOn).ThenBy(e => e.Name).ThenBy(e => e.Id)
            .Select(e => new CalendarEntryResponse(e.Id, e.Name, e.Kind, e.Scope, e.StateCode, e.Municipality, e.CourtId, e.StartsOn, e.EndsOn, e.RecursYearly, e.Note))
            .ToListAsync(cancellationToken);
}

internal sealed class CreateCalendarEntryHandler(ISchedulingDbContext db, ICourtDirectory courts, CalendarDeadlineAlerts alerts) : ICommandHandler<CreateCalendarEntryCommand, CalendarEntryResponse>
{
    public async Task<Result<CalendarEntryResponse>> Handle(CreateCalendarEntryCommand command, CancellationToken cancellationToken)
    {
        await db.LockDeadlinesAsync(cancellationToken);
        var entry = CalendarEntry.Create(command.Input);
        if (entry.IsFailure) { return entry.Error; }
        if (!await CourtExistsAsync(courts, command.Input, cancellationToken)) { return SchedulingErrors.CourtNotFound; }
        db.CalendarEntries.Add(entry.Value);
        await alerts.ChangedAsync(null, entry.Value, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return CalendarEntryResponse.Of(entry.Value);
    }

    internal static async Task<bool> CourtExistsAsync(ICourtDirectory courts, CalendarEntryInput input, CancellationToken cancellationToken) =>
        input.Scope != CalendarEntryScope.Court || (input.CourtId is { } id && await courts.GetAsync(id, cancellationToken) is not null);
}

internal sealed class UpdateCalendarEntryHandler(ISchedulingDbContext db, ICourtDirectory courts, CalendarDeadlineAlerts alerts) : ICommandHandler<UpdateCalendarEntryCommand, CalendarEntryResponse>
{
    public async Task<Result<CalendarEntryResponse>> Handle(UpdateCalendarEntryCommand command, CancellationToken cancellationToken)
    {
        await db.LockDeadlinesAsync(cancellationToken);
        var entry = await db.CalendarEntries.SingleOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (entry is null) { return SchedulingErrors.CalendarEntryNotFound; }
        var before = CalendarDeadlineAlerts.Copy(entry);
        var result = entry.Update(command.Input);
        if (result.IsFailure) { return result.Error; }
        if (!await CreateCalendarEntryHandler.CourtExistsAsync(courts, command.Input, cancellationToken)) { return SchedulingErrors.CourtNotFound; }
        await alerts.ChangedAsync(before, entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return CalendarEntryResponse.Of(entry);
    }
}

internal sealed class DeleteCalendarEntryHandler(ISchedulingDbContext db, CalendarDeadlineAlerts alerts) : ICommandHandler<DeleteCalendarEntryCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteCalendarEntryCommand command, CancellationToken cancellationToken)
    {
        await db.LockDeadlinesAsync(cancellationToken);
        var entry = await db.CalendarEntries.SingleOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (entry is null) { return SchedulingErrors.CalendarEntryNotFound; }
        db.CalendarEntries.Remove(entry);
        await alerts.ChangedAsync(entry, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
