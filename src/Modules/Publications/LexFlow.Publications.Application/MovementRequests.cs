using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public sealed record MovementResponse(Guid Id, Guid LegalCaseId, DateTimeOffset OccurredAt, DateTimeOffset CollectedAt, MovementOrigin Origin, string Description)
{
    public static MovementResponse Of(CaseMovement m) => new(m.Id, m.LegalCaseId, m.OccurredAt, m.CollectedAt, m.Origin, m.Description);
}
public sealed record MovementPosition(DateTimeOffset OccurredAt, Guid Id);
public sealed record MovementsPage(IReadOnlyList<MovementResponse> Items, MovementPosition? Next);
public sealed record ListCaseMovementsQuery(Guid CaseId, MovementPosition? After, int Limit) : IQuery<MovementsPage>;
public sealed record CreateCaseMovementCommand(Guid CaseId, MovementInput Input) : ICommand<MovementResponse>;

internal sealed class ListMovementsValidator : AbstractValidator<ListCaseMovementsQuery>
{
    public ListMovementsValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}
internal sealed class CreateMovementValidator : AbstractValidator<CreateCaseMovementCommand>
{
    public CreateMovementValidator() => RuleFor(q => q.Input).NotNull();
}
internal sealed class MovementAuthorizers(ICaseDirectory cases) :
    IResourceAuthorizer<ListCaseMovementsQuery>, IResourceAuthorizer<CreateCaseMovementCommand>
{
    public Task<Result> AuthorizeAsync(ListCaseMovementsQuery request, CancellationToken cancellationToken) =>
        cases.AuthorizeAsync(request.CaseId, "cases.read", cancellationToken);
    public Task<Result> AuthorizeAsync(CreateCaseMovementCommand request, CancellationToken cancellationToken) =>
        cases.AuthorizeAsync(request.CaseId, "cases.edit", cancellationToken);
}
internal sealed class ListCaseMovementsHandler(IPublicationsDbContext db) : IQueryHandler<ListCaseMovementsQuery, MovementsPage>
{
    public async Task<Result<MovementsPage>> Handle(ListCaseMovementsQuery query, CancellationToken cancellationToken)
    {
        var movements = db.Movements.AsNoTracking().Where(m => m.LegalCaseId == query.CaseId);
        if (query.After is { } after)
        {
            movements = movements.Where(m => m.OccurredAt < after.OccurredAt || (m.OccurredAt == after.OccurredAt && m.Id.CompareTo(after.Id) < 0));
        }

        var page = await movements.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id).Take(query.Limit + 1).ToListAsync(cancellationToken);
        var last = page.Count > query.Limit ? page[query.Limit - 1] : null;
        return new MovementsPage(page.Take(query.Limit).Select(MovementResponse.Of).ToList(),
            last is null ? null : new MovementPosition(last.OccurredAt, last.Id));
    }
}
internal sealed class CreateCaseMovementHandler(IPublicationsDbContext db, IClock clock) : ICommandHandler<CreateCaseMovementCommand, MovementResponse>
{
    public async Task<Result<MovementResponse>> Handle(CreateCaseMovementCommand command, CancellationToken cancellationToken)
    {
        var movement = CaseMovement.Create(command.CaseId, command.Input, clock.UtcNow);
        if (movement.IsFailure) { return movement.Error; }
        db.Movements.Add(movement.Value);
        await db.SaveChangesAsync(cancellationToken);
        return MovementResponse.Of(movement.Value);
    }
}
