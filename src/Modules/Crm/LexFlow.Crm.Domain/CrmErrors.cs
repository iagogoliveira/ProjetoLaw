using LexFlow.SharedKernel.Results;

namespace LexFlow.Crm.Domain;

/// <summary>Erros do módulo Crm com código estável (<c>crm.*</c>) traduzido no frontend.</summary>
public static class CrmErrors
{
    public static readonly Error InvalidCpf = Error.Validation("crm.cpf_invalid", "CPF inválido.");

    public static readonly Error InvalidCnpj = Error.Validation("crm.cnpj_invalid", "CNPJ inválido.");

    public static readonly Error InvalidState = Error.Validation("crm.state_invalid", "UF inválida.");

    public static readonly Error InvalidPostalCode = Error.Validation("crm.postal_code_invalid", "CEP inválido.");

    public static readonly Error RoleRequired =
        Error.Validation("crm.role_required", "Escolha ao menos um papel (cliente, parte contrária…).");

    public static readonly Error DocumentTaken =
        Error.Conflict("crm.document_taken", "Já existe uma pessoa com este CPF/CNPJ no escritório.");

    public static readonly Error PersonNotFound = Error.NotFound("crm.person_not_found", "Pessoa não encontrada.");

    public static readonly Error EconomicGroupNotFound =
        Error.NotFound("crm.economic_group_not_found", "Grupo econômico não encontrado.");

    public static readonly Error EconomicGroupInUse =
        Error.BusinessRule("crm.economic_group_in_use", "O grupo econômico tem pessoas vinculadas.");

    public static readonly Error EconomicGroupNameTaken =
        Error.Conflict("crm.economic_group_name_taken", "Já existe um grupo econômico com este nome.");

    public static readonly Error CnpjNotFound =
        Error.NotFound("crm.cnpj_not_found", "CNPJ não encontrado na Receita Federal.");

    public static readonly Error CnpjLookupUnavailable =
        Error.BusinessRule("crm.cnpj_lookup_unavailable", "A consulta de CNPJ está indisponível agora. Preencha os dados à mão.");

    public static readonly Error PersonAnonymized =
        Error.BusinessRule("crm.person_anonymized", "Este cadastro foi anonimizado (LGPD) e não pode mais ser alterado.");

    public static readonly Error MergeSamePerson =
        Error.Validation("crm.merge_same_person", "Escolha dois cadastros diferentes para unificar.");

    public static readonly Error MergeKindMismatch =
        Error.BusinessRule("crm.merge_kind_mismatch", "Só é possível unificar cadastros do mesmo tipo (física com física, jurídica com jurídica).");

    public static readonly Error MergeDocumentConflict =
        Error.BusinessRule("crm.merge_document_conflict", "Os cadastros têm documentos diferentes e não são a mesma pessoa.");

    public static readonly Error ConflictCheckNotFound =
        Error.NotFound("crm.conflict_check_not_found", "Verificação de conflito não encontrada.");

    public static readonly Error ConflictJustificationRequired =
        Error.Validation("crm.conflict_justification_required", "Há conflito apontado: escreva a justificativa para prosseguir.");

    public static readonly Error ConflictAlreadyDecided =
        Error.BusinessRule("crm.conflict_already_decided", "Esta verificação já foi concluída.");

    public static readonly Error OpportunityNotFound = Error.NotFound("crm.opportunity_not_found", "Oportunidade não encontrada.");

    public static readonly Error OpportunityWon =
        Error.BusinessRule("crm.opportunity_won", "A oportunidade já foi ganha e não pode mais ser alterada.");

    public static readonly Error OpportunityLost =
        Error.BusinessRule("crm.opportunity_lost", "A oportunidade está perdida. Reabra-a antes de continuar.");

    public static readonly Error OpportunityStageInvalid =
        Error.Validation("crm.opportunity_stage_invalid", "Escolha lead, reunião ou proposta. Ganhar é pela aceitação da proposta; perder, pela ação própria.");

    public static readonly Error OpportunityValueInvalid =
        Error.Validation("crm.opportunity_value_invalid", "O valor estimado não pode ser negativo.");

    public static readonly Error OriginNotFound =
        Error.Validation("crm.origin_invalid", "Origem inexistente ou desativada.");

    public static readonly Error LossReasonNotFound =
        Error.Validation("crm.loss_reason_invalid", "Motivo de perda inexistente ou desativado.");

    public static readonly Error ResponsibleNotMember =
        Error.Validation("crm.responsible_invalid", "O responsável precisa ser membro do escritório.");

    public static readonly Error ConflictCheckRequired =
        Error.BusinessRule("crm.conflict_check_required", "Faça a verificação de conflito do interessado e prossiga com ela antes de aceitar a proposta.");

    public static readonly Error ProposalNotFound = Error.NotFound("crm.proposal_not_found", "Proposta não encontrada.");

    public static readonly Error ProposalNotDraft =
        Error.BusinessRule("crm.proposal_not_draft", "Só o rascunho da proposta pode ser alterado ou enviado para aprovação.");

    public static readonly Error ProposalItemsRequired =
        Error.Validation("crm.proposal_items_required", "Inclua ao menos um item na proposta.");

    public static readonly Error ProposalTooManyItems =
        Error.Validation("crm.proposal_too_many_items", "A proposta aceita até 50 itens.");

    public static readonly Error ProposalAmountInvalid =
        Error.Validation("crm.proposal_amount_invalid", "Informe a quantidade (maior que zero) e o valor (zero ou mais) do item.");

    public static readonly Error ProposalPercentageInvalid =
        Error.Validation("crm.proposal_percentage_invalid", "No êxito, informe o percentual (maior que 0 e até 100).");

    public static readonly Error ProposalNotPendingApproval =
        Error.BusinessRule("crm.proposal_not_pending_approval", "A proposta não está aguardando aprovação.");

    public static readonly Error ProposalSelfApproval =
        Error.BusinessRule("crm.proposal_self_approval", "Quem elaborou a proposta não pode aprová-la.");

    public static readonly Error ProposalCannotReturn =
        Error.BusinessRule("crm.proposal_cannot_return", "Só a proposta aguardando aprovação ou aprovada volta para rascunho.");

    public static readonly Error ProposalNotApproved =
        Error.BusinessRule("crm.proposal_not_approved", "A proposta precisa ser aprovada antes do envio.");

    public static readonly Error ProposalNotSent =
        Error.BusinessRule("crm.proposal_not_sent", "A proposta ainda não foi enviada ao cliente.");

    public static readonly Error ProposalExpired =
        Error.BusinessRule("crm.proposal_expired", "A validade da proposta já passou.");

    public static readonly Error ProposalCannotDelete =
        Error.BusinessRule("crm.proposal_cannot_delete", "A proposta já enviada fica como histórico e não pode ser excluída.");

    public static readonly Error TemplateNotFound = Error.NotFound("crm.template_not_found", "Modelo de proposta não encontrado.");

    public static readonly Error TemplateInvalid =
        Error.Validation("crm.template_invalid", "O arquivo não é um documento do Word (.docx) válido.");

    public static readonly Error TemplateTooLarge =
        Error.Validation("crm.template_too_large", "O modelo pode ter até 5 MB.");

    public static readonly Error TemplateInfected =
        Error.Validation("crm.template_infected", "O antivírus bloqueou o arquivo.");

    public static Error TemplateUnknownMarkers(IEnumerable<string> markers) =>
        Error.Validation("crm.template_unknown_marker", $"Marcadores desconhecidos no modelo: {string.Join(", ", markers)}.");
}
