using System.Text.Json.Nodes;
using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Domain.CustomFields;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.CustomFields;

public sealed record CustomFieldResponse(
    Guid Id,
    string Entity,
    string Key,
    CustomFieldType Type,
    string Label,
    string? HelpText,
    bool Required,
    bool Filterable,
    int Position,
    bool IsActive,
    CustomFieldSettings Settings);

internal static class CustomFieldMapping
{
    public static CustomFieldResponse ToResponse(this CustomFieldDefinition d) =>
        new(d.Id, d.Entity, d.Key, d.Type, d.Label, d.HelpText, d.Required, d.Filterable, d.Position, d.IsActive, d.Settings);

    public static IRuleBuilderOptions<T, string> FieldLabel<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("platform.custom_field_label_required")
            .MaximumLength(CustomFieldDefinition.MaxLabelLength).WithErrorCode("platform.custom_field_label_too_long");

    public static IRuleBuilderOptions<T, string?> FieldHelpText<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(CustomFieldDefinition.MaxHelpTextLength).WithErrorCode("platform.custom_field_help_text_too_long");
}

/// <summary>Campos de uma entidade (ou de todas), na ordem do formulário; inclui os inativos para a tela de configuração.</summary>
public sealed record ListCustomFieldsQuery(string? Entity) : IQuery<IReadOnlyList<CustomFieldResponse>>;

internal sealed class ListCustomFieldsHandler(IPlatformDbContext db) : IQueryHandler<ListCustomFieldsQuery, IReadOnlyList<CustomFieldResponse>>
{
    public async Task<Result<IReadOnlyList<CustomFieldResponse>>> Handle(ListCustomFieldsQuery query, CancellationToken cancellationToken)
    {
        var fields = db.CustomFields.AsNoTracking();
        if (query.Entity is { } entity)
        {
            fields = fields.Where(f => f.Entity == entity);
        }

        var list = await fields.OrderBy(f => f.Entity).ThenBy(f => f.Position).ThenBy(f => f.Label).ToListAsync(cancellationToken);
        return list.Select(f => f.ToResponse()).ToList();
    }
}

/// <summary>Novo campo; chave, entidade e tipo ficam fixos depois de criado.</summary>
public sealed record CreateCustomFieldCommand(
    string Entity,
    string Key,
    CustomFieldType Type,
    string Label,
    string? HelpText,
    bool Required,
    bool Filterable,
    int Position,
    CustomFieldSettings? Settings) : ICommand<CustomFieldResponse>;

internal sealed class CreateCustomFieldValidator : AbstractValidator<CreateCustomFieldCommand>
{
    public CreateCustomFieldValidator()
    {
        RuleFor(c => c.Entity).Must(CustomFieldEntities.Exists).WithErrorCode(CustomFieldErrors.UnknownEntity.Code);
        RuleFor(c => c.Key).Must(CustomFieldDefinition.IsValidKey).WithErrorCode(CustomFieldErrors.InvalidKey.Code);
        RuleFor(c => c.Type).IsInEnum().WithErrorCode("platform.custom_field_type_invalid");
        RuleFor(c => c.Label).FieldLabel();
        RuleFor(c => c.HelpText).FieldHelpText();
    }
}

internal sealed class CreateCustomFieldHandler(IPlatformDbContext db, IIdGenerator ids) : ICommandHandler<CreateCustomFieldCommand, CustomFieldResponse>
{
    public async Task<Result<CustomFieldResponse>> Handle(CreateCustomFieldCommand command, CancellationToken cancellationToken)
    {
        if (await db.CustomFields.AnyAsync(f => f.Entity == command.Entity && f.Key == command.Key, cancellationToken))
        {
            return CustomFieldErrors.KeyTaken;
        }

        var settings = command.Settings ?? new CustomFieldSettings();
        if (command.Type == CustomFieldType.List && settings.LookupTableId is { } table
            && !await db.LookupTables.AnyAsync(t => t.Id == table && t.IsActive, cancellationToken))
        {
            return CustomFieldErrors.LookupTableNotFound;
        }

        var field = CustomFieldDefinition.Create(
            ids.NewId(), command.Entity, command.Key, command.Type, command.Label, command.HelpText, command.Required, command.Filterable, command.Position, settings);
        if (field.IsFailure)
        {
            return field.Error;
        }

        db.CustomFields.Add(field.Value);
        await db.SaveChangesAsync(cancellationToken);
        return field.Value.ToResponse();
    }
}

/// <summary>Edita rótulo, ajuda, obrigatoriedade, ordem, limites e se o campo está ativo.</summary>
public sealed record UpdateCustomFieldCommand(
    Guid Id,
    string Label,
    string? HelpText,
    bool Required,
    bool Filterable,
    int Position,
    bool IsActive,
    CustomFieldSettings? Settings) : ICommand<CustomFieldResponse>;

internal sealed class UpdateCustomFieldValidator : AbstractValidator<UpdateCustomFieldCommand>
{
    public UpdateCustomFieldValidator()
    {
        RuleFor(c => c.Label).FieldLabel();
        RuleFor(c => c.HelpText).FieldHelpText();
    }
}

internal sealed class UpdateCustomFieldHandler(IPlatformDbContext db) : ICommandHandler<UpdateCustomFieldCommand, CustomFieldResponse>
{
    public async Task<Result<CustomFieldResponse>> Handle(UpdateCustomFieldCommand command, CancellationToken cancellationToken)
    {
        var field = await db.CustomFields.SingleOrDefaultAsync(f => f.Id == command.Id, cancellationToken);
        if (field is null)
        {
            return CustomFieldErrors.NotFound;
        }

        var updated = field.Update(command.Label, command.HelpText, command.Required, command.Filterable, command.Position, command.Settings ?? field.Settings);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        field.Activate(command.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return field.ToResponse();
    }
}

public sealed record CustomFieldValuesResponse(JsonObject Values);

/// <summary>
/// Valida valores de campos customizados de uma entidade sem gravar nada: é o que os módulos fazem antes de gravar, e
/// o formulário de teste da tela de configuração usa para conferir os campos criados.
/// </summary>
public sealed record ValidateCustomFieldValuesQuery(string Entity, JsonObject? Values) : IQuery<CustomFieldValuesResponse>;

internal sealed class ValidateCustomFieldValuesValidator : AbstractValidator<ValidateCustomFieldValuesQuery>
{
    public ValidateCustomFieldValuesValidator() =>
        RuleFor(c => c.Entity).Must(CustomFieldEntities.Exists).WithErrorCode(CustomFieldErrors.UnknownEntity.Code);
}

internal sealed class ValidateCustomFieldValuesHandler(ICustomFieldValidator validator) : IQueryHandler<ValidateCustomFieldValuesQuery, CustomFieldValuesResponse>
{
    public async Task<Result<CustomFieldValuesResponse>> Handle(ValidateCustomFieldValuesQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query.Entity, query.Values, cancellationToken);
        return validation.IsValid ? new CustomFieldValuesResponse(validation.Values) : validation.ToValidationError();
    }
}
