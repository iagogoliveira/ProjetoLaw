using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.People;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.People;

public sealed record EconomicGroupResponse(Guid Id, string Name, int MemberCount);

public sealed record ListEconomicGroupsQuery : IQuery<IReadOnlyList<EconomicGroupResponse>>;

internal sealed class ListEconomicGroupsHandler(ICrmDbContext db) : IQueryHandler<ListEconomicGroupsQuery, IReadOnlyList<EconomicGroupResponse>>
{
    public async Task<Result<IReadOnlyList<EconomicGroupResponse>>> Handle(ListEconomicGroupsQuery query, CancellationToken cancellationToken)
    {
        var counts = await db.People.AsNoTracking().Where(p => p.EconomicGroupId != null)
            .GroupBy(p => p.EconomicGroupId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var groups = await db.EconomicGroups.AsNoTracking().OrderBy(g => g.Name).ToListAsync(cancellationToken);
        return groups.Select(g => new EconomicGroupResponse(g.Id, g.Name, counts.GetValueOrDefault(g.Id))).ToList();
    }
}

/// <summary>Cria (sem id) ou renomeia um grupo econômico; o nome é único no escritório.</summary>
public sealed record SaveEconomicGroupCommand(Guid? Id, string Name) : ICommand<EconomicGroupResponse>;

internal sealed class SaveEconomicGroupValidator : AbstractValidator<SaveEconomicGroupCommand>
{
    public SaveEconomicGroupValidator() =>
        RuleFor(c => c.Name).NotEmpty().WithErrorCode("crm.name_required")
            .MaximumLength(EconomicGroup.MaxNameLength).WithErrorCode("crm.name_too_long");
}

internal sealed class SaveEconomicGroupHandler(ICrmDbContext db, IIdGenerator ids) : ICommandHandler<SaveEconomicGroupCommand, EconomicGroupResponse>
{
    public async Task<Result<EconomicGroupResponse>> Handle(SaveEconomicGroupCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.EconomicGroups.AnyAsync(g => g.Name == name && g.Id != command.Id, cancellationToken))
        {
            return CrmErrors.EconomicGroupNameTaken;
        }

        EconomicGroup? group;
        if (command.Id is { } id)
        {
            group = await db.EconomicGroups.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
            if (group is null)
            {
                return CrmErrors.EconomicGroupNotFound;
            }

            group.Rename(name);
        }
        else
        {
            group = EconomicGroup.Create(ids.NewId(), name);
            db.EconomicGroups.Add(group);
        }

        await db.SaveChangesAsync(cancellationToken);
        var members = await db.People.CountAsync(p => p.EconomicGroupId == group.Id, cancellationToken);
        return new EconomicGroupResponse(group.Id, group.Name, members);
    }
}

/// <summary>Exclui (logicamente) um grupo sem pessoas vinculadas.</summary>
public sealed record DeleteEconomicGroupCommand(Guid Id) : ICommand;

internal sealed class DeleteEconomicGroupHandler(ICrmDbContext db) : ICommandHandler<DeleteEconomicGroupCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteEconomicGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await db.EconomicGroups.SingleOrDefaultAsync(g => g.Id == command.Id, cancellationToken);
        if (group is null)
        {
            return CrmErrors.EconomicGroupNotFound;
        }

        if (await db.People.AnyAsync(p => p.EconomicGroupId == group.Id, cancellationToken))
        {
            return CrmErrors.EconomicGroupInUse;
        }

        db.EconomicGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
