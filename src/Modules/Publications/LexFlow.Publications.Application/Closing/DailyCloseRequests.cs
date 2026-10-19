using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Closing;

public sealed record DailyCloseInput(DateOnly Day);
public sealed record DailyCloseResponse(Guid Id, DateOnly Day, int Revision, Guid ClosedBy, DateTimeOffset ClosedAt, int PublicationCount)
{
    public static DailyCloseResponse Of(DailyClose c) => new(c.Id, c.Day, c.Revision, c.ClosedBy, c.ClosedAt, c.PublicationCount);
}
public sealed record DailyCloseStatusResponse(DateOnly Day, int PendingCount, int MissingCaptureCount, int PublicationCount,
    bool IsClosed, DailyCloseResponse? Latest, IReadOnlyList<DailyCloseResponse> History, int? NextRevision);
public sealed record GetDailyCloseQuery(DateOnly Day, int? BeforeRevision = null, int Limit = 50) : IQuery<DailyCloseStatusResponse>;
public sealed record ClosePublicationsDayCommand(DateOnly Day) : ICommand<DailyCloseResponse>;

internal sealed class DailyCloseQueryValidator : AbstractValidator<GetDailyCloseQuery>
{
    public DailyCloseQueryValidator()
    {
        RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
        RuleFor(q => q.BeforeRevision).GreaterThan(0).When(q => q.BeforeRevision is not null).WithErrorCode("publications.close_revision_invalid");
    }
}

internal sealed class DailyCloseAccess(IPublicationsDbContext db, PublicationAccess readable, ICurrentAccess access, IClock clock)
{
    public async Task<Result> CheckAsync(DateOnly day, CancellationToken cancellationToken)
    {
        if (day == default || day > clock.Today || day == DateOnly.MaxValue) { return PublicationErrors.InvalidCloseDay; }
        if (!(await access.ScopesForAsync("publications.close", cancellationToken)).Any(s => s.Scope == AccessScope.Organization)
            || !(await access.ScopesForAsync("publications.read", cancellationToken)).Any(s => s.Scope == AccessScope.Organization))
        { return PublicationErrors.CloseDenied; }
        var end = LegalDay.Start(day.AddDays(1), clock.LegalTimeZone);
        var visible = (await readable.VisibleAsync(cancellationToken)).Select(p => p.Id);
        var hidden = await db.Publications.AnyAsync(p => p.ReceivedAt < end && !visible.Contains(p.Id), cancellationToken);
        return hidden ? PublicationErrors.CloseDenied : Result.Success;
    }
}

internal sealed class DailyCloseAuthorizers(DailyCloseAccess access) :
    IResourceAuthorizer<GetDailyCloseQuery>, IResourceAuthorizer<ClosePublicationsDayCommand>
{
    public Task<Result> AuthorizeAsync(GetDailyCloseQuery request, CancellationToken cancellationToken) => access.CheckAsync(request.Day, cancellationToken);
    public Task<Result> AuthorizeAsync(ClosePublicationsDayCommand request, CancellationToken cancellationToken) => access.CheckAsync(request.Day, cancellationToken);
}

internal sealed class GetDailyCloseHandler(IPublicationsDbContext db, DailyCloseState state) : IQueryHandler<GetDailyCloseQuery, DailyCloseStatusResponse>
{
    public async Task<Result<DailyCloseStatusResponse>> Handle(GetDailyCloseQuery query, CancellationToken cancellationToken)
    {
        var snapshot = await state.ReadAsync(query.Day, cancellationToken);
        var history = await db.DailyCloses.AsNoTracking().Where(c => c.Day == query.Day
                && (query.BeforeRevision == null || c.Revision < query.BeforeRevision))
            .OrderByDescending(c => c.Revision).Take(query.Limit + 1).ToListAsync(cancellationToken);
        return new DailyCloseStatusResponse(query.Day, snapshot.PendingCount, snapshot.MissingCaptureCount, snapshot.PublicationCount,
            snapshot.IsClosed, snapshot.Latest is null ? null : DailyCloseResponse.Of(snapshot.Latest),
            history.Take(query.Limit).Select(DailyCloseResponse.Of).ToList(), history.Count > query.Limit ? history[query.Limit - 1].Revision : null);
    }
}

internal sealed class ClosePublicationsDayHandler(IPublicationsDbContext db, DailyCloseState state, DailyCloseAccess access,
    ICaptureLocks locks, ICurrentUser user, IClock clock) : ICommandHandler<ClosePublicationsDayCommand, DailyCloseResponse>
{
    public async Task<Result<DailyCloseResponse>> Handle(ClosePublicationsDayCommand command, CancellationToken cancellationToken)
    {
        await locks.ControlAsync(cancellationToken);
        var allowed = await access.CheckAsync(command.Day, cancellationToken);
        if (allowed.IsFailure) { return allowed.Error; }
        var snapshot = await state.ReadAsync(command.Day, cancellationToken);
        if (snapshot.PendingCount > 0) { return PublicationErrors.PendingTriage; }
        if (snapshot.MissingCaptureCount > 0) { return PublicationErrors.IncompleteCapture; }
        if (snapshot.IsClosed) { return DailyCloseResponse.Of(snapshot.Latest!); }
        if (user.UserId is not { } userId) { return PublicationErrors.CloseDenied; }
        var close = DailyClose.Create(command.Day, (snapshot.Latest?.Revision ?? 0) + 1, userId, clock.UtcNow,
            snapshot.PublicationCount, snapshot.CaptureFingerprint);
        db.DailyCloses.Add(close);
        await db.SaveChangesAsync(cancellationToken);
        return DailyCloseResponse.Of(close);
    }
}

internal static class LegalDay
{
    public static DateTimeOffset Start(DateOnly day, TimeZoneInfo zone) => At(day, TimeOnly.MinValue, zone);

    public static DateTimeOffset At(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) { local = local.AddMinutes(1); }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
