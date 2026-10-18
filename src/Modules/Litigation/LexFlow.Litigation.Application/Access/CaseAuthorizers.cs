using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Application.Cases;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Application.Access;

/// <summary>
/// Segunda etapa da autorização (seção 7.2) nos casos de uso de um processo: o caso precisa estar ao alcance da
/// permissão do endpoint (escopo de dados e listas de sigilo). Roda depois da validação e antes da transação.
/// </summary>
internal sealed class CaseAuthorizers(CaseAccess access) :
    IResourceAuthorizer<GetCaseQuery>,
    IResourceAuthorizer<UpdateCaseCommand>,
    IResourceAuthorizer<DeleteCaseCommand>,
    IResourceAuthorizer<CloseCaseCommand>,
    IResourceAuthorizer<ChangeCaseStatusCommand>,
    IResourceAuthorizer<LinkCaseCommand>,
    IResourceAuthorizer<UnlinkCaseCommand>,
    IResourceAuthorizer<GetCaseRestrictionsQuery>,
    IResourceAuthorizer<SetCaseRestrictionsCommand>
{
    public Task<Result> AuthorizeAsync(GetCaseQuery request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.Id, CasePermissions.Read, cancellationToken);

    public Task<Result> AuthorizeAsync(UpdateCaseCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.Id, CasePermissions.Edit, cancellationToken);

    public Task<Result> AuthorizeAsync(DeleteCaseCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.Id, CasePermissions.Delete, cancellationToken);

    public Task<Result> AuthorizeAsync(CloseCaseCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.Id, CasePermissions.Edit, cancellationToken);

    public Task<Result> AuthorizeAsync(ChangeCaseStatusCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.Id, CasePermissions.Edit, cancellationToken);

    /// <summary>Os dois processos da relação precisam estar ao alcance de quem liga.</summary>
    public async Task<Result> AuthorizeAsync(LinkCaseCommand request, CancellationToken cancellationToken)
    {
        var parent = await access.AuthorizeAsync(request.ParentCaseId, CasePermissions.Edit, cancellationToken);
        return parent.IsFailure ? parent : await access.AuthorizeAsync(request.ChildCaseId, CasePermissions.Edit, cancellationToken);
    }

    public Task<Result> AuthorizeAsync(UnlinkCaseCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.CaseId, CasePermissions.Edit, cancellationToken);

    public Task<Result> AuthorizeAsync(GetCaseRestrictionsQuery request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.CaseId, CasePermissions.ManageRestrictions, cancellationToken);

    public Task<Result> AuthorizeAsync(SetCaseRestrictionsCommand request, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(request.CaseId, CasePermissions.ManageRestrictions, cancellationToken);
}
