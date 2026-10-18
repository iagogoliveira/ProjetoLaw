using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Crm.Contracts;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Hearings;

public sealed record HearingRescheduleResponse(DateTimeOffset PreviousDate, DateTimeOffset NewDate, string? Reason, Guid ChangedBy, DateTimeOffset ChangedAt);

public sealed record HearingResponse(
    Guid Id,
    Guid CaseId,
    HearingType Type,
    HearingFormat Format,
    HearingStatus Status,
    DateTimeOffset ScheduledAt,
    string? Location,
    string? MeetingLink,
    Guid? LawyerUserId,
    string? Representative,
    IReadOnlyList<Guid> WitnessPersonIds,
    string? Notes,
    string? Outcome,
    IReadOnlyList<HearingRescheduleResponse> Reschedules,
    DateTimeOffset CreatedAt);

/// <summary>As audiências do processo, pela data.</summary>
public sealed record ListHearingsQuery(Guid CaseId) : IQuery<IReadOnlyList<HearingResponse>>;

public sealed record ScheduleHearingCommand(Guid CaseId, HearingInput Hearing) : ICommand<HearingResponse>;

public sealed record UpdateHearingCommand(Guid CaseId, Guid HearingId, HearingInput Hearing) : ICommand<HearingResponse>;

public sealed record RescheduleHearingCommand(Guid CaseId, Guid HearingId, DateTimeOffset NewDate, string? Reason) : ICommand<HearingResponse>;

/// <summary>Encerra a audiência: realizada (com o resultado ou a ata) ou cancelada (com o motivo).</summary>
public sealed record CloseHearingCommand(Guid CaseId, Guid HearingId, bool Held, string? Text) : ICommand<HearingResponse>;

internal sealed class HearingInputValidator : AbstractValidator<HearingInput>
{
    public const int MaxWitnesses = 30;

