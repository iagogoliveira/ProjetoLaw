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

    public static readonly Error InvalidEmail =
        Error.Validation("organization.email_invalid", "E-mail inválido.");

    public static readonly Error InvitationRolesRequired =
        Error.Validation("organization.invitation_roles_required", "Escolha ao menos um perfil para o convidado.");

    public static readonly Error InvitationNotFound =
        Error.NotFound("organization.invitation_not_found", "Convite não encontrado, já usado ou cancelado.");

    public static readonly Error InvitationExpired =
        Error.BusinessRule("organization.invitation_expired", "O convite expirou. Peça um novo convite a quem o enviou.");

    public static readonly Error InvitationNotPending =
        Error.BusinessRule("organization.invitation_not_pending", "O convite já foi aceito ou cancelado.");

    public static readonly Error InvitationPending =
        Error.Conflict("organization.invitation_pending", "Já existe um convite pendente para este e-mail. Reenvie o convite existente.");

    public static readonly Error InvitationEmailMismatch =
        Error.Forbidden("organization.invitation_email_mismatch", "Este convite foi enviado para outro e-mail. Entre com a conta do e-mail convidado.");

    public static readonly Error AlreadyAMember =
        Error.Conflict("organization.already_a_member", "Esta pessoa já é membro do escritório.");

    public static readonly Error NameTaken =
        Error.Conflict("organization.name_taken", "Já existe um cadastro com este nome.");

    public static readonly Error CodeTaken =
        Error.Conflict("organization.code_taken", "Já existe um centro de custo com este código.");

    public static readonly Error StructureNotFound =
        Error.NotFound("organization.structure_not_found", "Cadastro não encontrado.");

    public static readonly Error StructureReferenceNotFound =
        Error.Validation("organization.structure_reference_not_found", "A área ou a filial escolhida não existe.");

    public static readonly Error StructureInUse =
        Error.BusinessRule("organization.structure_in_use", "Este cadastro está em uso por equipes, perfis atribuídos ou convites pendentes.");

    public static readonly Error InvalidCnpj =
        Error.Validation("organization.cnpj_invalid", "CNPJ inválido.");

    public static readonly Error PartnerSharesExceedTotal =
        Error.BusinessRule("organization.partner_shares_exceed_total", "A soma das participações dos sócios passaria de 100%.");

    public static readonly Error RoleInUse =
        Error.BusinessRule("organization.role_in_use", "O perfil está atribuído a membros ou em convites pendentes.");
}
