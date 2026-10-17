using System.Text.RegularExpressions;

namespace LexFlow.Organization.Contracts.Permissions;

/// <summary>Uma permissão do catálogo.</summary>
/// <param name="Name">Código estável <c>modulo.recurso.acao</c> (ou <c>modulo.acao</c>), usado em <c>RequirePermission</c>.</param>
/// <param name="Group">Grupo da tela de perfis (módulo).</param>
/// <param name="Description">Texto em pt-BR para a tela de perfis.</param>
/// <param name="Universal">
/// Concedida a todo usuário autenticado, sem depender de perfil (a própria conta e criar um escritório). Não entra na
/// matriz de perfis.
/// </param>
public sealed record PermissionDefinition(string Name, string Group, string Description, bool Universal = false);

/// <summary>
/// Catálogo de permissões (seção 7.2): todas as previstas na especificação, inclusive de módulos que ainda não
/// existem, para a tela de perfis ficar completa desde já. Permissões são constantes em código; nunca vão no token.
/// Renomear uma permissão publicada exige migration dos perfis já gravados.
/// </summary>
public static partial class PermissionCatalog
{
    public static class Groups
    {
        public const string Account = "Minha conta";
        public const string Settings = "Configurações";
        public const string Platform = "Plataforma";
        public const string Crm = "Pessoas e CRM";
        public const string Cases = "Processos e casos";
        public const string Publications = "Publicações";
        public const string Schedule = "Prazos, agenda e tarefas";
        public const string Workflow = "Workflow";
        public const string Documents = "Documentos";
        public const string Finance = "Financeiro";
        public const string Ai = "IA jurídica";
        public const string Reports = "Relatórios e BI";
    }

    public const string ProfileRead = "identity.profile.read";
    public const string ProfileManage = "identity.profile.manage";
    public const string OrganizationsCreate = "organizations.create";
    public const string InvitationsAccept = "organizations.invitations.accept";
    public const string SettingsOrganizationManage = "settings.organization.manage";
    public const string SettingsStructureManage = "settings.structure.manage";
    public const string SettingsUsersRead = "settings.users.read";
    public const string SettingsUsersManage = "settings.users.manage";
    public const string SettingsRolesManage = "settings.roles.manage";
    public const string SettingsCustomFieldsRead = "settings.custom_fields.read";
    public const string SettingsCustomFieldsManage = "settings.custom_fields.manage";
    public const string SettingsIntegrationsManage = "settings.integrations.manage";
    public const string SettingsAuditRead = "settings.audit.read";
    public const string PlatformJobsRead = "platform.jobs.read";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(ProfileRead, Groups.Account, "Ver o próprio perfil e as sessões", Universal: true),
        new(ProfileManage, Groups.Account, "Gerenciar a verificação em duas etapas e as sessões", Universal: true),
        new(OrganizationsCreate, Groups.Account, "Criar um novo escritório", Universal: true),
        new(InvitationsAccept, Groups.Account, "Aceitar convites para escritórios", Universal: true),

        new(SettingsOrganizationManage, Groups.Settings, "Editar os dados e as políticas do escritório"),
        new(SettingsStructureManage, Groups.Settings, "Gerenciar filiais, áreas, equipes, centros de custo e sócios"),
        new(SettingsUsersRead, Groups.Settings, "Ver usuários e convites"),
        new(SettingsUsersManage, Groups.Settings, "Convidar, desativar e atribuir perfis a usuários"),
        new(SettingsRolesManage, Groups.Settings, "Criar e editar perfis e permissões"),
        new(SettingsCustomFieldsRead, Groups.Settings, "Ver campos customizados e tabelas auxiliares (necessário para preencher formulários)"),
        new(SettingsCustomFieldsManage, Groups.Settings, "Gerenciar campos customizados e tabelas auxiliares"),
        new(SettingsIntegrationsManage, Groups.Settings, "Gerenciar integrações"),
        new(SettingsAuditRead, Groups.Settings, "Consultar a trilha de auditoria"),

        new(PlatformJobsRead, Groups.Platform, "Acompanhar operações longas (importações, exportações)"),

        new("crm.people.read", Groups.Crm, "Ver pessoas, clientes e contatos"),
        new("crm.people.create", Groups.Crm, "Cadastrar pessoas"),
        new("crm.people.edit", Groups.Crm, "Editar pessoas"),
        new("crm.people.delete", Groups.Crm, "Excluir pessoas"),
        new("crm.people.export", Groups.Crm, "Exportar pessoas"),
        new("crm.people.merge", Groups.Crm, "Unificar cadastros duplicados"),
        new("crm.people.anonymize", Groups.Crm, "Anonimizar pessoas (LGPD, irreversível)"),
        new("crm.conflicts.check", Groups.Crm, "Consultar conflito de interesses"),
        new("crm.opportunities.read", Groups.Crm, "Ver oportunidades"),
        new("crm.opportunities.manage", Groups.Crm, "Gerenciar oportunidades"),
        new("crm.proposals.read", Groups.Crm, "Ver propostas"),
        new("crm.proposals.manage", Groups.Crm, "Elaborar propostas"),
        new("crm.proposals.approve", Groups.Crm, "Aprovar propostas"),

