namespace LexFlow.SharedKernel.Results;

/// <summary>Categoria do erro; define o status HTTP no mapeamento para ProblemDetails.</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    BusinessRule,
}

/// <summary>
/// Erro de negócio com código estável (<c>modulo.erro</c>) usado pelo frontend para tradução.
/// Erros são constantes estáticas por módulo (ex.: <c>CaseErrors.ClientNotFound</c>).
/// </summary>
#pragma warning disable CA1716 // "Error" é palavra reservada em VB; o nome é o da seção 4.3 e o código é só C#
public record Error(string Code, string Message, ErrorType Type)
#pragma warning restore CA1716
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);
}

/// <summary>Falha em um campo específico de uma requisição.</summary>
public sealed record FieldError(string Field, string Code, string Message);

/// <summary>Erro de validação com a lista de campos inválidos.</summary>
public sealed record ValidationError : Error
{
    public const string DefaultCode = "validation.failed";

    public ValidationError(IReadOnlyList<FieldError> errors)
        : base(DefaultCode, "Um ou mais campos são inválidos.", ErrorType.Validation)
    {
        Errors = errors;
    }

    public IReadOnlyList<FieldError> Errors { get; }
}
