using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Indexes;
using LexFlow.Platform.Application.Preferences;
using LexFlow.Platform.Contracts.Indexes;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Queries;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Platform.Endpoints;

/// <summary>Índices monetários da plataforma e a calculadora de correção (RF-13), com a permissão de ver processos.</summary>
internal static class MonetaryIndexEndpoints
{
    private static readonly Error InvalidIndex = Error.Validation("platform.monetary_index_invalid", "Índice inválido.");

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapPreferences(endpoints);

        endpoints.MapGet("/api/v1/monetary-indexes", ListAsync)
            .WithTags("MonetaryIndexes")
            .DocumentEnumQuery<MonetaryIndexCode>("index")
            .RequirePermission(PlatformPermissions.CasesRead)
            .WithName("ListMonetaryIndexes")
            .WithSummary("Variação mensal de um índice (IPCA-E, INPC, SELIC ou TR) entre dois meses")
            .Produces<IReadOnlyList<MonetaryIndexResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/api/v1/monetary-corrections", async (MonetaryCorrectionRequest request, IQueryHandler<CalculateCorrectionQuery, MonetaryCorrectionResult> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CalculateCorrectionQuery(request), cancellationToken)).ToHttpResult())
            .WithTags("MonetaryIndexes")
            .RequirePermission(PlatformPermissions.CasesRead)
            .WithName("CalculateMonetaryCorrection")
            .WithSummary("Corrige um valor pelo índice e aplica juros simples pro rata dia, com a memória de cálculo")
            .Produces<MonetaryCorrectionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    /// <summary>
    /// Preferências de tela do usuário no escritório ativo (ex.: <c>cases.list</c>: filtros salvos e colunas da lista de
    /// processos). A chave vai na query; cada usuário só lê e grava as dele.
    /// </summary>
    private static void MapPreferences(IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/api/v1/me").WithTags("Me");

        me.MapGet("/preferences", async (string key, IQueryHandler<GetPreferenceQuery, PreferenceResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetPreferenceQuery(key), cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.ProfileRead)
            .WithName("GetMyPreference")
            .WithSummary("Uma preferência de tela do usuário (404 se ainda não salva)")
            .Produces<PreferenceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapPut("/preferences", async (SetPreferenceCommand request, ICommandHandler<SetPreferenceCommand, PreferenceResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(request, cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.ProfileManage)
            .WithName("SetMyPreference")
            .WithSummary("Cria ou troca uma preferência de tela do usuário (objeto ou lista JSON de até 16 KB)")
            .Produces<PreferenceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary><c>?index=Selic&amp;from=2025-01-01&amp;to=2025-12-01</c>; sem datas, a série inteira.</summary>
    private static async Task<IResult> ListAsync(string? index, DateOnly? from, DateOnly? to, IQueryHandler<ListMonetaryIndexesQuery, IReadOnlyList<MonetaryIndexResponse>> handler, CancellationToken cancellationToken)
    {
        var code = EnumQuery.Parse<MonetaryIndexCode>(index, InvalidIndex);
        if (code.IsFailure)
        {
            return code.Error.ToProblem();
        }

        if (code.Value is not { } value)
        {
            return InvalidIndex.ToProblem();
        }

        return (await handler.Handle(new ListMonetaryIndexesQuery(value, from, to), cancellationToken)).ToHttpResult();
    }
}
