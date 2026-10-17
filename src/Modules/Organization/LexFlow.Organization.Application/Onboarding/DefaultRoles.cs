using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Organization.Domain.Roles;

namespace LexFlow.Organization.Application.Onboarding;

/// <summary>Perfil padrão criado no onboarding, com o escopo de dados sugerido ao atribuí-lo a alguém.</summary>
public sealed record DefaultRole(string SystemKey, string Name, string Description, DataScope SuggestedScope, IReadOnlyList<string> Permissions);

/// <summary>
/// Perfis padrão internos da seção 3.2 (decisão de 2026-10-17: os externos, Cliente e Correspondente, chegam na
/// Etapa 12; o Administrador da plataforma, na Etapa 13). A matriz é a proposta da ADR 0017, editável por escritório.
/// </summary>
public static class DefaultRoles
{
    public const string OfficeAdmin = "office_admin";
    public const string Partner = "partner";
    public const string Lawyer = "lawyer";
    public const string Paralegal = "paralegal";
    public const string Controller = "legal_controller";
    public const string Finance = "finance";

    private static readonly string[] Everything = [.. PermissionCatalog.Assignable.Select(p => p.Name)];

    private static readonly string[] BusinessArea = [.. Everything.Where(p => !p.StartsWith("settings.", StringComparison.Ordinal))];

    public static IReadOnlyList<DefaultRole> All { get; } =
    [
        new(OfficeAdmin, "Administrador do escritório", "Usuários, perfis, estrutura, configurações e integrações, além de todo o conteúdo do escritório.",
            DataScope.Organization, Everything),

        new(Partner, "Sócio", "Visão completa da área ou do escritório, aprovações e BI.",
            DataScope.Organization, [.. BusinessArea, PermissionCatalog.SettingsUsersRead, PermissionCatalog.SettingsAuditRead]),

        new(Lawyer, "Advogado", "Casos sob sua responsabilidade ou da equipe: peças, prazos e timesheet.",
            DataScope.Team,
            [
                "crm.people.read", "crm.people.create", "crm.people.edit", "crm.conflicts.check",
                "crm.opportunities.read", "crm.opportunities.manage", "crm.proposals.read", "crm.proposals.manage",
                "cases.read", "cases.create", "cases.edit", "cases.export",
                "publications.read",
                "schedule.deadlines.read", "schedule.deadlines.manage", "schedule.events.read", "schedule.events.manage",
                "schedule.tasks.read", "schedule.tasks.manage",
                "workflow.read",
                "documents.read", "documents.upload", "documents.edit", "documents.export", "documents.sign",
                "finance.fee_agreements.read", "finance.timesheet.log", "finance.timesheet.read", "finance.expenses.read", "finance.expenses.manage",
                "ai.research.use", "ai.drafts.generate",
                "reports.read", PermissionCatalog.PlatformJobsRead,
            ]),

        new(Paralegal, "Estagiário/paralegal", "Tarefas atribuídas, cadastro e anexos, sem aprovação ou exclusão.",
            DataScope.Own,
            [
                "crm.people.read", "crm.people.create", "crm.people.edit",
                "cases.read", "cases.create", "cases.edit",
                "schedule.deadlines.read", "schedule.events.read", "schedule.tasks.read", "schedule.tasks.manage",
                "documents.read", "documents.upload",
                "finance.timesheet.log", "finance.timesheet.read",
                "ai.research.use", PermissionCatalog.PlatformJobsRead,
            ]),

        new(Controller, "Controladoria jurídica", "Triagem de publicações, cadastro de prazos, distribuição e conferência de cumprimento.",
            DataScope.Organization,
            [
                "cases.read", "publications.read", "publications.triage", "publications.distribute",
                "schedule.deadlines.read", "schedule.deadlines.manage", "schedule.deadlines.verify",
                "schedule.events.read", "schedule.events.manage", "schedule.tasks.read", "schedule.tasks.manage",
                "workflow.read", "documents.read", "reports.read", PermissionCatalog.PlatformJobsRead,
            ]),

        new(Finance, "Financeiro", "Contas, faturamento, NFS-e e conciliação, sem conteúdo jurídico.",
            DataScope.Organization,
            [
                .. Everything.Where(p => p.StartsWith("finance.", StringComparison.Ordinal)),
                "crm.people.read", "reports.read", "reports.export", PermissionCatalog.PlatformJobsRead,
            ]),
    ];
}
