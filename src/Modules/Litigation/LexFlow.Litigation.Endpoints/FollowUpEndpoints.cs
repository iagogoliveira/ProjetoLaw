using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Hearings;
using LexFlow.Litigation.Application.Risk;
using LexFlow.Litigation.Application.Settlements;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.Litigation.Domain.Risk;
using LexFlow.Litigation.Domain.Settlements;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Litigation.Endpoints;

public sealed record StartSettlementRequest(SettlementDirection Direction, string? Notes, OfferInput? FirstOffer);

public sealed record SubmitSettlementRequest(decimal AgreedAmount, IReadOnlyList<InstallmentInput>? Installments);

public sealed record ReasonRequest(string? Reason);

public sealed record HomologateSettlementRequest(DateOnly HomologatedOn);

public sealed record PayInstallmentRequest(DateOnly PaidOn, decimal PaidAmount);

public sealed record RescheduleHearingRequest(DateTimeOffset NewDate, string? Reason);

public sealed record HearingOutcomeRequest(string? Text);

/// <summary>Risco (RF-14), acordos (RF-16) e audiências (RF-12) do processo (Etapa 5, PR 5C2).</summary>
internal static class FollowUpEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var cases = endpoints.MapGroup("/api/v1/cases").WithTags("Cases");
        MapRisk(cases);
        MapSettlements(cases);
        MapSettlementActions(cases);
        MapHearings(cases);
    }

    private static void MapRisk(RouteGroupBuilder cases)
    {
        cases.MapGet("/{id:guid}/risk", async (Guid id, IQueryHandler<GetCaseRiskQuery, CaseRiskResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetCaseRiskQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("GetCaseRisk")
            .WithSummary("Risco atual do processo e de cada pedido, com a provisão e o passivo contingente (CPC 25)")
            .Produces<CaseRiskResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        cases.MapGet("/{id:guid}/risk/history", async (Guid id, IQueryHandler<GetRiskHistoryQuery, IReadOnlyList<RiskAssessmentResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetRiskHistoryQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("GetCaseRiskHistory")
            .WithSummary("Todas as avaliações de risco do processo, da mais recente à mais antiga")
            .Produces<IReadOnlyList<RiskAssessmentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        cases.MapPost("/{id:guid}/risk-assessments", async (Guid id, RiskInput request, ICommandHandler<AssessRiskCommand, RiskAssessmentResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new AssessRiskCommand(id, request), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/cases/{id:D}/risk", created)))
            .RequirePermission(LitigationPermissions.CasesRiskAssess)
            .WithName("AssessCaseRisk")
            .WithSummary("Registra uma avaliação de risco do processo ou de um pedido")
            .Produces<RiskAssessmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapSettlements(RouteGroupBuilder cases)
    {
        cases.MapGet("/{id:guid}/settlements", async (Guid id, IQueryHandler<ListSettlementsQuery, IReadOnlyList<SettlementResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListSettlementsQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("ListCaseSettlements")
            .WithSummary("Acordos do processo com as rodadas, as parcelas e o valor pedido comparado com o acordado")
            .Produces<IReadOnlyList<SettlementResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        cases.MapPost("/{id:guid}/settlements", async (Guid id, StartSettlementRequest request, ICommandHandler<StartSettlementCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new StartSettlementCommand(id, request.Direction, request.Notes, request.FirstOffer), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/cases/{id:D}/settlements", created)))
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("StartCaseSettlement")
            .WithSummary("Abre uma negociação de acordo, com a primeira proposta se houver")
            .Produces<SettlementResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        Action(cases, "offers", "AddSettlementOffer", "Registra uma proposta ou contraproposta", LitigationPermissions.CasesEdit,
            async (Guid id, Guid settlementId, OfferInput request, ICommandHandler<AddSettlementOfferCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new AddSettlementOfferCommand(id, settlementId, request), cancellationToken)).ToHttpResult());

        Action(cases, "submit", "SubmitSettlement", "Envia os termos finais (valor e parcelas) à aprovação interna", LitigationPermissions.CasesEdit,
            async (Guid id, Guid settlementId, SubmitSettlementRequest request, ICommandHandler<SubmitSettlementCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new SubmitSettlementCommand(id, settlementId, request.AgreedAmount, request.Installments), cancellationToken)).ToHttpResult());

        Action(cases, "approve", "ApproveSettlement", "Aprova os termos finais do acordo", LitigationPermissions.CasesSettlementsApprove,
            async (Guid id, Guid settlementId, ICommandHandler<ApproveSettlementCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ApproveSettlementCommand(id, settlementId), cancellationToken)).ToHttpResult());
    }

    private static void MapSettlementActions(RouteGroupBuilder cases)
    {
        Action(cases, "return", "ReturnSettlement", "Devolve o acordo à negociação, com o motivo", LitigationPermissions.CasesEdit,
            async (Guid id, Guid settlementId, ReasonRequest request, ICommandHandler<ReturnSettlementCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ReturnSettlementCommand(id, settlementId, request.Reason), cancellationToken)).ToHttpResult());

        Action(cases, "homologate", "HomologateSettlement", "Registra a homologação do acordo aprovado; as parcelas vão ao financeiro", LitigationPermissions.CasesEdit,
            async (Guid id, Guid settlementId, HomologateSettlementRequest request, ICommandHandler<HomologateSettlementCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new HomologateSettlementCommand(id, settlementId, request.HomologatedOn), cancellationToken)).ToHttpResult());

        Action(cases, "reject", "RejectSettlement", "Encerra a negociação sem acordo", LitigationPermissions.CasesEdit,
            async (Guid id, Guid settlementId, ReasonRequest request, ICommandHandler<EndSettlementCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new EndSettlementCommand(id, settlementId, Cancel: false, request.Reason), cancellationToken)).ToHttpResult());

        Action(cases, "cancel", "CancelSettlement", "Cancela o acordo ainda não homologado", LitigationPermissions.CasesEdit,
            async (Guid id, Guid settlementId, ReasonRequest request, ICommandHandler<EndSettlementCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new EndSettlementCommand(id, settlementId, Cancel: true, request.Reason), cancellationToken)).ToHttpResult());

        cases.MapPost("/{id:guid}/settlements/{settlementId:guid}/installments/{installmentId:guid}/pay",
                async (Guid id, Guid settlementId, Guid installmentId, PayInstallmentRequest request, ICommandHandler<PayInstallmentCommand, SettlementResponse> handler, CancellationToken cancellationToken) =>
                    (await handler.Handle(new PayInstallmentCommand(id, settlementId, installmentId, request.PaidOn, request.PaidAmount), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("PaySettlementInstallment")
            .WithSummary("Dá baixa manual numa parcela do acordo homologado; com todas pagas, o acordo está cumprido")
            .Produces<SettlementResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapHearings(RouteGroupBuilder cases)
    {
        cases.MapGet("/{id:guid}/hearings", async (Guid id, IQueryHandler<ListHearingsQuery, IReadOnlyList<HearingResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListHearingsQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("ListCaseHearings")
            .WithSummary("Audiências do processo pela data, com o histórico das redesignações")
            .Produces<IReadOnlyList<HearingResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        cases.MapPost("/{id:guid}/hearings", async (Guid id, HearingInput request, ICommandHandler<ScheduleHearingCommand, HearingResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ScheduleHearingCommand(id, request), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/cases/{id:D}/hearings", created)))
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("ScheduleCaseHearing")
            .WithSummary("Agenda uma audiência")
            .Produces<HearingResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        HearingRoute(cases.MapPut("/{id:guid}/hearings/{hearingId:guid}",
                async (Guid id, Guid hearingId, HearingInput request, ICommandHandler<UpdateHearingCommand, HearingResponse> handler, CancellationToken cancellationToken) =>
                    (await handler.Handle(new UpdateHearingCommand(id, hearingId, request), cancellationToken)).ToHttpResult()),
            "UpdateCaseHearing", "Troca os dados da audiência agendada (a data muda pela redesignação)");

        HearingRoute(cases.MapPost("/{id:guid}/hearings/{hearingId:guid}/reschedule",
                async (Guid id, Guid hearingId, RescheduleHearingRequest request, ICommandHandler<RescheduleHearingCommand, HearingResponse> handler, CancellationToken cancellationToken) =>
                    (await handler.Handle(new RescheduleHearingCommand(id, hearingId, request.NewDate, request.Reason), cancellationToken)).ToHttpResult()),
            "RescheduleCaseHearing", "Redesigna a audiência; a data anterior fica no histórico com o motivo");

        HearingRoute(cases.MapPost("/{id:guid}/hearings/{hearingId:guid}/hold",
                async (Guid id, Guid hearingId, HearingOutcomeRequest request, ICommandHandler<CloseHearingCommand, HearingResponse> handler, CancellationToken cancellationToken) =>
                    (await handler.Handle(new CloseHearingCommand(id, hearingId, Held: true, request.Text), cancellationToken)).ToHttpResult()),
            "HoldCaseHearing", "Registra a audiência como realizada, com o resultado ou a ata");

        HearingRoute(cases.MapPost("/{id:guid}/hearings/{hearingId:guid}/cancel",
                async (Guid id, Guid hearingId, HearingOutcomeRequest request, ICommandHandler<CloseHearingCommand, HearingResponse> handler, CancellationToken cancellationToken) =>
                    (await handler.Handle(new CloseHearingCommand(id, hearingId, Held: false, request.Text), cancellationToken)).ToHttpResult()),
            "CancelCaseHearing", "Cancela a audiência, com o motivo");
    }

    private static void Action(RouteGroupBuilder cases, string action, string name, string summary, string permission, Delegate handler) =>
        cases.MapPost($"/{{id:guid}}/settlements/{{settlementId:guid}}/{action}", handler)
            .RequirePermission(permission)
            .WithName(name)
            .WithSummary(summary)
            .Produces<SettlementResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static void HearingRoute(RouteHandlerBuilder route, string name, string summary) =>
        route.RequirePermission(LitigationPermissions.CasesEdit)
            .WithName(name)
            .WithSummary(summary)
            .Produces<HearingResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
