using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Movements;

public sealed record MovementIntegrationInput(MovementProviderKind Provider, string? ApiKey = null, bool ClearApiKey = false)
{
    public override string ToString() => Provider.ToString();
}
public sealed record MovementIntegrationResponse(MovementProviderKind Provider, bool HasApiKey)
{
    public static MovementIntegrationResponse Of(MovementIntegration? value) => new(value?.Provider ?? MovementProviderKind.Disabled, value?.ProtectedApiKey is not null);
}
public sealed record GetMovementIntegrationQuery : IQuery<MovementIntegrationResponse>;
public sealed record ConfigureMovementIntegrationCommand(MovementIntegrationInput Input) : ICommand<MovementIntegrationResponse>;

internal sealed class MovementIntegrationAuthorizers(ICurrentUser user, IOrganizationAccessDirectory directory) :
    IResourceAuthorizer<GetMovementIntegrationQuery>, IResourceAuthorizer<ConfigureMovementIntegrationCommand>
{
    public Task<Result> AuthorizeAsync(GetMovementIntegrationQuery request, CancellationToken cancellationToken) => AllowedAsync(cancellationToken);
    public Task<Result> AuthorizeAsync(ConfigureMovementIntegrationCommand request, CancellationToken cancellationToken) => AllowedAsync(cancellationToken);
    private async Task<Result> AllowedAsync(CancellationToken cancellationToken) =>
        user.UserId is { } id && await directory.GetAsync(id, cancellationToken) is { } access
        && access.Permissions.TryGetValue("settings.integrations.manage", out var scopes) && scopes.Any(s => s.Scope == OfficeAccessScope.Organization)
            ? Result.Success : Error.Forbidden("publications.integration_access_denied", "A configuração exige permissão no escritório.");
}
internal sealed class GetMovementIntegrationHandler(IPublicationsDbContext db) : IQueryHandler<GetMovementIntegrationQuery, MovementIntegrationResponse>
{
    public async Task<Result<MovementIntegrationResponse>> Handle(GetMovementIntegrationQuery query, CancellationToken cancellationToken) =>
        MovementIntegrationResponse.Of(await db.MovementIntegrations.AsNoTracking().SingleOrDefaultAsync(cancellationToken));
}
internal sealed class ConfigureMovementIntegrationHandler(IPublicationsDbContext db, IMovementSecrets secrets, ICaptureLocks locks)
    : ICommandHandler<ConfigureMovementIntegrationCommand, MovementIntegrationResponse>
{
    public async Task<Result<MovementIntegrationResponse>> Handle(ConfigureMovementIntegrationCommand command, CancellationToken cancellationToken)
    {
        var input = command.Input;
        if (input is null || (input.ClearApiKey && input.ApiKey is not null) || input.ApiKey is { } key &&
            (string.IsNullOrWhiteSpace(key) || key.Length > 1024 || key.Any(char.IsWhiteSpace)))
        { return Error.Validation("publications.integration_invalid", "Configuração de integração inválida."); }
        await locks.IntegrationAsync(cancellationToken);
        var value = await db.MovementIntegrations.SingleOrDefaultAsync(cancellationToken);
        var created = value is null;
        value ??= MovementIntegration.Create();
        var secret = input.ClearApiKey ? null : input.ApiKey is not null ? secrets.Protect(input.ApiKey) : value.ProtectedApiKey;
        var result = value.Configure(input.Provider, secret);
        if (result.IsFailure) { return result.Error; }
        if (created) { db.MovementIntegrations.Add(value); }
        await db.SaveChangesAsync(cancellationToken);
        return MovementIntegrationResponse.Of(value);
    }
}
