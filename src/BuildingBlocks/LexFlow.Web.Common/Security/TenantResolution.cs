using LexFlow.Application.Abstractions.Security;
using LexFlow.Web.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Web.Common.Security;

/// <summary>
/// Resolve a organização ativa da requisição autenticada (seção 6.2, item 2) e a valida contra a pertença do usuário:
/// cabeçalho <c>X-Organization-Id</c>, ou a claim <c>org_id</c>, ou (sem pedido) a última organização escolhida ou a
/// única. Pedir uma organização da qual o usuário não é membro responde 403 <c>organization.not_a_member</c>. Sem
/// <see cref="IMembershipLookup"/> registrado, não há tenant (nunca se confia só no que o cliente manda).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Organization-Id";
    internal const string ItemKey = "lexflow.organization_id";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var userId = context.User.Identity?.IsAuthenticated == true ? HttpContextCurrentUser.ParseGuid(context.User, LexFlowClaims.Subject) : null;
        var lookup = context.RequestServices.GetService<IMembershipLookup>();
        if (userId is null || lookup is null)
        {
            await next(context);
            return;
        }

        var requested = context.Request.Headers[HeaderName].ToString() is { Length: > 0 } header
            ? header
            : context.User.FindFirst(LexFlowClaims.OrganizationId)?.Value;
        var memberships = await lookup.GetAsync(userId.Value, context.RequestAborted);

        if (requested is not null)
        {
            if (!Guid.TryParse(requested, out var organizationId) || !memberships.OrganizationIds.Contains(organizationId))
            {
                await Forbidden(context);
                return;
            }

            context.Items[ItemKey] = organizationId;
        }
        else if (memberships.DefaultOrganization is { } fallback)
        {
            context.Items[ItemKey] = fallback;
        }

        await next(context);
    }

    private static Task Forbidden(HttpContext context) =>
        SecurityProblems.NotAMember.ToProblem().ExecuteAsync(context);
}

/// <summary>Erros de autorização com código estável (traduzidos no SPA).</summary>
public static class SecurityProblems
{
    public static readonly SharedKernel.Results.Error NotAMember =
        SharedKernel.Results.Error.Forbidden("organization.not_a_member", "Você não é membro desta organização.");

    public static readonly SharedKernel.Results.Error MfaRequired =
        SharedKernel.Results.Error.Forbidden("organization.mfa_required", "Esta organização exige verificação em duas etapas. Ative-a em Minha conta e entre novamente.");
}
