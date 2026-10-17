using System.Text.Json.Nodes;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Contracts.CustomFields;

/// <summary>
/// Entidades que aceitam campos customizados (decisão de 2026-10-17). O valor é o nome usado na API e gravado na
/// definição; o módulo dono da entidade guarda os valores na coluna <c>custom_fields jsonb</c>.
/// </summary>
public static class CustomFieldEntities
{
    /// <summary>Processos e casos (Etapa 5).</summary>
    public const string Cases = "cases";

    /// <summary>Pessoas e clientes (Etapa 4).</summary>
    public const string People = "people";

    /// <summary>Contratos de honorários (Etapa 10).</summary>
    public const string FeeAgreements = "fee_agreements";

    /// <summary>Oportunidades do CRM (Etapa 4).</summary>
    public const string Opportunities = "opportunities";

    public static IReadOnlyList<string> All { get; } = [Cases, People, FeeAgreements, Opportunities];

    public static bool Exists(string? entity) => entity is not null && All.Contains(entity, StringComparer.Ordinal);
}

/// <summary>Chaves das tabelas auxiliares do sistema (RF-03), criadas no onboarding de cada escritório.</summary>
public static class SystemLookupTables
{
    public const string ActionTypes = "action_types";
    public const string CasePhases = "case_phases";
    public const string Claims = "claims";
    public const string ClosingReasons = "closing_reasons";
    public const string ExpenseTypes = "expense_types";
    public const string TaskTypes = "task_types";
}

/// <summary>Valor inválido de um campo customizado; <see cref="Code"/> é estável (<c>custom_fields.*</c>).</summary>
public sealed record CustomFieldError(string Key, string Code, string Message);

/// <summary>Resultado da validação: os valores normalizados (só campos conhecidos) e os erros por campo.</summary>
public sealed record CustomFieldValidation(JsonObject Values, IReadOnlyList<CustomFieldError> Errors)
{
    /// <summary>Prefixo dos campos no <c>errors</c> do ProblemDetails: <c>customFields.{chave}</c>.</summary>
    public const string FieldPrefix = "customFields";

    public bool IsValid => Errors.Count == 0;

    /// <summary>Erro de validação (400) com um item por campo, para o módulo devolver como devolve os próprios.</summary>
    public ValidationError ToValidationError() =>
        new([.. Errors.Select(e => new FieldError($"{FieldPrefix}.{e.Key}", e.Code, e.Message))]);
}

/// <summary>
/// Valida os campos customizados de uma entidade da organização ativa contra as definições do escritório, antes de o
/// módulo dono gravar a coluna <c>custom_fields</c>. Formato dos valores: texto (string), número e moeda (number),
/// data (<c>yyyy-MM-dd</c>), lista (id do item, ou lista de ids se aceitar vários) e usuário (id do usuário membro).
/// </summary>
public interface ICustomFieldValidator
{
    Task<CustomFieldValidation> ValidateAsync(string entity, JsonObject? values, CancellationToken cancellationToken);
}