        new("cases.read", Groups.Cases, "Ver processos e casos"),
        new("cases.create", Groups.Cases, "Cadastrar processos e casos"),
        new("cases.edit", Groups.Cases, "Editar processos e casos"),
        new("cases.delete", Groups.Cases, "Excluir processos e casos"),
        new("cases.export", Groups.Cases, "Exportar processos e casos"),
        new("cases.restrictions.manage", Groups.Cases, "Restringir o acesso a casos sigilosos"),
        new("cases.settlements.approve", Groups.Cases, "Aprovar acordos"),

        new("publications.read", Groups.Publications, "Ver publicações e intimações"),
        new("publications.triage", Groups.Publications, "Triar publicações"),
        new("publications.distribute", Groups.Publications, "Distribuir publicações e prazos"),

        new("schedule.deadlines.read", Groups.Schedule, "Ver prazos"),
        new("schedule.deadlines.manage", Groups.Schedule, "Cadastrar e cumprir prazos"),
        new("schedule.deadlines.verify", Groups.Schedule, "Conferir o cumprimento de prazos"),
        new("schedule.events.read", Groups.Schedule, "Ver a agenda"),
        new("schedule.events.manage", Groups.Schedule, "Gerenciar compromissos e audiências"),
        new("schedule.tasks.read", Groups.Schedule, "Ver tarefas"),
        new("schedule.tasks.manage", Groups.Schedule, "Criar e atribuir tarefas"),

        new("workflow.read", Groups.Workflow, "Ver fluxos e automações"),
        new("workflow.manage", Groups.Workflow, "Criar e editar fluxos e automações"),
        new("workflow.approvals.approve", Groups.Workflow, "Decidir aprovações por alçada"),

        new("documents.read", Groups.Documents, "Ver documentos"),
        new("documents.upload", Groups.Documents, "Enviar documentos"),
        new("documents.edit", Groups.Documents, "Editar documentos e metadados"),
        new("documents.delete", Groups.Documents, "Excluir documentos"),
        new("documents.export", Groups.Documents, "Baixar documentos em lote"),
        new("documents.templates.manage", Groups.Documents, "Gerenciar modelos de documentos"),
        new("documents.sign", Groups.Documents, "Enviar documentos para assinatura"),

        new("finance.fee_agreements.read", Groups.Finance, "Ver contratos de honorários"),
        new("finance.fee_agreements.manage", Groups.Finance, "Gerenciar contratos de honorários"),
        new("finance.timesheet.log", Groups.Finance, "Lançar as próprias horas"),
        new("finance.timesheet.read", Groups.Finance, "Ver o timesheet"),
        new("finance.timesheet.manage", Groups.Finance, "Revisar e ajustar horas de terceiros"),
        new("finance.expenses.read", Groups.Finance, "Ver despesas e custas"),
        new("finance.expenses.manage", Groups.Finance, "Lançar despesas e custas"),
        new("finance.expenses.approve", Groups.Finance, "Aprovar despesas"),
        new("finance.invoices.read", Groups.Finance, "Ver faturas"),
        new("finance.invoices.manage", Groups.Finance, "Preparar pré-faturas e faturas"),
        new("finance.invoices.approve", Groups.Finance, "Aprovar faturamento"),
        new("finance.payables.manage", Groups.Finance, "Gerenciar contas a pagar"),
        new("finance.receivables.manage", Groups.Finance, "Gerenciar contas a receber e conciliação"),
        new("finance.reports.read", Groups.Finance, "Ver relatórios financeiros e rentabilidade"),

        new("ai.research.use", Groups.Ai, "Usar a pesquisa jurídica"),
        new("ai.drafts.generate", Groups.Ai, "Gerar minutas de peças"),
        new("ai.settings.manage", Groups.Ai, "Configurar a IA e acompanhar o consumo"),

        new("reports.read", Groups.Reports, "Ver painéis e relatórios"),
        new("reports.export", Groups.Reports, "Exportar relatórios"),
        new("reports.manage", Groups.Reports, "Criar relatórios customizados"),
    ];

    private static readonly Dictionary<string, PermissionDefinition> ByName = All.ToDictionary(p => p.Name, StringComparer.Ordinal);

    /// <summary>Permissões que aparecem na matriz de perfis (todas menos as universais).</summary>
    public static IReadOnlyList<PermissionDefinition> Assignable { get; } = [.. All.Where(p => !p.Universal)];

    public static bool Exists(string permission) => ByName.ContainsKey(permission);

    public static bool IsUniversal(string permission) => ByName.TryGetValue(permission, out var definition) && definition.Universal;

    /// <summary>Formato <c>modulo.recurso.acao</c> ou <c>modulo.acao</c>, em minúsculas com <c>_</c>.</summary>
    public static bool IsWellFormed(string permission) => NamePattern().IsMatch(permission);

    [GeneratedRegex("^[a-z][a-z_]*(\\.[a-z][a-z_]*){1,2}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NamePattern();
}
