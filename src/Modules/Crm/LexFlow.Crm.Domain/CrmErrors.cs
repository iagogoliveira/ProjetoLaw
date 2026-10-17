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
}
