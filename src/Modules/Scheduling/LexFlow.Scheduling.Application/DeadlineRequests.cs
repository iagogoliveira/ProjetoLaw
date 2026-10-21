using System.Text.Json;
using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Scheduling.Application.Calendar;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

public sealed record ManualDeadlineRequest(string Type, string Title, Guid AssigneeUserId, DeadlineRule Rule,
    DateOnly ConfirmedInternalDueOn, DateOnly ConfirmedFatalDueOn, bool Confirmed,
    Guid? CourtId = null, string? StateCode = null, string? Municipality = null);
public sealed record RegisterDeadlineCommand(Guid CaseId, ManualDeadlineRequest Input) : ICommand<DeadlineDetailResponse>;
public sealed record GetDeadlineQuery(Guid Id) : IQuery<DeadlineDetailResponse>;
public sealed record DeadlineDetailResponse(Guid Id, Guid CaseId, Guid? PublicationId, string Type, string Title,
    DateOnly StartOn, DateOnly InternalDueOn, DateOnly FatalDueOn, Guid AssigneeUserId, WorkItemStatus Status,
    Guid ConfirmedBy, DateTimeOffset ConfirmedAt, DeadlineRule? Rule, DeadlineCalculationResponse? Calculation,
    bool RequireReview, Guid? ReviewedBy, DateTimeOffset? ReviewedAt, string? CompletionEvidence, string? MissedReason,
    Guid? ClosedBy, DateTimeOffset? ClosedAt, DateTimeOffset? LastCalendarAlertAt)
{
    public static DeadlineDetailResponse Of(Deadline d) => new(d.Id, d.LegalCaseId, d.PublicationId, d.Type, d.Title,
        d.StartOn, d.InternalDueOn, d.FatalDueOn, d.AssigneeUserId, d.Status, d.ConfirmedBy, d.ConfirmedAt, d.Rule,
        d.CalculationSnapshot is null ? null : JsonSerializer.Deserialize<DeadlineCalculationResponse>(d.CalculationSnapshot),
        d.RequireReview, d.ReviewedBy, d.ReviewedAt, d.CompletionEvidence, d.MissedReason, d.ClosedBy, d.ClosedAt, d.LastCalendarAlertAt);
}

internal sealed class RegisterDeadlineValidator : AbstractValidator<RegisterDeadlineCommand>
{
    public RegisterDeadlineValidator()
    {
        RuleFor(c => c.Input).NotNull();
        When(c => c.Input is not null, () =>
        {
            RuleFor(c => c.Input.Title).NotEmpty().MaximumLength(300);
            RuleFor(c => c.Input.Type).NotEmpty().MaximumLength(80);
            RuleFor(c => c.Input.Rule).NotNull();
            RuleFor(c => c.Input.AssigneeUserId).NotEmpty();
        });
    }
}

internal sealed class DeadlineAccess(ISchedulingDbContext db, ICaseDirectory cases)
{
    public async Task<Result> AuthorizeAsync(Guid id, string permission, CancellationToken ct)
    {
        var caseId = await db.Deadlines.AsNoTracking().Where(d => d.Id == id).Select(d => (Guid?)d.LegalCaseId).SingleOrDefaultAsync(ct);
        return caseId is null ? SchedulingErrors.DeadlineNotFound : await cases.AuthorizeAsync(caseId.Value, permission, ct);
    }
}

internal sealed class DeadlineRequestAuthorizers(ICaseDirectory cases, DeadlineAccess access)
    : IResourceAuthorizer<RegisterDeadlineCommand>, IResourceAuthorizer<GetDeadlineQuery>
{
    public Task<Result> AuthorizeAsync(RegisterDeadlineCommand request, CancellationToken cancellationToken) =>
        cases.AuthorizeAsync(request.CaseId, "schedule.deadlines.manage", cancellationToken);
    public Task<Result> AuthorizeAsync(GetDeadlineQuery request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.Id, "schedule.deadlines.read", cancellationToken);
}

internal sealed class GetDeadlineHandler(ISchedulingDbContext db) : IQueryHandler<GetDeadlineQuery, DeadlineDetailResponse>
{
    public async Task<Result<DeadlineDetailResponse>> Handle(GetDeadlineQuery query, CancellationToken cancellationToken)
    {
        var deadline = await db.Deadlines.AsNoTracking().SingleOrDefaultAsync(d => d.Id == query.Id, cancellationToken);
        return deadline is null ? SchedulingErrors.DeadlineNotFound : DeadlineDetailResponse.Of(deadline);
    }
}

internal sealed class RegisterDeadlineHandler(ISchedulingDbContext db, WorkAssignees assignees, IDeadlineReviewPolicy policy,
    IQueryHandler<CalculateDeadlineQuery, DeadlineCalculationResponse> calculate, ICurrentUser user, IClock clock)
    : ICommandHandler<RegisterDeadlineCommand, DeadlineDetailResponse>
{
    public async Task<Result<DeadlineDetailResponse>> Handle(RegisterDeadlineCommand command, CancellationToken cancellationToken)
    {
        await db.LockDeadlinesAsync(cancellationToken);
        var input = command.Input;
        var allowed = await assignees.CheckAsync(command.CaseId, input.AssigneeUserId, "schedule.deadlines.read", cancellationToken);
        if (allowed.IsFailure) { return allowed.Error; }
        var rule = input.Rule;
        var calculation = await calculate.Handle(new CalculateDeadlineQuery(command.CaseId, input.CourtId, input.StateCode,
            input.Municipality, rule.StartKind, rule.TriggerDate, rule.ConsultedOn, rule.Days, rule.Mode, rule.Doubled, rule.InternalLeadDays), cancellationToken);
        if (calculation.IsFailure) { return calculation.Error; }
        var c = calculation.Value;
        if (c.InternalDueOn != input.ConfirmedInternalDueOn || c.FatalDueOn != input.ConfirmedFatalDueOn) { return SchedulingErrors.CalculationChanged; }
        var value = Deadline.CreateManual(new ManualDeadlineInput(command.CaseId, input.Type, input.Title, input.AssigneeUserId,
            rule, c.StartsOn, c.InternalDueOn, c.FatalDueOn, JsonSerializer.Serialize(c), c.CourtId, c.StateCode, c.Municipality,
            await policy.RequiredAsync(cancellationToken), input.Confirmed), user.UserId ?? Guid.Empty, clock.UtcNow);
        if (value.IsFailure) { return value.Error; }
        db.Deadlines.Add(value.Value);
        db.DeadlineActivities.Add(DeadlineActivity.Record(value.Value, "scheduling.deadline_registered", user.UserId!.Value, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return DeadlineDetailResponse.Of(value.Value);
    }
}
