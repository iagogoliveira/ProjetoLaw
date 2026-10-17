using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Identity.Contracts;

/// <summary>E-mails transacionais de conta (Etapa 2, tarefa 5).</summary>
public enum AccountEmailKind
{
    /// <summary>Confirmação do e-mail informado no cadastro.</summary>
    ConfirmEmail,

    /// <summary>Link de redefinição de senha.</summary>
    ResetPassword,

    /// <summary>Aviso de que alguém tentou cadastrar um e-mail que já tem conta (a resposta do cadastro não revela isso).</summary>
    AlreadyRegistered,

    /// <summary>Código de acesso do segundo fator por e-mail, a cada login de quem escolheu esse método.</summary>
    SignInCode,
}

/// <summary>
/// Pede ao Worker o envio de um e-mail de conta. Não leva token nem link: o Worker gera o token na hora do envio,
/// para que nenhum segredo passe pelo outbox ou pela fila. É evento da plataforma, porque usuários são globais.
/// </summary>
public sealed record AccountEmailRequestedIntegrationEvent(Guid UserId, AccountEmailKind Kind) : PlatformIntegrationEvent;
