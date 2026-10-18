using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace LexFlow.Web.Common.Queries;

/// <summary>
/// Filtros de enum na query string (<c>?role=client</c>, <c>?status=closed</c>): a API serializa enums em camelCase,
/// mas o binding padrão compara o nome exato. Aqui a comparação ignora maiúsculas; valor desconhecido vira o erro do
/// módulo (400).
/// </summary>
public static class EnumQuery
{
    public static Result<TEnum?> Parse<TEnum>(string? value, Error invalid)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Ok<TEnum?>(null);
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? Result.Ok<TEnum?>(parsed)
            : invalid;
    }

    /// <summary>
    /// O filtro chega como texto (para a comparação sem diferenciar maiúsculas), mas o contrato OpenAPI continua dizendo
    /// que é o enum: o parâmetro aponta para o schema dele, e o cliente gerado oferece só os valores válidos.
    /// </summary>
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
