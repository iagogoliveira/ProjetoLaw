using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Queries;

namespace LexFlow.Crm.Endpoints;

/// <summary>Filtros de enum na query do Crm (<c>?role=</c>, <c>?risk=</c>), com o código de erro do módulo.</summary>
internal static class QueryEnums
{
    public static readonly Error InvalidFilter = Error.Validation("crm.filter_invalid", "Filtro inválido.");

    public static Result<TEnum?> Parse<TEnum>(string? value)
        where TEnum : struct, Enum => EnumQuery.Parse<TEnum>(value, InvalidFilter);
}
