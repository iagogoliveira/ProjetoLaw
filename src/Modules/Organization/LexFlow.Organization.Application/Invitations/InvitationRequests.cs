using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Contracts;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Application.Access;
using LexFlow.Organization.Contracts;
using LexFlow.Organization.Domain;
using LexFlow.Organization.Domain.Invitations;
using LexFlow.Organization.Domain.Members;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using EmailAddress = LexFlow.SharedKernel.ValueObjects.Email;

namespace LexFlow.Organization.Application.Invitations;

/// <summary>Token dos convites: 32 bytes aleatórios em Base64Url; no banco fica só o SHA-256 (hexadecimal).</summary>
public static class InvitationTokens
{
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));
}

/// <summary>Situação exibida: a gravada, ou "expirado" para um pendente com o prazo vencido.</summary>
public enum InvitationState
{
    Pending,
    Accepted,
    Revoked,
    Expired,
}

public sealed record InvitationResponse(
    Guid Id,
    string Email,
    InvitationState Status,
    DateTimeOffset ExpiresAt,
    Guid InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt,
    IReadOnlyList<AssignmentResponse> Roles);

internal static class InvitationMapping
{
    public static InvitationResponse ToResponse(this Invitation invitation, DateTimeOffset now) => new(
        invitation.Id,
        invitation.Email,
        invitation.IsExpired(now) ? InvitationState.Expired : (InvitationState)invitation.Status,
        invitation.ExpiresAt,
        invitation.InvitedBy,
        invitation.CreatedAt,
        invitation.AcceptedAt,
        [.. invitation.Roles.Select(r => new AssignmentResponse(r.RoleId, r.Scope, r.ScopeRefId))]);
}

/// <summary>
/// Convida alguém por e-mail com os perfis que receberá (decisão de 2026-10-17: escolhidos no convite). O e-mail sai
/// pelo Worker; o convite vale 7 dias. Quem ainda não tem conta se cadastra pelo link com o mesmo e-mail.
/// </summary>
public sealed record CreateInvitationCommand(string Email, IReadOnlyList<RoleAssignmentInput> Roles) : ICommand<InvitationResponse>;

internal sealed class CreateInvitationValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationValidator()
    {
        RuleFor(c => c.Email).Must(EmailAddress.IsValid).WithErrorCode(OrganizationErrors.InvalidEmail.Code);
        RuleFor(c => c.Roles).NotEmpty().WithErrorCode(OrganizationErrors.InvitationRolesRequired.Code);
    }
}

internal sealed class CreateInvitationHandler(
    IOrganizationDbContext db,
    RoleAssignments rules,
    IUserDirectory users,
    ICurrentUser user,
    IIntegrationEventPublisher events,
    IIdGenerator ids,
    IClock clock)
    : ICommandHandler<CreateInvitationCommand, InvitationResponse>
{
    public async Task<Result<InvitationResponse>> Handle(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Create(command.Email).Value.Value;
        var now = clock.UtcNow;

        if (await users.FindByEmailAsync(email, cancellationToken) is { } account
            && await db.Members.AnyAsync(m => m.UserId == account.Id, cancellationToken))
        {
            return OrganizationErrors.AlreadyAMember;
        }

        if (await db.Invitations.AnyAsync(i => i.Email == email && i.Status == InvitationStatus.Pending && i.ExpiresAt > now, cancellationToken))
        {
            return OrganizationErrors.InvitationPending;
        }

        var roles = await rules.BuildAsync(Guid.Empty, command.Roles, cancellationToken);
        if (roles.IsFailure)
        {
            return roles.Error;
        }

        var invitation = Invitation.Create(ids.NewId(), email, user.UserId!.Value, roles.Value, now);
        if (invitation.IsFailure)
        {
            return invitation.Error;
        }

        db.Invitations.Add(invitation.Value);
        events.Publish(new InvitationEmailRequestedIntegrationEvent(invitation.Value.Id));
        await db.SaveChangesAsync(cancellationToken);
        return invitation.Value.ToResponse(now);
    }
}

public sealed record ListInvitationsQuery : IQuery<IReadOnlyList<InvitationResponse>>;

internal sealed class ListInvitationsHandler(IOrganizationDbContext db, IClock clock) : IQueryHandler<ListInvitationsQuery, IReadOnlyList<InvitationResponse>>
{
    public async Task<Result<IReadOnlyList<InvitationResponse>>> Handle(ListInvitationsQuery query, CancellationToken cancellationToken)
    {
        var invitations = await db.Invitations.AsNoTracking().Include(i => i.Roles).OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        return invitations.Select(i => i.ToResponse(clock.UtcNow)).ToList();
    }
}

/// <summary>Reenvia um convite pendente (inclusive expirado): o prazo recomeça e o link anterior deixa de valer.</summary>
public sealed record ResendInvitationCommand(Guid InvitationId) : ICommand<InvitationResponse>;

internal sealed class ResendInvitationHandler(IOrganizationDbContext db, IIntegrationEventPublisher events, IClock clock)
    : ICommandHandler<ResendInvitationCommand, InvitationResponse>
{
    public async Task<Result<InvitationResponse>> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.Include(i => i.Roles).SingleOrDefaultAsync(i => i.Id == command.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        var renewed = invitation.Renew(clock.UtcNow);
        if (renewed.IsFailure)
        {
            return renewed.Error;
        }

        db.InvitationTokens.RemoveRange(await db.InvitationTokens.Where(t => t.InvitationId == invitation.Id).ToListAsync(cancellationToken));
        events.Publish(new InvitationEmailRequestedIntegrationEvent(invitation.Id));
        await db.SaveChangesAsync(cancellationToken);
        return invitation.ToResponse(clock.UtcNow);
    }
}

