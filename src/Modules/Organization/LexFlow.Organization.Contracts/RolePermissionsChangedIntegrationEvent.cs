using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Organization.Contracts;

/// <summary>
/// As permissões de alguém na organização mudaram (perfil editado, atribuição trocada, política de MFA). Quem guarda
/// permissões em cache invalida as da organização do evento (seção 7.1: cache de 5 min com invalidação por evento).
/// </summary>
public sealed record RolePermissionsChangedIntegrationEvent(Guid? RoleId) : IntegrationEvent;
