using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Domain.CustomFields;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.CustomFields;

/// <summary>Códigos estáveis dos erros de valor de campo customizado, traduzidos no frontend.</summary>
public static class CustomFieldValueCodes
{
    public const string Required = "custom_fields.required";
    public const string UnknownField = "custom_fields.unknown_field";
    public const string InvalidValue = "custom_fields.invalid_value";
    public const string TooLong = "custom_fields.too_long";
    public const string BelowMinimum = "custom_fields.below_minimum";
    public const string AboveMaximum = "custom_fields.above_maximum";
    public const string TooManyDecimals = "custom_fields.too_many_decimals";
    public const string InvalidDate = "custom_fields.invalid_date";
    public const string UnknownOption = "custom_fields.unknown_option";
    public const string UnknownUser = "custom_fields.unknown_user";

    public static string MessageFor(string code) => code switch
    {
        Required => "Preencha este campo.",
        UnknownField => "Campo inexistente para esta entidade.",
        TooLong => "Texto maior que o permitido.",
        BelowMinimum => "Valor abaixo do mínimo.",
        AboveMaximum => "Valor acima do máximo.",
        TooManyDecimals => "Casas decimais demais.",
        InvalidDate => "Data inválida (use AAAA-MM-DD).",
        UnknownOption => "Opção inexistente ou desativada.",
        UnknownUser => "Usuário não é membro do escritório.",
        _ => "Valor inválido para o tipo do campo.",
    };
}

/// <summary>
/// Regras de valor por tipo, sem acesso a banco (quem chama diz se a opção está ativa na tabela do campo e se o
/// usuário é membro). Campos inativos
/// mantêm o valor gravado sem validar; chave desconhecida é erro, para não gravar lixo na coluna.
/// </summary>
public static class CustomFieldValueRules
{
    public static CustomFieldValidation Validate(
        IReadOnlyCollection<CustomFieldDefinition> definitions,
        JsonObject? values,
        Func<Guid, Guid, bool> isActiveOption,
        Func<Guid, bool> isMember)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(isActiveOption);
        ArgumentNullException.ThrowIfNull(isMember);
        values ??= [];
        var byKey = definitions.ToDictionary(d => d.Key, StringComparer.Ordinal);
        var normalized = new JsonObject();
        var errors = new List<CustomFieldError>();

        void Fail(string key, string code) => errors.Add(new CustomFieldError(key, code, CustomFieldValueCodes.MessageFor(code)));

        foreach (var (key, _) in values.Where(v => !byKey.ContainsKey(v.Key)))
        {
            Fail(key, CustomFieldValueCodes.UnknownField);
        }

        foreach (var definition in definitions.OrderBy(d => d.Position))
        {
            var node = values[definition.Key];
            if (!definition.IsActive)
            {
                if (node is not null)
                {
                    normalized[definition.Key] = node.DeepClone();
                }

                continue;
            }

            if (IsEmpty(node))
            {
                if (definition.Required)
                {
                    Fail(definition.Key, CustomFieldValueCodes.Required);
                }

                continue;
            }

            var (value, code) = Normalize(definition, node!, isActiveOption, isMember);
            if (code is not null)
            {
                Fail(definition.Key, code);
            }
            else
            {
                normalized[definition.Key] = value;
            }
        }

