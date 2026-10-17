using LexFlow.SharedKernel.Results;

namespace LexFlow.Organization.Domain;

/// <summary>Erros do módulo Organization com código estável (<c>organization.*</c>) traduzido no frontend.</summary>
public static class OrganizationErrors
{
    public static readonly Error UnknownPermission =
        Error.Validation("organization.unknown_permission", "Permissão inexistente no catálogo.");

    public static readonly Error ScopeReferenceRequired =
        Error.Validation("organization.scope_reference_required", "Escolha a equipe, a área ou a filial do escopo.");

    public static readonly Error ScopeReferenceNotAllowed =
        Error.Validation("organization.scope_reference_not_allowed", "Os escopos \"próprio\" e \"organização\" não levam referência.");

    public static readonly Error InvalidPartnerShare =
        Error.Validation("organization.invalid_partner_share", "A participação do sócio deve ser maior que 0 e no máximo 100%.");
}
