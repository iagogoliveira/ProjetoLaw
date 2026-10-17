using System.Text.RegularExpressions;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Domain.CustomFields;

/// <summary>Tipos de campo customizado (RF-03).</summary>
public enum CustomFieldType
{
    Text,
    Number,
    Date,

    /// <summary>Uma ou mais opções de uma tabela auxiliar do escritório (decisão de 2026-10-17).</summary>
    List,

    /// <summary>Valor em reais, com 2 casas.</summary>
    Currency,

    /// <summary>Um usuário membro do escritório.</summary>
    User,
}

/// <summary>Limites e opções de um campo, conforme o tipo; o que não se aplica ao tipo fica nulo.</summary>
/// <param name="MaxLength">Texto: tamanho máximo (padrão 500).</param>
/// <param name="Minimum">Número e moeda: valor mínimo.</param>
/// <param name="Maximum">Número e moeda: valor máximo.</param>
/// <param name="Decimals">Número: casas decimais aceitas (0 a 6).</param>
/// <param name="LookupTableId">Lista: tabela auxiliar de onde vêm as opções.</param>
/// <param name="AllowMultiple">Lista: aceita mais de uma opção.</param>
public sealed record CustomFieldSettings(
    int? MaxLength = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    int? Decimals = null,
    Guid? LookupTableId = null,
    bool AllowMultiple = false);

/// <summary>
/// Campo customizado de uma entidade (processo, pessoa, contrato de honorários, oportunidade), definido pelo
/// escritório (RF-03). Os valores ficam na coluna <c>custom_fields jsonb</c> da própria entidade, indexados pela
/// <see cref="Key"/>. Por isso chave, entidade e tipo não mudam depois de criados, e o campo não é excluído: é
/// desativado (some dos formulários, e os valores gravados continuam).
/// </summary>
public sealed partial class CustomFieldDefinition : AggregateRoot<Guid>, ITenantOwned, IAuditable
{
    public const int MaxKeyLength = 50;
    public const int MaxLabelLength = 100;
    public const int MaxHelpTextLength = 300;
    public const int DefaultTextLength = 500;
    public const int MaxTextLength = 4000;
    public const int MaxDecimals = 6;

    private CustomFieldDefinition()
    {
    }

    private CustomFieldDefinition(Guid id, string entity, string key, CustomFieldType type)
        : base(id)
    {
        Entity = entity;
        Key = key;
        Type = type;
    }

    public Guid OrganizationId { get; private set; }

    /// <summary>Entidade dona do campo (ex.: <c>cases</c>); o catálogo fica em Platform.Contracts.</summary>
    public string Entity { get; private set; } = string.Empty;

    /// <summary>Chave do valor no JSON (<c>snake_case</c>), única por entidade no escritório.</summary>
    public string Key { get; private set; } = string.Empty;