        return new CustomFieldValidation(normalized, errors);
    }

    private static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => string.IsNullOrWhiteSpace(v.GetValue<string>()),
        JsonArray a => a.Count == 0,
        _ => false,
    };

    private static (JsonNode? Value, string? Code) Normalize(CustomFieldDefinition definition, JsonNode node, Func<Guid, Guid, bool> isActiveOption, Func<Guid, bool> isMember)
    {
        switch (definition.Type)
        {
            case CustomFieldType.Text:
                if (!TryString(node, out var text))
                {
                    return (null, CustomFieldValueCodes.InvalidValue);
                }

                text = text.Trim();
                return text.Length > definition.MaxLength ? (null, CustomFieldValueCodes.TooLong) : (JsonValue.Create(text), null);

            case CustomFieldType.Number:
            case CustomFieldType.Currency:
                return Number(definition, node);

            case CustomFieldType.Date:
                return TryString(node, out var date) && DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? (JsonValue.Create(parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), null)
                    : (null, CustomFieldValueCodes.InvalidDate);

            case CustomFieldType.List:
                return Options(definition, node, isActiveOption);

            default:
                if (!TryGuid(node, out var user))
                {
                    return (null, CustomFieldValueCodes.InvalidValue);
                }

                return isMember(user) ? (JsonValue.Create(user), null) : (null, CustomFieldValueCodes.UnknownUser);
        }
    }

    private static (JsonNode? Value, string? Code) Number(CustomFieldDefinition definition, JsonNode node)
    {
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.Number || !value.TryGetValue<decimal>(out var number))
        {
            return (null, CustomFieldValueCodes.InvalidValue);
        }

        if (Math.Round(number, definition.Decimals ?? 0) != number)
        {
            return (null, CustomFieldValueCodes.TooManyDecimals);
        }

        if (definition.Minimum is { } min && number < min)
        {
            return (null, CustomFieldValueCodes.BelowMinimum);
        }

        return definition.Maximum is { } max && number > max ? (null, CustomFieldValueCodes.AboveMaximum) : (JsonValue.Create(number), null);
    }

    private static (JsonNode? Value, string? Code) Options(CustomFieldDefinition definition, JsonNode node, Func<Guid, Guid, bool> isActiveOption)
    {
        var raw = node is JsonArray array ? [.. array] : new List<JsonNode?> { node };
        if (!definition.AllowMultiple && node is JsonArray)
        {
            return (null, CustomFieldValueCodes.InvalidValue);
        }

        var options = new List<Guid>();
        foreach (var item in raw)
        {
            if (item is null || !TryGuid(item, out var option))
            {
                return (null, CustomFieldValueCodes.InvalidValue);
            }

            if (!isActiveOption(definition.LookupTableId!.Value, option))
            {
                return (null, CustomFieldValueCodes.UnknownOption);
            }

            if (!options.Contains(option))
            {
                options.Add(option);
            }
        }

        return definition.AllowMultiple
            ? (new JsonArray([.. options.Select(o => (JsonNode?)JsonValue.Create(o))]), null)
            : (JsonValue.Create(options[0]), null);
    }

    private static bool TryString(JsonNode node, out string text)
    {
        text = string.Empty;
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            text = value.GetValue<string>();
            return true;
        }

        return false;
    }

    private static bool TryGuid(JsonNode node, out Guid id)
    {
        id = Guid.Empty;
        return TryString(node, out var text) && Guid.TryParse(text, out id);
    }
}

/// <summary>
/// <see cref="ICustomFieldValidator"/> sobre as definições da organização ativa: carrega as opções das tabelas usadas e
/// confere usuários pela pertença (com cache).
/// </summary>
internal sealed class CustomFieldValidator(IPlatformDbContext db, ITenantContext tenant, IMembershipLookup memberships) : ICustomFieldValidator
{
    public async Task<CustomFieldValidation> ValidateAsync(string entity, JsonObject? values, CancellationToken cancellationToken)
    {
        var definitions = await db.CustomFields.AsNoTracking().Where(d => d.Entity == entity).ToListAsync(cancellationToken);

        var tables = definitions.Where(d => d.IsActive && d.LookupTableId is not null).Select(d => d.LookupTableId!.Value).Distinct().ToList();
        var options = tables.Count == 0
            ? []
            : (await db.LookupItems.AsNoTracking().Where(i => i.IsActive && tables.Contains(i.TableId)).Select(i => new { i.TableId, i.Id }).ToListAsync(cancellationToken))
                .Select(i => (i.TableId, i.Id)).ToHashSet();

        var users = new List<Guid>();
        foreach (var definition in definitions.Where(d => d.IsActive && d.Type == CustomFieldType.User))
        {
            if (values?[definition.Key] is JsonValue value && value.GetValueKind() == JsonValueKind.String && Guid.TryParse(value.GetValue<string>(), out var user))
            {
                users.Add(user);
            }
        }

        var organization = tenant.RequiredOrganizationId;
        var members = new HashSet<Guid>();
        foreach (var user in users.Distinct())
        {
            if ((await memberships.GetAsync(user, cancellationToken)).OrganizationIds.Contains(organization))
            {
                members.Add(user);
            }
        }

        return CustomFieldValueRules.Validate(definitions, values, (table, item) => options.Contains((table, item)), members.Contains);
    }
}