    public HearingInputValidator()
    {
        RuleFor(h => h.Type).IsInEnum().WithErrorCode("litigation.hearing_type_invalid");
        RuleFor(h => h.Format).IsInEnum().WithErrorCode("litigation.hearing_format_invalid");
        RuleFor(h => h.Location).MaximumLength(Hearing.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(h => h.MeetingLink).MaximumLength(Hearing.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(h => h.Representative).MaximumLength(Hearing.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(h => h.Notes).MaximumLength(Hearing.MaxNotesLength).WithErrorCode("litigation.text_too_long");
        RuleFor(h => h.WitnessPersonIds).Must(w => w is null || w.Count <= MaxWitnesses).WithErrorCode("litigation.hearing_too_many_witnesses");
    }
}

internal sealed class ScheduleHearingValidator : AbstractValidator<ScheduleHearingCommand>
{
    public ScheduleHearingValidator()
    {
        RuleFor(c => c.Hearing).NotNull().SetValidator(new HearingInputValidator());
    }
}

internal sealed class UpdateHearingValidator : AbstractValidator<UpdateHearingCommand>
{
    public UpdateHearingValidator()
    {
        RuleFor(c => c.Hearing).NotNull().SetValidator(new HearingInputValidator());
    }
}

internal sealed class RescheduleHearingValidator : AbstractValidator<RescheduleHearingCommand>
{
    public RescheduleHearingValidator()
    {
        RuleFor(c => c.Reason).MaximumLength(Hearing.MaxTextLength).WithErrorCode("litigation.text_too_long");
    }
}

internal sealed class CloseHearingValidator : AbstractValidator<CloseHearingCommand>
{
    public CloseHearingValidator()
    {
        RuleFor(c => c.Text).MaximumLength(Hearing.MaxNotesLength).WithErrorCode("litigation.text_too_long");
    }
}

internal sealed class ListHearingsHandler(ILitigationDbContext db, CaseWrites cases) : IQueryHandler<ListHearingsQuery, IReadOnlyList<HearingResponse>>
{
    public async Task<Result<IReadOnlyList<HearingResponse>>> Handle(ListHearingsQuery query, CancellationToken cancellationToken)
    {
        if (!await cases.ExistsAsync(query.CaseId, cancellationToken))
        {
            return LitigationErrors.CaseNotFound;
        }

        var hearings = await db.Hearings.AsNoTracking().Include(h => h.Reschedules)
            .Where(h => h.LegalCaseId == query.CaseId)
            .OrderBy(h => h.ScheduledAt)
            .ToListAsync(cancellationToken);
        return hearings.Select(h => h.ToResponse()).ToList();
    }
}

internal sealed class ScheduleHearingHandler(ILitigationDbContext db, HearingRules rules, IIdGenerator ids) : ICommandHandler<ScheduleHearingCommand, HearingResponse>
{
    public async Task<Result<HearingResponse>> Handle(ScheduleHearingCommand command, CancellationToken cancellationToken)
    {
        var allowed = await rules.CheckAsync(command.CaseId, command.Hearing, cancellationToken);
        if (allowed.IsFailure)
        {
            return allowed.Error!;
        }

        var hearing = Hearing.Schedule(ids.NewId(), command.CaseId, command.Hearing);
        if (hearing.IsFailure)
        {
            return hearing.Error!;
        }

        db.Hearings.Add(hearing.Value);
        await db.SaveChangesAsync(cancellationToken);
        return hearing.Value.ToResponse();
    }
}

internal sealed class UpdateHearingHandler(HearingRules rules) : ICommandHandler<UpdateHearingCommand, HearingResponse>
{
    public async Task<Result<HearingResponse>> Handle(UpdateHearingCommand command, CancellationToken cancellationToken)
    {
        var allowed = await rules.CheckAsync(command.CaseId, command.Hearing, cancellationToken);
        return allowed.IsFailure
            ? allowed.Error!
            : await rules.RunAsync(command.CaseId, command.HearingId, h => h.Update(command.Hearing), cancellationToken);
    }
}

internal sealed class RescheduleHearingHandler(HearingRules rules, CaseWrites cases) : ICommandHandler<RescheduleHearingCommand, HearingResponse>
{
    public Task<Result<HearingResponse>> Handle(RescheduleHearingCommand command, CancellationToken cancellationToken) =>
        rules.RunAsync(command.CaseId, command.HearingId, h => h.Reschedule(command.NewDate, command.Reason, cases.UserId, cases.Now), cancellationToken);
}

internal sealed class CloseHearingHandler(HearingRules rules) : ICommandHandler<CloseHearingCommand, HearingResponse>
{
    public Task<Result<HearingResponse>> Handle(CloseHearingCommand command, CancellationToken cancellationToken) =>
        rules.RunAsync(command.CaseId, command.HearingId, h => command.Held ? h.Hold(command.Text) : h.Cancel(command.Text), cancellationToken);
}

/// <summary>
/// Conferências das audiências: o processo aberto, o advogado designado como membro do escritório e as testemunhas
/// como pessoas do cadastro.
/// </summary>
internal sealed class HearingRules(
    ILitigationDbContext db,
    CaseWrites cases,
    IMembershipLookup memberships,
    ITenantContext tenant,
    IPeopleDirectory people)
{
    public async Task<Result> CheckAsync(Guid caseId, HearingInput input, CancellationToken cancellationToken)
    {
        var open = await cases.CheckOpenAsync(caseId, cancellationToken);
        if (open.IsFailure)
        {
            return open;
        }

        if (input.LawyerUserId is { } lawyer
            && !(await memberships.GetAsync(lawyer, cancellationToken)).OrganizationIds.Contains(tenant.RequiredOrganizationId))
        {
            return LitigationErrors.ResponsibleNotMember;
        }

        var witnesses = (input.WitnessPersonIds ?? []).Distinct().ToList();
        return witnesses.Count > 0 && (await people.GetAsync(witnesses, cancellationToken)).Count != witnesses.Count
            ? LitigationErrors.PersonNotFound
            : Result.Success;
    }

    public async Task<Result<HearingResponse>> RunAsync(Guid caseId, Guid hearingId, Func<Hearing, Result> action, CancellationToken cancellationToken)
    {
        var open = await cases.CheckOpenAsync(caseId, cancellationToken);
        if (open.IsFailure)
        {
            return open.Error!;
        }

        var hearing = await db.Hearings.Include(h => h.Reschedules).SingleOrDefaultAsync(h => h.Id == hearingId && h.LegalCaseId == caseId, cancellationToken);
        if (hearing is null)
        {
            return LitigationErrors.HearingNotFound;
        }

        var done = action(hearing);
        if (done.IsFailure)
        {
            return done.Error!;
        }

        await db.SaveChangesAsync(cancellationToken);
        return hearing.ToResponse();
    }
}

internal static class HearingMapping
{
    public static HearingResponse ToResponse(this Hearing h) => new(
        h.Id,
        h.LegalCaseId,
        h.Type,
        h.Format,
        h.Status,
        h.ScheduledAt,
        h.Location,
        h.MeetingLink,
        h.LawyerUserId,
        h.Representative,
        h.WitnessPersonIds,
        h.Notes,
        h.Outcome,
        [.. h.Reschedules.OrderBy(r => r.ChangedAt).Select(r => new HearingRescheduleResponse(r.PreviousDate, r.NewDate, r.Reason, r.ChangedBy, r.ChangedAt))],
        h.CreatedAt);
}
