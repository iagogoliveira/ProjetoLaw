using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Domain;

/// <summary>Erros do módulo Litigation com código estável (<c>litigation.*</c>) traduzido no frontend.</summary>
public static class LitigationErrors
{
    public static readonly Error CaseNotFound = Error.NotFound("litigation.case_not_found", "Processo ou caso não encontrado.");

    public static readonly Error CnjRequired =
        Error.Validation("litigation.cnj_required", "Processo judicial precisa do número CNJ.");

    public static readonly Error CnjInvalid = Error.Validation("litigation.cnj_invalid", "Número CNJ inválido (confira o dígito verificador).");

    public static readonly Error CnjNotAllowed =
        Error.Validation("litigation.cnj_not_allowed", "Caso administrativo ou extrajudicial não tem número CNJ.");

    public static readonly Error CnjTaken =
        Error.Conflict("litigation.cnj_taken", "Já existe um processo com este número CNJ no escritório.");

    public static readonly Error ClientRequired =
        Error.Validation("litigation.client_required", "Indique ao menos uma parte que é cliente do escritório.");

    public static readonly Error PartyDuplicated =
        Error.Validation("litigation.party_duplicated", "A mesma pessoa aparece mais de uma vez nas partes.");

    public static readonly Error PersonNotFound =
        Error.Validation("litigation.person_not_found", "Pessoa inexistente no cadastro do escritório.");

    public static readonly Error PersonAnonymized =
        Error.Validation("litigation.person_anonymized", "Pessoa anonimizada (LGPD) não pode ser parte de um novo processo.");

    public static readonly Error PayerNotClient =
        Error.Validation("litigation.payer_not_client", "O cliente pagador precisa ser uma das partes clientes.");

    public static readonly Error AssignmentInvalid =
        Error.Validation("litigation.assignment_invalid", "Responsável inválido: sócio e advogado são usuários; equipe é uma equipe do escritório.");

    public static readonly Error ResponsibleNotMember =
        Error.Validation("litigation.responsible_invalid", "O responsável precisa ser membro do escritório.");

    public static readonly Error PracticeAreaNotFound = Error.Validation("litigation.practice_area_invalid", "Área inexistente.");

    public static readonly Error BranchNotFound = Error.Validation("litigation.branch_invalid", "Filial inexistente.");

    public static readonly Error TeamNotFound = Error.Validation("litigation.team_invalid", "Equipe inexistente.");

    public static readonly Error CourtNotFound = Error.Validation("litigation.court_invalid", "Tribunal inexistente.");

    public static readonly Error PhaseNotFound = Error.Validation("litigation.phase_invalid", "Fase inexistente ou desativada.");

    public static readonly Error ActionTypeNotFound =
        Error.Validation("litigation.action_type_invalid", "Tipo de ação inexistente ou desativado.");

    public static readonly Error ClosingReasonNotFound =
        Error.Validation("litigation.closing_reason_invalid", "Motivo de encerramento inexistente ou desativado.");

    public static readonly Error ValueInvalid = Error.Validation("litigation.value_invalid", "O valor não pode ser negativo.");

    public static readonly Error CaseClosed =
        Error.BusinessRule("litigation.case_closed", "O processo está encerrado. Reabra-o antes de alterar.");

    public static readonly Error CaseNotClosed = Error.BusinessRule("litigation.case_not_closed", "O processo não está encerrado.");

    public static readonly Error RelationToItself =
        Error.Validation("litigation.relation_to_itself", "Um processo não se relaciona com ele mesmo.");

    public static readonly Error RelationExists =
        Error.Conflict("litigation.relation_exists", "Os dois processos já estão relacionados.");

    public static readonly Error RelationCycle =
        Error.BusinessRule("litigation.relation_cycle", "A relação formaria um ciclo na hierarquia dos processos.");

    public static readonly Error RelationNotFound = Error.NotFound("litigation.relation_not_found", "Relação não encontrada.");

    public static readonly Error NumberingFormatInvalid =
        Error.Validation("litigation.numbering_format_invalid", "O formato precisa ter {SEQ} (ex.: {AREA}-{ANO}-{SEQ:00000}) e só os marcadores {AREA}, {ANO} e {SEQ}.");

    public static readonly Error AreaCodeInvalid =
        Error.Validation("litigation.area_code_invalid", "A sigla da área tem de 1 a 10 letras ou números.");
}
