using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Application.Hearings;
using LexFlow.Litigation.Application.Risk;
using LexFlow.Litigation.Application.Settlements;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Application.Access;

/// <summary>
/// Alcance do processo nos casos de uso de risco, acordos e audiências (PR 5C2), com a permissão de cada um: avaliar
/// risco pede <c>cases.risk.assess</c>; aprovar acordo, <c>cases.settlements.approve</c>; o resto, ler ou editar.
/// </summary>
internal sealed class FollowUpAuthorizers(CaseAccess access) :
    IResourceAuthorizer<GetCaseRiskQuery>,
    IResourceAuthorizer<GetRiskHistoryQuery>,
    IResourceAuthorizer<AssessRiskCommand>,
    IResourceAuthorizer<ListSettlementsQuery>,
    IResourceAuthorizer<StartSettlementCommand>,
    IResourceAuthorizer<AddSettlementOfferCommand>,
    IResourceAuthorizer<SubmitSettlementCommand>,
    IResourceAuthorizer<ApproveSettlementCommand>,
    IResourceAuthorizer<ReturnSettlementCommand>,
    IResourceAuthorizer<HomologateSettlementCommand>,
    IResourceAuthorizer<EndSettlementCommand>,
    IResourceAuthorizer<PayInstallmentCommand>,
    IResourceAuthorizer<ListHearingsQuery>,
    IResourceAuthorizer<ScheduleHearingCommand>,
    IResourceAuthorizer<UpdateHearingCommand>,
    IResourceAuthorizer<RescheduleHearingCommand>,
    IResourceAuthorizer<CloseHearingCommand>
{
    public Task<Result> AuthorizeAsync(GetCaseRiskQuery request, CancellationToken cancellationToken) => Read(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(GetRiskHistoryQuery request, CancellationToken cancellationToken) => Read(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(AssessRiskCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.CaseId, CasePermissions.AssessRisk, cancellationToken);

    public Task<Result> AuthorizeAsync(ListSettlementsQuery request, CancellationToken cancellationToken) => Read(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(StartSettlementCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(AddSettlementOfferCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(SubmitSettlementCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(ApproveSettlementCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.CaseId, CasePermissions.ApproveSettlements, cancellationToken);

    public Task<Result> AuthorizeAsync(ReturnSettlementCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(HomologateSettlementCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(EndSettlementCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(PayInstallmentCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(ListHearingsQuery request, CancellationToken cancellationToken) => Read(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(ScheduleHearingCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(UpdateHearingCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(RescheduleHearingCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    public Task<Result> AuthorizeAsync(CloseHearingCommand request, CancellationToken cancellationToken) => Edit(request.CaseId, cancellationToken);

    private Task<Result> Read(Guid caseId, CancellationToken cancellationToken) => access.AuthorizeAsync(caseId, CasePermissions.Read, cancellationToken);

    private Task<Result> Edit(Guid caseId, CancellationToken cancellationToken) => access.AuthorizeAsync(caseId, CasePermissions.Edit, cancellationToken);
}