    public CustomFieldType Type { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string? HelpText { get; private set; }

    public bool Required { get; private set; }

    /// <summary>Usado em filtros de lista: o módulo dono da entidade cria índice GIN para ele.</summary>
    public bool Filterable { get; private set; }

    /// <summary>Posição no formulário (crescente).</summary>
    public int Position { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int? MaxLength { get; private set; }

    public decimal? Minimum { get; private set; }

    public decimal? Maximum { get; private set; }

    public int? Decimals { get; private set; }

    public Guid? LookupTableId { get; private set; }

    public bool AllowMultiple { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public CustomFieldSettings Settings => new(MaxLength, Minimum, Maximum, Decimals, LookupTableId, AllowMultiple);

    public static bool IsValidKey(string? key) => key is { Length: > 0 and <= MaxKeyLength } && KeyPattern().IsMatch(key);

    public static Result<CustomFieldDefinition> Create(
        Guid id,
        string entity,
        string key,
        CustomFieldType type,
        string label,
        string? helpText,
        bool required,
        bool filterable,
        int position,
        CustomFieldSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        if (!IsValidKey(key))
        {
            return CustomFieldErrors.InvalidKey;
        }

        var definition = new CustomFieldDefinition(id, entity, key, type);
        var updated = definition.Update(label, helpText, required, filterable, position, settings);
        return updated.IsFailure ? updated.Error : definition;
    }

    /// <summary>Muda rótulo, ajuda, obrigatoriedade, ordem e limites. A tabela de um campo lista não muda.</summary>
    public Result Update(string label, string? helpText, bool required, bool filterable, int position, CustomFieldSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(settings);
        var checkedSettings = Check(settings);
        if (checkedSettings.IsFailure)
        {
            return checkedSettings.Error;
        }

        if (Type == CustomFieldType.List && LookupTableId is { } current && settings.LookupTableId != current)
        {
            return CustomFieldErrors.LookupTableLocked;
        }

        var normalized = checkedSettings.Value;
        Label = label.Trim();
        HelpText = string.IsNullOrWhiteSpace(helpText) ? null : helpText.Trim();
        Required = required;
        Filterable = filterable;
        Position = position;
        MaxLength = normalized.MaxLength;
        Minimum = normalized.Minimum;
        Maximum = normalized.Maximum;
        Decimals = normalized.Decimals;
        LookupTableId = normalized.LookupTableId;
        AllowMultiple = normalized.AllowMultiple;
        return Result.Success;
    }

    public void Activate(bool active) => IsActive = active;

    /// <summary>Guarda só o que vale para o tipo e confere a coerência dos limites.</summary>
    private Result<CustomFieldSettings> Check(CustomFieldSettings settings)
    {
        if (settings.Minimum is { } min && settings.Maximum is { } max && min > max)
        {
            return CustomFieldErrors.InvalidRange;
        }

        switch (Type)
        {
            case CustomFieldType.Text:
                var length = settings.MaxLength ?? DefaultTextLength;
                return length is < 1 or > MaxTextLength ? CustomFieldErrors.InvalidMaxLength : new CustomFieldSettings(MaxLength: length);
            case CustomFieldType.Number:
                var decimals = settings.Decimals ?? 0;
                return decimals is < 0 or > MaxDecimals
                    ? CustomFieldErrors.InvalidDecimals
                    : new CustomFieldSettings(Minimum: settings.Minimum, Maximum: settings.Maximum, Decimals: decimals);
            case CustomFieldType.Currency:
                return new CustomFieldSettings(Minimum: settings.Minimum, Maximum: settings.Maximum, Decimals: 2);
            case CustomFieldType.List:
                return settings.LookupTableId is null
                    ? CustomFieldErrors.LookupTableRequired
                    : new CustomFieldSettings(LookupTableId: settings.LookupTableId, AllowMultiple: settings.AllowMultiple);
            default:
                return new CustomFieldSettings();
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyPattern();
}

/// <summary>Erros das definições de campos e das tabelas auxiliares (<c>platform.*</c>).</summary>
public static class CustomFieldErrors
{
    public static readonly Error UnknownEntity =
        Error.Validation("platform.custom_field_unknown_entity", "Esta entidade não aceita campos customizados.");

    public static readonly Error InvalidKey =
        Error.Validation("platform.custom_field_key_invalid", "A chave usa só letras minúsculas, números e _, começando por letra.");

    public static readonly Error KeyTaken =
        Error.Conflict("platform.custom_field_key_taken", "Já existe um campo com esta chave nesta entidade.");

    public static readonly Error NotFound =
        Error.NotFound("platform.custom_field_not_found", "Campo customizado não encontrado.");

    public static readonly Error InvalidRange =
        Error.Validation("platform.custom_field_range_invalid", "O mínimo não pode ser maior que o máximo.");

    public static readonly Error InvalidMaxLength =
        Error.Validation("platform.custom_field_max_length_invalid", "O tamanho máximo do texto vai de 1 a 4000 caracteres.");

    public static readonly Error InvalidDecimals =
        Error.Validation("platform.custom_field_decimals_invalid", "As casas decimais vão de 0 a 6.");

    public static readonly Error LookupTableRequired =
        Error.Validation("platform.custom_field_lookup_table_required", "Escolha a tabela auxiliar com as opções da lista.");

    public static readonly Error LookupTableLocked =
        Error.BusinessRule("platform.custom_field_lookup_table_locked", "A tabela de opções de um campo lista não pode ser trocada.");

    public static readonly Error LookupTableNotFound =
        Error.NotFound("platform.lookup_table_not_found", "Tabela auxiliar não encontrada.");

    public static readonly Error LookupItemNotFound =
        Error.NotFound("platform.lookup_item_not_found", "Item da tabela auxiliar não encontrado.");

    public static readonly Error LookupNameTaken =
        Error.Conflict("platform.lookup_name_taken", "Já existe um cadastro com este nome.");

    public static readonly Error SystemTableLocked =
        Error.BusinessRule("platform.lookup_table_system_locked", "As tabelas auxiliares do sistema não podem ser desativadas.");
}