/// <summary>Cancela um convite pendente; o link enviado deixa de valer na hora.</summary>
public sealed record RevokeInvitationCommand(Guid InvitationId) : ICommand;

internal sealed class RevokeInvitationHandler(IOrganizationDbContext db, IClock clock) : ICommandHandler<RevokeInvitationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == command.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        var revoked = invitation.Revoke(clock.UtcNow);
        if (revoked.IsFailure)
        {
            return revoked.Error;
        }

        db.InvitationTokens.RemoveRange(await db.InvitationTokens.Where(t => t.InvitationId == invitation.Id).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>O que a página do convite mostra antes do login: escritório, e-mail convidado (para o cadastro) e prazo.</summary>
public sealed record InvitationPreviewResponse(string OrganizationName, string Email, DateTimeOffset ExpiresAt);

public sealed record PreviewInvitationQuery(string Token) : IQuery<InvitationPreviewResponse>;

/// <summary>
/// Lê só o diretório de tokens e o nome do escritório (sem tenant): quem tem o link ainda não é membro. Token
/// desconhecido, usado, cancelado ou expirado responde igual (404).
/// </summary>
internal sealed class PreviewInvitationHandler(IOrganizationDbContext db, IClock clock) : IQueryHandler<PreviewInvitationQuery, InvitationPreviewResponse>
{
    public async Task<Result<InvitationPreviewResponse>> Handle(PreviewInvitationQuery query, CancellationToken cancellationToken)
    {
        var hash = InvitationTokens.Hash(query.Token);
        var now = clock.UtcNow;
        var preview = await db.InvitationTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash && t.ExpiresAt > now)
            .Join(db.Organizations, t => t.OrganizationId, o => o.Id, (t, o) => new InvitationPreviewResponse(o.Name, t.Email, t.ExpiresAt))
            .SingleOrDefaultAsync(cancellationToken);
        return preview is null ? OrganizationErrors.InvitationNotFound : preview;
    }
}

public sealed record InvitationAcceptedResponse(Guid OrganizationId, string OrganizationName);

/// <summary>
/// Aceite pelo usuário autenticado (decisão de 2026-10-17: só a conta com o e-mail convidado, confirmado). Cria o
/// membro com os perfis do convite e o token deixa de valer (uso único).
/// </summary>
public sealed record AcceptInvitationCommand(string Token) : ICommand<InvitationAcceptedResponse>;

internal sealed class AcceptInvitationHandler(
    IOrganizationDbContext db,
    IUserDirectory users,
    ICurrentUser user,
    TenantOverride tenant,
    IAccessCache cache,
    IIdGenerator ids,
    IClock clock)
    : ICommandHandler<AcceptInvitationCommand, InvitationAcceptedResponse>
{
    public async Task<Result<InvitationAcceptedResponse>> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var hash = InvitationTokens.Hash(command.Token);
        var token = await db.InvitationTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        var account = await users.FindAsync(user.UserId!.Value, cancellationToken);
        if (account is not { EmailConfirmed: true } || !string.Equals(account.Email, token.Email, StringComparison.OrdinalIgnoreCase))
        {
            return OrganizationErrors.InvitationEmailMismatch;
        }

        Result<InvitationAcceptedResponse> result;
        using (tenant.BeginForInvitationAcceptance(token.OrganizationId))
        {
            result = await JoinAsync(token, account.Id, cancellationToken);
        }

        if (result.IsSuccess)
        {
            await cache.InvalidateMembershipsAsync(account.Id, cancellationToken);
            await cache.InvalidateOrganizationAsync(token.OrganizationId, cancellationToken);
        }

        return result;
    }

    private async Task<Result<InvitationAcceptedResponse>> JoinAsync(InvitationToken token, Guid userId, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.Include(i => i.Roles).SingleOrDefaultAsync(i => i.Id == token.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        if (await db.Members.AnyAsync(m => m.UserId == userId, cancellationToken))
        {
            return OrganizationErrors.AlreadyAMember;
        }

        var now = clock.UtcNow;
        var member = OrganizationMember.Join(ids.NewId(), token.OrganizationId, userId, now);
        var accepted = invitation.Accept(member.Id, now);
        if (accepted.IsFailure)
        {
            return accepted.Error;
        }

        // Perfis excluídos depois do convite ficam de fora (a exclusão de perfil em convite pendente é barrada).
        var roleIds = await db.Roles.Select(r => r.Id).ToListAsync(cancellationToken);
        db.Members.Add(member);
        foreach (var role in invitation.Roles.Where(r => roleIds.Contains(r.RoleId)))
        {
            db.RoleAssignments.Add(MemberRoleAssignment.Assign(ids.NewId(), member.Id, role.RoleId, role.Scope, role.ScopeRefId).Value);
        }

        if (!await db.UserOrganizations.AnyAsync(u => u.UserId == userId && u.OrganizationId == token.OrganizationId, cancellationToken))
        {
            db.UserOrganizations.Add(new UserOrganization(userId, token.OrganizationId));
        }

        db.InvitationTokens.Remove(token);
        await db.SaveChangesAsync(cancellationToken);

        var name = await db.Organizations.Where(o => o.Id == token.OrganizationId).Select(o => o.Name).SingleAsync(cancellationToken);
        return new InvitationAcceptedResponse(token.OrganizationId, name);
    }
}
