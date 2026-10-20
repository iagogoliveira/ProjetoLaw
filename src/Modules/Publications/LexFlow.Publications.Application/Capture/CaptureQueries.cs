using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Capture;

public sealed record CaptureRunResponse(Guid Id, DateOnly Day, CaptureRunStatus Status, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, int Count, string? ErrorCode);
public sealed record CaptureAlertResponse(Guid Id, Guid SourceId, DateOnly Day, DateTimeOffset RaisedAt, DateTimeOffset? ResolvedAt, string ErrorCode);
public sealed record CaptureStateResponse(Guid SourceId, DateOnly Day, DateTimeOffset DueBy, IReadOnlyList<CaptureRunResponse> Runs, CaptureAlertResponse? Alert);
public sealed record GetCaptureStateQuery(Guid SourceId, bool MultiFactorAuthenticated = false, DateOnly? Day = null) : IQuery<CaptureStateResponse>;
public sealed record ListCaptureAlertsQuery(DateOnly? Day, int Limit, bool MultiFactorAuthenticated = false) : IQuery<IReadOnlyList<CaptureAlertResponse>>;

internal sealed class ListCaptureAlertsValidator : AbstractValidator<ListCaptureAlertsQuery>
{
    public ListCaptureAlertsValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}
internal sealed class CaptureQueriesAccess(ICurrentUser user, IOrganizationAccessDirectory directory)
{
    public async Task<bool> AllowedAsync(bool mfa, CancellationToken cancellationToken) =>
        user.UserId is { } id && await directory.GetAsync(id, cancellationToken) is { } access
            && (!access.MfaRequired || mfa) && PublicationNotificationVisibility.HasOfficeAlertAccess(access);
}
internal sealed class GetCaptureStateHandler(IPublicationsDbContext db, IClock clock, CaptureQueriesAccess access) : IQueryHandler<GetCaptureStateQuery, CaptureStateResponse>
{
    public async Task<Result<CaptureStateResponse>> Handle(GetCaptureStateQuery query, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(query.MultiFactorAuthenticated, cancellationToken)) { return Error.Forbidden("publications.capture_access_denied", "Sem acesso ao monitoramento da captura."); }
        var source = await db.Sources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == query.SourceId, cancellationToken);
        if (source is null) { return PublicationErrors.SourceNotFound; }
        var day = query.Day ?? clock.Today;
        if (day == default || day > clock.Today) { return Error.Validation("publications.capture_day_invalid", "Informe uma data válida até hoje."); }
        var runs = await db.Runs.AsNoTracking().Where(r => r.SourceId == source.Id && r.Day == day).OrderByDescending(r => r.StartedAt).Take(20)
            .Select(r => new CaptureRunResponse(r.Id, r.Day, r.Status, r.StartedAt, r.FinishedAt, r.Count, r.ErrorCode)).ToListAsync(cancellationToken);
        var alert = await db.Alerts.AsNoTracking().Where(a => a.SourceId == source.Id && a.Day == day)
            .Select(a => new CaptureAlertResponse(a.Id, a.SourceId, a.Day, a.RaisedAt, a.ResolvedAt, a.ErrorCode)).SingleOrDefaultAsync(cancellationToken);
        return new CaptureStateResponse(source.Id, day, CaptureSchedule.Deadline(source, day, clock.LegalTimeZone), runs, alert);
    }
}
internal sealed class ListCaptureAlertsHandler(IPublicationsDbContext db, IClock clock, CaptureQueriesAccess access)
    : IQueryHandler<ListCaptureAlertsQuery, IReadOnlyList<CaptureAlertResponse>>
{
    public async Task<Result<IReadOnlyList<CaptureAlertResponse>>> Handle(ListCaptureAlertsQuery query, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(query.MultiFactorAuthenticated, cancellationToken)) { return Error.Forbidden("publications.capture_access_denied", "Sem acesso ao monitoramento da captura."); }
        var day = query.Day ?? clock.Today;
        return await db.Alerts.AsNoTracking().Where(a => a.Day == day).OrderByDescending(a => a.RaisedAt).ThenByDescending(a => a.Id).Take(query.Limit)
            .Select(a => new CaptureAlertResponse(a.Id, a.SourceId, a.Day, a.RaisedAt, a.ResolvedAt, a.ErrorCode)).ToListAsync(cancellationToken);
    }
}
