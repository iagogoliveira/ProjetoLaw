using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Domain.Hearings;

public enum HearingType
{
    Conciliation,
    Instruction,

    /// <summary>Audiência una (conciliação, instrução e julgamento no mesmo ato).</summary>
    Combined,
    Judgment,
    Other,
}

public enum HearingFormat
{
    InPerson,
    Virtual,
    Hybrid,
}

/// <summary>Uma audiência redesignada continua agendada; as datas anteriores ficam no histórico.</summary>
public enum HearingStatus
{
    Scheduled,
    Held,
    Cancelled,
}

/// <summary>
/// Dados da audiência: tipo, formato, data e hora, local (presencial e híbrida) e link (virtual e híbrida), advogado
/// designado (membro do escritório), preposto e testemunhas (pessoas do cadastro).
/// </summary>
public sealed record HearingInput(
    HearingType Type,
    HearingFormat Format,
    DateTimeOffset ScheduledAt,
    string? Location,
    string? MeetingLink,
    Guid? LawyerUserId,
    string? Representative,
    IReadOnlyList<Guid>? WitnessPersonIds,
    string? Notes);

/// <summary>
/// Audiência do processo (RF-12, parte básica; decisão de 2026-10-18): a redesignação muda a data da mesma audiência e
/// guarda a anterior com o motivo. Realizada ou cancelada, não muda mais. Checklist, correspondentes e a agenda chegam
/// na Etapa 7.
/// </summary>
public sealed class Hearing : AggregateRoot<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    public const int MaxTextLength = 500;
    public const int MaxNotesLength = 4000;

    private readonly List<HearingReschedule> _reschedules = [];

    private Hearing()
    {
    }

    private Hearing(Guid id, Guid caseId)
        : base(id)
    {
        LegalCaseId = caseId;
        Status = HearingStatus.Scheduled;
    }

    public Guid OrganizationId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public HearingType Type { get; private set; }

    public HearingFormat Format { get; private set; }

    public HearingStatus Status { get; private set; }

    /// <summary>Data e hora, guardada em UTC.</summary>
    public DateTimeOffset ScheduledAt { get; private set; }

    public string? Location { get; private set; }

    public string? MeetingLink { get; private set; }

    public Guid? LawyerUserId { get; private set; }

    /// <summary>Preposto (quem representa a empresa cliente na audiência).</summary>
    public string? Representative { get; private set; }

    /// <summary>Testemunhas (pessoas do cadastro do Crm).</summary>
    public IReadOnlyList<Guid> WitnessPersonIds { get; private set; } = [];

    public string? Notes { get; private set; }

    /// <summary>Resultado ou ata, quando realizada; motivo, quando cancelada.</summary>
    public string? Outcome { get; private set; }

    public IReadOnlyCollection<HearingReschedule> Reschedules => _reschedules.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;

    public static Result<Hearing> Schedule(Guid id, Guid caseId, HearingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hearing = new Hearing(id, caseId);
        var applied = hearing.Apply(input, keepDate: false);
        return applied.IsFailure ? applied.Error! : hearing;
    }

    /// <summary>Troca os dados da audiência agendada. A data só muda pela redesignação.</summary>
    public Result Update(HearingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Status != HearingStatus.Scheduled ? LitigationErrors.HearingClosed : Apply(input, keepDate: true);
    }

    public Result Reschedule(DateTimeOffset newDate, string? reason, Guid userId, DateTimeOffset now)
    {
        if (Status != HearingStatus.Scheduled)
        {
            return LitigationErrors.HearingClosed;
        }

        newDate = newDate.ToUniversalTime();
        if (newDate == ScheduledAt)
        {
            return LitigationErrors.HearingSameDate;
        }

        _reschedules.Add(new HearingReschedule(Id, LegalCaseId, ScheduledAt, newDate, Clean(reason), userId, now));
        ScheduledAt = newDate;
        return Result.Success;
    }

    /// <summary>Registra a audiência como realizada, com o resultado ou a ata.</summary>
    public Result Hold(string? outcome) => Close(HearingStatus.Held, outcome);

    public Result Cancel(string? reason) => Close(HearingStatus.Cancelled, reason);

    private Result Close(HearingStatus status, string? text)
    {
        if (Status != HearingStatus.Scheduled)
        {
            return LitigationErrors.HearingClosed;
        }

        Status = status;
        Outcome = Clean(text);
        return Result.Success;
    }

    private Result Apply(HearingInput input, bool keepDate)
    {
        var location = Clean(input.Location);
        var link = Clean(input.MeetingLink);
        var needsPlace = input.Format is HearingFormat.InPerson or HearingFormat.Hybrid;
        var needsLink = input.Format is HearingFormat.Virtual or HearingFormat.Hybrid;
        if ((needsPlace && location is null) || (needsLink && link is null))
        {
            return LitigationErrors.HearingPlaceRequired;
        }

        if (link is not null && !IsWebLink(link))
        {
            return LitigationErrors.HearingLinkInvalid;
        }

        Type = input.Type;
        Format = input.Format;
        if (!keepDate)
        {
            ScheduledAt = input.ScheduledAt.ToUniversalTime();
        }

        Location = needsPlace ? location : null;
        MeetingLink = needsLink ? link : null;
        LawyerUserId = input.LawyerUserId;
        Representative = Clean(input.Representative);
        WitnessPersonIds = [.. (input.WitnessPersonIds ?? []).Distinct()];
        Notes = Clean(input.Notes);
        return Result.Success;
    }

    private static bool IsWebLink(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Uma redesignação: a data anterior, a nova, o motivo, quem e quando.</summary>
public sealed class HearingReschedule : ITenantOwned, ICaseScoped
{
    private HearingReschedule()
    {
    }

    internal HearingReschedule(Guid hearingId, Guid caseId, DateTimeOffset previousDate, DateTimeOffset newDate, string? reason, Guid changedBy, DateTimeOffset changedAt)
    {
        Id = Guid.CreateVersion7();
        HearingId = hearingId;
        LegalCaseId = caseId;
        PreviousDate = previousDate;
        NewDate = newDate;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid HearingId { get; private set; }

    public Guid LegalCaseId { get; private set; }

    public DateTimeOffset PreviousDate { get; private set; }

    public DateTimeOffset NewDate { get; private set; }

    public string? Reason { get; private set; }

    public Guid ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    Guid? ICaseScoped.CaseId => LegalCaseId;
}
