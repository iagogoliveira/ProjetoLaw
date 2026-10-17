using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace LexFlow.Crm.Endpoints;

/// <summary>
/// Filtros de enum na query string (<c>?role=client</c>, <c>?risk=direct</c>): a API serializa enums em camelCase, mas
/// o binding padrão compara o nome exato. Aqui a comparação ignora maiúsculas; valor desconhecido vira 400.
/// </summary>
internal static class QueryEnums
{
    public static readonly Error InvalidFilter = Error.Validation("crm.filter_invalid", "Filtro inválido.");

    public static Result<TEnum?> Parse<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Ok<TEnum?>(null);
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? Result.Ok<TEnum?>(parsed)
            : InvalidFilter;
    }
}

/// <summary>
/// O filtro chega como texto (para a comparação sem diferenciar maiúsculas), mas o contrato OpenAPI continua dizendo
/// que é o enum: o parâmetro aponta para o schema dele, e o cliente gerado oferece só os valores válidos.
/// </summary>
internal static class EnumQueryDocumentation
{
    public static RouteHandlerBuilder DocumentEnumQuery<TEnum>(this RouteHandlerBuilder builder, string parameter)
        where TEnum : struct, Enum =>
        builder.AddOpenApiOperationTransformer((operation, context, _) =>
        {
            if (operation.Parameters?.FirstOrDefault(p => string.Equals(p.Name, parameter, StringComparison.Ordinal)) is OpenApiParameter target)
            {
                target.Schema = new OpenApiSchemaReference(typeof(TEnum).Name, context.Document);
            }

            return Task.CompletedTask;
        });
}
