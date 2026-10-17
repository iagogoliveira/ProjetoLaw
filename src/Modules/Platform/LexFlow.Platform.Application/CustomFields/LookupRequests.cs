using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Domain.CustomFields;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.CustomFields;

public sealed record LookupItemResponse(Guid Id, Guid TableId, string Name, int Position, bool IsActive);

public sealed record LookupTableResponse(Guid Id, string Name, string? SystemKey, bool IsActive, IReadOnlyList<LookupItemResponse> Items);

internal static class LookupMapping
{
    public static LookupItemResponse ToResponse(this LookupItem i) => new(i.Id, i.TableId, i.Name, i.Position, i.IsActive);

    public static LookupTableResponse ToResponse(this LookupTable t, IEnumerable<LookupItem> items) =>
        new(t.Id, t.Name, t.SystemKey, t.IsActive, [.. items.OrderBy(i => i.Position).ThenBy(i => i.Name, StringComparer.Ordinal).Select(i => i.ToResponse())]);

    public static IRuleBuilderOptions<T, string> LookupName<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.NotEmpty().WithErrorCode("platform.lookup_name_required")
            .MaximumLength(maxLength).WithErrorCode("platform.lookup_name_too_long");
}

/// <summary>Tabelas auxiliares da organização ativa com os itens (inclusive inativos, marcados).</summary>
public sealed record ListLookupTablesQuery : IQuery<IReadOnlyList<LookupTableResponse>>;

internal sealed class ListLookupTablesHandler(IPlatformDbContext db) : IQueryHandler<ListLookupTablesQuery, IReadOnlyList<LookupTableResponse>>
{
    public async Task<Result<IReadOnlyList<LookupTableResponse>>> Handle(ListLookupTablesQuery query, CancellationToken cancellationToken)
    {
        var tables = await db.LookupTables.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var items = (await db.LookupItems.AsNoTracking().ToListAsync(cancellationToken)).ToLookup(i => i.TableId);
        return tables.Select(t => t.ToResponse(items[t.Id])).ToList();
    }
}

public sealed record CreateLookupTableCommand(string Name) : ICommand<LookupTableResponse>;

public sealed record UpdateLookupTableCommand(Guid Id, string Name, bool IsActive) : ICommand<LookupTableResponse>;

public sealed record CreateLookupItemCommand(Guid TableId, string Name, int? Position) : ICommand<LookupItemResponse>;

/// <summary>Item não é excluído (valores gravados apontam para ele): é desativado.</summary>
public sealed record UpdateLookupItemCommand(Guid Id, string Name, int Position, bool IsActive) : ICommand<LookupItemResponse>;

internal sealed class CreateLookupTableValidator : AbstractValidator<CreateLookupTableCommand>
{
    public CreateLookupTableValidator() => RuleFor(c => c.Name).LookupName(LookupTable.MaxNameLength);
}

internal sealed class UpdateLookupTableValidator : AbstractValidator<UpdateLookupTableCommand>
{
    public UpdateLookupTableValidator() => RuleFor(c => c.Name).LookupName(LookupTable.MaxNameLength);
}

internal sealed class CreateLookupItemValidator : AbstractValidator<CreateLookupItemCommand>
{
    public CreateLookupItemValidator() => RuleFor(c => c.Name).LookupName(LookupItem.MaxNameLength);
}

internal sealed class UpdateLookupItemValidator : AbstractValidator<UpdateLookupItemCommand>
{
    public UpdateLookupItemValidator() => RuleFor(c => c.Name).LookupName(LookupItem.MaxNameLength);
}

internal sealed class CreateLookupTableHandler(IPlatformDbContext db, IIdGenerator ids) : ICommandHandler<CreateLookupTableCommand, LookupTableResponse>
{
    public async Task<Result<LookupTableResponse>> Handle(CreateLookupTableCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.LookupTables.AnyAsync(t => t.Name == name, cancellationToken))
        {
            return CustomFieldErrors.LookupNameTaken;
        }

        var table = LookupTable.Create(ids.NewId(), name);
        db.LookupTables.Add(table);
        await db.SaveChangesAsync(cancellationToken);
        return table.ToResponse([]);
    }
}

internal sealed class UpdateLookupTableHandler(IPlatformDbContext db) : ICommandHandler<UpdateLookupTableCommand, LookupTableResponse>
{
    public async Task<Result<LookupTableResponse>> Handle(UpdateLookupTableCommand command, CancellationToken cancellationToken)
    {
        var table = await db.LookupTables.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (table is null)
        {
            return CustomFieldErrors.LookupTableNotFound;
        }

        var name = command.Name.Trim();
        if (await db.LookupTables.AnyAsync(t => t.Name == name && t.Id != table.Id, cancellationToken))
        {
            return CustomFieldErrors.LookupNameTaken;
        }

        var activated = table.Activate(command.IsActive);
        if (activated.IsFailure)
        {
            return activated.Error;
        }

        table.Rename(name);
        await db.SaveChangesAsync(cancellationToken);
        var items = await db.LookupItems.AsNoTracking().Where(i => i.TableId == table.Id).ToListAsync(cancellationToken);
        return table.ToResponse(items);
    }
}

internal sealed class CreateLookupItemHandler(IPlatformDbContext db, IIdGenerator ids) : ICommandHandler<CreateLookupItemCommand, LookupItemResponse>
{
    public async Task<Result<LookupItemResponse>> Handle(CreateLookupItemCommand command, CancellationToken cancellationToken)
    {
        if (!await db.LookupTables.AnyAsync(t => t.Id == command.TableId, cancellationToken))
        {
            return CustomFieldErrors.LookupTableNotFound;
        }

        var name = command.Name.Trim();
        var siblings = await db.LookupItems.Where(i => i.TableId == command.TableId).Select(i => new { i.Name, i.Position }).ToListAsync(cancellationToken);
        if (siblings.Exists(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return CustomFieldErrors.LookupNameTaken;
        }

        var item = LookupItem.Create(ids.NewId(), command.TableId, name, command.Position ?? (siblings.Count == 0 ? 1 : siblings.Max(i => i.Position) + 1));
        db.LookupItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.ToResponse();
    }
}

internal sealed class UpdateLookupItemHandler(IPlatformDbContext db) : ICommandHandler<UpdateLookupItemCommand, LookupItemResponse>
{
    public async Task<Result<LookupItemResponse>> Handle(UpdateLookupItemCommand command, CancellationToken cancellationToken)
    {
        var item = await db.LookupItems.SingleOrDefaultAsync(i => i.Id == command.Id, cancellationToken);
        if (item is null)
        {
            return CustomFieldErrors.LookupItemNotFound;
        }

        var name = command.Name.Trim();
        var taken = (await db.LookupItems.Where(i => i.TableId == item.TableId && i.Id != item.Id).Select(i => i.Name).ToListAsync(cancellationToken))
            .Exists(other => string.Equals(other, name, StringComparison.OrdinalIgnoreCase));
        if (taken)
        {
            return CustomFieldErrors.LookupNameTaken;
        }

        item.Update(name, command.Position, command.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return item.ToResponse();
    }
}
