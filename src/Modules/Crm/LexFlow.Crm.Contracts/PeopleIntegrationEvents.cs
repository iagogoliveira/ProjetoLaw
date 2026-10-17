using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Crm.Contracts;

/// <summary>
/// Dois cadastros de pessoa foram unificados: quem guarda o id da pessoa (processos, documentos, financeiro) passa a
/// usar <see cref="KeptPersonId"/> no lugar de <see cref="MergedPersonId"/>.
/// </summary>
public sealed record PeopleMergedIntegrationEvent(Guid KeptPersonId, Guid MergedPersonId) : IntegrationEvent;

/// <summary>Uma pessoa foi anonimizada (LGPD): quem guarda cópia de dados pessoais dela também os apaga.</summary>
public sealed record PersonAnonymizedIntegrationEvent(Guid PersonId) : IntegrationEvent;
