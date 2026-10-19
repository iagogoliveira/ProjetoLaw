using System.Text.RegularExpressions;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Domain.Preferences;

/// <summary>
/// Preferência de tela do usuário no escritório (decisão de 2026-10-18): ex.: <c>cases.list</c> guarda os filtros
/// salvos e as colunas da lista de processos. O valor é um JSON (objeto ou lista) de até 16 KB, que só a tela entende;
/// cada usuário vê e altera só as dele. Fora da trilha de auditoria: é configuração pessoal de tela.
/// </summary>
[NotAudited]
public sealed partial class UserPreference : ITenantOwned
{
    public const int MaxKeyLength = 100;
    public const int MaxValueLength = 16 * 1024;

    public static readonly Error KeyInvalid =
        Error.Validation("platform.preference_key_invalid", "Chave de preferência inválida (letras minúsculas, números, \".\", \"-\" e \"_\").");

    public static readonly Error ValueInvalid =
        Error.Validation("platform.preference_value_invalid", "A preferência precisa ser um objeto ou uma lista JSON de até 16 KB.");

    public static readonly Error NotFound = Error.NotFound("platform.preference_not_found", "Nenhuma preferência salva com esta chave.");

    private UserPreference()
    {
    }

    private UserPreference(Guid id, Guid userId, string key)
    {
        Id = id;
        UserId = userId;
        Key = key;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = "{}";

    public DateTimeOffset UpdatedAt { get; private set; }

    public static bool IsValidKey(string? key) => key is { Length: > 0 and <= MaxKeyLength } && KeyPattern().IsMatch(key);

    public static Result<UserPreference> Create(Guid id, Guid userId, string key, string json, DateTimeOffset now)
    {
        if (!IsValidKey(key))
        {
            return KeyInvalid;
        }

        var preference = new UserPreference(id, userId, key);
        var set = preference.Set(json, now);
        return set.IsFailure ? set.Error! : preference;
    }

    /// <summary>Troca o valor; o JSON já vem conferido (objeto ou lista) pela aplicação, aqui só o tamanho.</summary>
    public Result Set(string json, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxValueLength)
        {
            return ValueInvalid;
        }

        Value = json;
        UpdatedAt = now;
        return Result.Success;
    }

    [GeneratedRegex("^[a-z][a-z0-9._-]*$", RegexOptions.None, 1000)]
    private static partial Regex KeyPattern();
}
