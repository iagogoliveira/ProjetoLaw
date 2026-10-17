using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Organization.Contracts;

/// <summary>
/// Um escritório acabou de ser criado (onboarding). Os módulos preparam os dados iniciais dele, como as tabelas
/// auxiliares do módulo Platform. Leva a organização nova em <see cref="IntegrationEvent.OrganizationId"/>.
/// </summary>
public sealed record OrganizationCreatedIntegrationEvent(string Name) : IntegrationEvent;
