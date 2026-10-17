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

    public static readonly Error NotAMember =
        Error.Forbidden("organization.not_a_member", "Você não é membro desta organização.");

    public static readonly Error RoleNotFound =
        Error.NotFound("organization.role_not_found", "Perfil não encontrado.");

    public static readonly Error MemberNotFound =
        Error.NotFound("organization.member_not_found", "Membro não encontrado.");

    public static readonly Error ScopeReferenceNotFound =
        Error.Validation("organization.scope_reference_not_found", "A equipe, a área ou a filial do escopo não existe.");

    public static readonly Error AdminRoleLocked =
        Error.BusinessRule("organization.admin_role_locked", "O perfil Administrador do escritório tem sempre todas as permissões.");

    public static readonly Error LastAdmin =
        Error.BusinessRule("organization.last_admin", "O escritório precisa de pelo menos um Administrador do escritório.");

    public static readonly Error InvalidPartnerShare =
        Error.Validation("organization.invalid_partner_share", "A participação do sócio deve ser maior que 0 e no máximo 100%.");
}
