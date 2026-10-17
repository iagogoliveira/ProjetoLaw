using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Organization.Contracts;

/// <summary>
/// Pede ao Worker o e-mail de um convite (novo ou reenviado). Não leva token: o Worker gera o token na hora do envio e
/// grava só o hash, para que nenhum segredo passe pelo outbox ou pela fila (mesmo padrão dos e-mails de conta).
/// </summary>
public sealed record InvitationEmailRequestedIntegrationEvent(Guid InvitationId) : IntegrationEvent;
