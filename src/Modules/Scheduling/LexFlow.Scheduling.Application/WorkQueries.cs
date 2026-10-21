using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

/// <summary>Compatibility value of the original confirmed-work lists; deadline lifecycle is exposed separately.</summary>
public enum ConfirmedWorkStatus { Pending }

public sealed record DeadlineResponse(Guid Id, Guid CaseId, Guid PublicationId, string Title, DateOnly StartOn,
    DateOnly InternalDueOn, DateOnly FatalDueOn, Guid AssigneeUserId, ConfirmedWorkStatus Status, Guid ConfirmedBy, DateTimeOffset ConfirmedAt,
    WorkItemStatus LifecycleStatus);
public sealed record TaskItemResponse(Guid Id, Guid CaseId, Guid PublicationId, string Title, DateOnly DueOn,
    Guid AssigneeUserId, ConfirmedWorkStatus Status, Guid ConfirmedBy, DateTimeOffset ConfirmedAt);
public sealed record ListCaseDeadlinesQuery(Guid CaseId) : IQuery<IReadOnlyList<DeadlineResponse>>;
public sealed record ListCaseTasksQuery(Guid CaseId) : IQuery<IReadOnlyList<TaskItemResponse>>;

internal sealed class WorkQueryAuthorizers(ICaseDirectory cases) :
    IResourceAuthorizer<ListCaseDeadlinesQuery>, IResourceAuthorizer<ListCaseTasksQuery>
{
    public Task<Result> AuthorizeAsync(ListCaseDeadlinesQuery request, CancellationToken cancellationToken) =>
        cases.AuthorizeAsync(request.CaseId, "schedule.deadlines.read", cancellationToken);
    public Task<Result> AuthorizeAsync(ListCaseTasksQuery request, CancellationToken cancellationToken) =>
        cases.AuthorizeAsync(request.CaseId, "schedule.tasks.read", cancellationToken);
}

internal sealed class ListCaseDeadlinesHandler(ISchedulingDbContext db) : IQueryHandler<ListCaseDeadlinesQuery, IReadOnlyList<DeadlineResponse>>
{
    public async Task<Result<IReadOnlyList<DeadlineResponse>>> Handle(ListCaseDeadlinesQuery query, CancellationToken cancellationToken) =>
        await db.Deadlines.AsNoTracking().Where(d => d.LegalCaseId == query.CaseId).OrderBy(d => d.FatalDueOn).ThenBy(d => d.Id)
            .Select(d => new DeadlineResponse(d.Id, d.LegalCaseId, d.PublicationId ?? Guid.Empty, d.Title, d.StartOn, d.InternalDueOn, d.FatalDueOn,
                d.AssigneeUserId, ConfirmedWorkStatus.Pending, d.ConfirmedBy, d.ConfirmedAt, d.Status)).ToListAsync(cancellationToken);
}

internal sealed class ListCaseTasksHandler(ISchedulingDbContext db) : IQueryHandler<ListCaseTasksQuery, IReadOnlyList<TaskItemResponse>>
{
    public async Task<Result<IReadOnlyList<TaskItemResponse>>> Handle(ListCaseTasksQuery query, CancellationToken cancellationToken) =>
        await db.Tasks.AsNoTracking().Where(t => t.LegalCaseId == query.CaseId).OrderBy(t => t.DueOn).ThenBy(t => t.Id)
            .Select(t => new TaskItemResponse(t.Id, t.LegalCaseId, t.PublicationId, t.Title, t.DueOn,
                t.AssigneeUserId, ConfirmedWorkStatus.Pending, t.ConfirmedBy, t.ConfirmedAt)).ToListAsync(cancellationToken);
}
