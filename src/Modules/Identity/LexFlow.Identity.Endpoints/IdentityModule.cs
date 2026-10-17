using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Accounts;
using LexFlow.Web.Common.Modules;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.WebEncoders;

namespace LexFlow.Identity.Endpoints;

public sealed record RegisterRequest(string Email, string FullName, string Password);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

/// <summary>
/// Módulo Identity na API: servidor OIDC (<c>/connect/*</c>), páginas de conta (<c>/conta/*</c>) e endpoints de conta
/// (Etapa 2, tarefa 4). Os endpoints de conta são públicos, porque quem os usa ainda não tem login. Cadastro e
/// "esqueci a senha" respondem 202 em qualquer caso válido, sem revelar se o e-mail tem conta.
/// </summary>
public sealed class IdentityModule : IModule
{
    /// <summary>Limite por IP do login e dos endpoints públicos de conta (seção 7.3: 10 req/min).</summary>
    public const string PublicRateLimitPolicy = PublicRateLimit.PolicyName;

    public const string PermitsPerMinuteKey = "Identity:PublicPermitsPerMinute";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var permits = configuration.GetValue<int?>(PermitsPerMinuteKey) ?? 10;

        services.AddRazorPages();

        // Páginas em pt-BR: acentos saem como texto, não como entidades HTML (o escape de marcação continua).
        services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PublicRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        OidcEndpoints.Map(endpoints);
        ProfileEndpoints.Map(endpoints);
        endpoints.MapRazorPages().RequireRateLimiting(PublicRateLimitPolicy);

        var auth = endpoints.MapGroup("/api/v1/auth").WithTags("Auth").RequireRateLimiting(PublicRateLimitPolicy);

        auth.MapPost("/register", async (RegisterRequest request, ICommandHandler<RegisterUserCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new RegisterUserCommand(request.Email, request.FullName, request.Password), cancellationToken))
                    .ToHttpResult(_ => TypedResults.Accepted((string?)null)))
            .AllowPublicAccess("Cadastro aberto, antes de existir login")
            .WithName("Register")
            .WithSummary("Cadastra uma conta e envia o e-mail de confirmação")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/confirm-email", async (ConfirmEmailRequest request, ICommandHandler<ConfirmEmailCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ConfirmEmailCommand(request.UserId, request.Token), cancellationToken))
                    .ToHttpResult(_ => TypedResults.NoContent()))
            .AllowPublicAccess("Link do e-mail de confirmação, antes do primeiro login")
            .WithName("ConfirmEmail")
            .WithSummary("Confirma o e-mail com o token recebido")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest request, ICommandHandler<ForgotPasswordCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ForgotPasswordCommand(request.Email), cancellationToken))
                    .ToHttpResult(_ => TypedResults.Accepted((string?)null)))
            .AllowPublicAccess("Recuperação de senha de quem não consegue entrar")
            .WithName("ForgotPassword")
            .WithSummary("Envia o link de redefinição de senha, se o e-mail tiver conta")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, ICommandHandler<ResetPasswordCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ResetPasswordCommand(request.UserId, request.Token, request.NewPassword), cancellationToken))
                    .ToHttpResult(_ => TypedResults.NoContent()))
            .AllowPublicAccess("Link do e-mail de redefinição, de quem não consegue entrar")
            .WithName("ResetPassword")
            .WithSummary("Redefine a senha com o token recebido")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
