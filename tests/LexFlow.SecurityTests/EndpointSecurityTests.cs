using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using LexFlow.Api.IntegrationTests;
using LexFlow.Application.Abstractions;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.Crm.Infrastructure.Persistence;
using LexFlow.Identity.Domain.Sessions;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Litigation.Domain.CaseImports;
using LexFlow.Litigation.Infrastructure.Persistence;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Platform.Domain.Jobs;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.SecurityTests;

/// <summary>
/// SecurityTests da seção 6.2, item 7: descobrem todos os endpoints pelo <see cref="EndpointDataSource"/> e verificam
/// autenticação, permissão e isolamento entre duas organizações. Endpoint novo entra sozinho; se tiver parâmetro de
/// rota, precisa de um caso em <see cref="CrossTenantCases"/>, ou o teste falha apontando a rota.
/// </summary>
public sealed partial class EndpointSecurityTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] CasesRead = ["cases.read"];
    private static readonly string[] ClientRole = ["client"];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record ApiEndpoint(string Method, string Route, string? Permission, bool Public)
    {
        public bool HasRouteParameter => Route.Contains('{', StringComparison.Ordinal);

        public bool OfficeScoped => Permission is not null && !PermissionCatalog.IsUniversal(Permission);
    }

    /// <summary>O recurso da Org A que a Org B tenta alcançar pela rota, e o corpo válido da requisição.</summary>
    private sealed record CrossTenantCase(Guid Id, object? Body = null);

    /// <summary>Recurso de outra organização (ou de outro usuário) para cada rota com parâmetro.</summary>
    private Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> CrossTenantCases => WithDeadlineRoutes(WithCaseWorkflowRoutes(WithPublicationsRoutes(WithCaseRoutes(WithCommercialCases(new(StringComparer.Ordinal)
    {
        ["GET /api/v1/jobs/{id:guid}"] = async owner => new CrossTenantCase(await CreateJobAsync(owner.OrganizationId)),
        ["DELETE /api/v1/me/sessions/{id:guid}"] = async owner => new CrossTenantCase(await CreateSessionAsync(owner.UserId)),
        ["PUT /api/v1/organization/members/{id:guid}/roles"] = owner =>
            Task.FromResult(new CrossTenantCase(owner.MemberId, new { roles = new[] { new { roleId = owner.RoleId, scope = "organization", scopeRefId = (Guid?)null } } })),
        ["PUT /api/v1/roles/{id:guid}/permissions"] = owner =>
            Task.FromResult(new CrossTenantCase(owner.RoleId, new { permissions = CasesRead })),
        ["PUT /api/v1/roles/{id:guid}"] = owner => Task.FromResult(new CrossTenantCase(owner.RoleId, new { name = "Tomado" })),
        ["DELETE /api/v1/roles/{id:guid}"] = owner => Task.FromResult(new CrossTenantCase(owner.RoleId)),
        ["DELETE /api/v1/organization/members/{id:guid}"] = owner => Task.FromResult(new CrossTenantCase(owner.MemberId)),
        ["POST /api/v1/organization/invitations/{id:guid}/resend"] = async owner => new CrossTenantCase(await InvitationOfAsync(owner)),
        ["DELETE /api/v1/organization/invitations/{id:guid}"] = async owner => new CrossTenantCase(await InvitationOfAsync(owner)),
        ["PUT /api/v1/organization/branches/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "branches", new { name = "Matriz" }), new { name = "Tomada" }),
        ["DELETE /api/v1/organization/branches/{id:guid}"] = async owner => new CrossTenantCase(await CreateAsOwnerAsync(owner, "branches", new { name = "Matriz" })),
        ["PUT /api/v1/organization/practice-areas/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "practice-areas", new { name = "Cível" }), new { name = "Tomada" }),
        ["DELETE /api/v1/organization/practice-areas/{id:guid}"] = async owner => new CrossTenantCase(await CreateAsOwnerAsync(owner, "practice-areas", new { name = "Cível" })),
        ["PUT /api/v1/organization/teams/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "teams", new { name = "Equipe" }), new { name = "Tomada" }),
        ["DELETE /api/v1/organization/teams/{id:guid}"] = async owner => new CrossTenantCase(await CreateAsOwnerAsync(owner, "teams", new { name = "Equipe" })),
        ["PUT /api/v1/organization/teams/{id:guid}/members"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "teams", new { name = "Equipe" }), new { memberIds = Array.Empty<Guid>() }),
        ["PUT /api/v1/organization/cost-centers/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "cost-centers", new { code = "ADM", name = "Administrativo" }), new { code = "TOM", name = "Tomado" }),
        ["DELETE /api/v1/organization/cost-centers/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "cost-centers", new { code = "ADM", name = "Administrativo" })),
        ["PUT /api/v1/organization/partners/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "partners", new { name = "Sócia", sharePercentage = 10m }), new { name = "Tomada", sharePercentage = 10m }),
        ["DELETE /api/v1/organization/partners/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "partners", new { name = "Sócia", sharePercentage = 10m })),
        ["PUT /api/v1/custom-fields/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, "/api/v1/custom-fields", new { entity = "cases", key = $"campo_{Guid.CreateVersion7():N}", type = "text", label = "Campo" }), new { label = "Tomado" }),
        ["PUT /api/v1/lookup-tables/{id:guid}"] = async owner =>
            new CrossTenantCase(await LookupTableOfAsync(owner), new { name = $"Tomada {Guid.CreateVersion7():N}", isActive = true }),
        ["POST /api/v1/lookup-tables/{id:guid}/items"] = async owner => new CrossTenantCase(await LookupTableOfAsync(owner), new { name = "Item" }),
        ["GET /api/v1/people/{id:guid}"] = async owner => new CrossTenantCase(await PersonOfAsync(owner)),
        ["PUT /api/v1/people/{id:guid}"] = async owner => new CrossTenantCase(await PersonOfAsync(owner), new { name = "Tomada", roles = ClientRole }),
        ["DELETE /api/v1/people/{id:guid}"] = async owner => new CrossTenantCase(await PersonOfAsync(owner)),
        ["POST /api/v1/people/{id:guid}/merge"] = async owner =>
            new CrossTenantCase(await PersonOfAsync(owner), new { duplicateId = await PersonOfAsync(owner) }),
        ["POST /api/v1/people/{id:guid}/anonymize"] = async owner => new CrossTenantCase(await PersonOfAsync(owner), new { reason = "Pedido do titular" }),
        ["GET /api/v1/conflict-checks/{id:guid}"] = async owner => new CrossTenantCase(await ConflictCheckOfAsync(owner)),
        ["POST /api/v1/conflict-checks/{id:guid}/proceed"] = async owner =>
            new CrossTenantCase(await ConflictCheckOfAsync(owner), new { justification = "Sem relação" }),
        ["PUT /api/v1/economic-groups/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, "/api/v1/economic-groups", new { name = $"Grupo {Guid.CreateVersion7():N}" }), new { name = "Tomado" }),
        ["DELETE /api/v1/economic-groups/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, "/api/v1/economic-groups", new { name = $"Grupo {Guid.CreateVersion7():N}" })),
        ["PUT /api/v1/lookup-items/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, $"/api/v1/lookup-tables/{await LookupTableOfAsync(owner)}/items", new { name = "Item" }), new { name = "Tomado", position = 1, isActive = true }),
    })))));

    private Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> WithPublicationsRoutes(Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> cases)
    {
        cases.Add("GET /api/v1/publication-sources/{id:guid}", async owner => new CrossTenantCase(await SourceOfAsync(owner)));
        cases.Add("GET /api/v1/publication-sources/{id:guid}/capture", async owner => new CrossTenantCase(await SourceOfAsync(owner)));
        cases.Add("POST /api/v1/publication-sources/{id:guid}/capture", async owner => new CrossTenantCase(await SourceOfAsync(owner), new { day = "2026-10-19" }));
        cases.Add("POST /api/v1/notifications/{id:guid}/read", async owner => new CrossTenantCase(await NotificationOfAsync(owner)));
        cases.Add("PUT /api/v1/publication-sources/{id:guid}", async owner => new CrossTenantCase(await SourceOfAsync(owner), new { kind = "name", label = "Fonte", value = "Maria" }));
        cases.Add("PUT /api/v1/schedule/calendar-entries/{id:guid}", async owner => new CrossTenantCase(await CalendarEntryOfAsync(owner), CalendarEntryBody("Tomado")));
        cases.Add("DELETE /api/v1/schedule/calendar-entries/{id:guid}", async owner => new CrossTenantCase(await CalendarEntryOfAsync(owner)));
        cases.Add("GET /api/v1/publications/{id:guid}", async owner => new CrossTenantCase(await PublicationOfAsync(owner)));
        cases.Add("PUT /api/v1/publications/{id:guid}/link", async owner => new CrossTenantCase(await PublicationOfAsync(owner), new { caseId = Guid.CreateVersion7() }));
        cases.Add("PUT /api/v1/publications/{id:guid}/assignment", async owner => new CrossTenantCase(await PublicationOfAsync(owner), new { assigneeUserId = owner.UserId }));
        cases.Add("POST /api/v1/publications/{id:guid}/triage", async owner => new CrossTenantCase(await PublicationOfAsync(owner),
            new { classification = "other", action = "discard", confirmed = true, reason = "Homônimo" }));
        return cases;
    }

    private Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> WithDeadlineRoutes(Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> cases)
    {
        cases.Add("GET /api/v1/cases/{id:guid}/deadlines", async owner => new CrossTenantCase(await CaseOfAsync(owner)));
        cases.Add("POST /api/v1/cases/{id:guid}/deadlines", async owner => new CrossTenantCase(await CaseOfAsync(owner), new
        {
            type = "procedural",
            title = "Prazo de teste",
            assigneeUserId = owner.UserId,
            rule = new { startKind = "publication", triggerDate = "2027-02-01", consultedOn = (string?)null, days = 5, mode = "businessDays", doubled = false, internalLeadDays = 0 },
            confirmedInternalDueOn = "2027-02-08",
            confirmedFatalDueOn = "2027-02-08",
            confirmed = true,
        }));
        cases.Add("GET /api/v1/deadlines/{id:guid}", async owner => new CrossTenantCase(await DeadlineOfAsync(owner)));
        cases.Add("POST /api/v1/deadlines/{id:guid}/start", async owner => new CrossTenantCase(await DeadlineOfAsync(owner)));
        cases.Add("POST /api/v1/deadlines/{id:guid}/review", async owner => new CrossTenantCase(await DeadlineOfAsync(owner)));
        cases.Add("POST /api/v1/deadlines/{id:guid}/complete", async owner => new CrossTenantCase(await DeadlineOfAsync(owner), new { evidence = "Protocolo" }));
        cases.Add("POST /api/v1/deadlines/{id:guid}/miss", async owner => new CrossTenantCase(await DeadlineOfAsync(owner), new { reason = "Justificativa" }));
        return cases;
    }

    /// <summary>Funil, propostas e modelos (Etapa 4, PR 4C).</summary>
    private Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> WithCommercialCases(Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> cases)
    {
        var commercial = new Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>>(StringComparer.Ordinal)
        {
            ["GET /api/v1/opportunities/{id:guid}"] = async owner => new CrossTenantCase(await OpportunityOfAsync(owner)),
            ["PUT /api/v1/opportunities/{id:guid}"] = async owner =>
                new CrossTenantCase(await OpportunityOfAsync(owner), new { title = "Tomada", prospect = new { kind = "company", name = "Tomada" } }),
            ["DELETE /api/v1/opportunities/{id:guid}"] = async owner => new CrossTenantCase(await OpportunityOfAsync(owner)),
            ["POST /api/v1/opportunities/{id:guid}/stage"] = async owner => new CrossTenantCase(await OpportunityOfAsync(owner), new { stage = "meeting" }),
            ["POST /api/v1/opportunities/{id:guid}/lose"] = async owner =>
                new CrossTenantCase(await OpportunityOfAsync(owner), new { reasonId = Guid.CreateVersion7(), note = "Preço" }),
            ["POST /api/v1/opportunities/{id:guid}/conflict-check"] = async owner => new CrossTenantCase(await OpportunityOfAsync(owner)),
            ["POST /api/v1/opportunities/{id:guid}/proposals"] = async owner => new CrossTenantCase(await OpportunityOfAsync(owner), new { title = "Tomada" }),
            ["GET /api/v1/proposals/{id:guid}"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner)),
            ["GET /api/v1/proposals/{id:guid}/document"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner)),
            ["PUT /api/v1/proposals/{id:guid}"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner), new { title = "Tomada" }),
            ["DELETE /api/v1/proposals/{id:guid}"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner)),
            ["POST /api/v1/proposals/{id:guid}/submit"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner)),
            ["POST /api/v1/proposals/{id:guid}/approve"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner)),
            ["POST /api/v1/proposals/{id:guid}/return"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner), new { note = "Rever" }),
            ["POST /api/v1/proposals/{id:guid}/decline"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner), new { note = "Caro" }),
            ["POST /api/v1/proposals/{id:guid}/send"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner)),
            ["POST /api/v1/proposals/{id:guid}/accept"] = async owner => new CrossTenantCase(await ProposalOfAsync(owner)),
            ["DELETE /api/v1/proposal-templates/{id:guid}"] = async owner => new CrossTenantCase(await ProposalTemplateOfAsync(owner)),
        };
        foreach (var (route, create) in commercial)
        {
            cases.Add(route, create);
        }

        return cases;
    }

    /// <summary>Processos e casos (Etapa 5, PRs 5A a 5D). Os corpos passam na validação; o 404 vem do caso de outra organização.</summary>
    private Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> WithCaseRoutes(Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> cases)
    {
        var parties = new[] { new { personId = Guid.CreateVersion7(), pole = "active", isClient = true } };
        var claim = new { claimTypeId = Guid.CreateVersion7(), valuations = Array.Empty<object>() };
        var hearing = new { type = "conciliation", format = "virtual", scheduledAt = "2026-12-01T10:00:00Z", meetingLink = "https://meet.example.com/x" };
        var routes = new Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>>(StringComparer.Ordinal)
        {
            ["GET /api/v1/cases/{id:guid}/movements"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/{id:guid}/tasks"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/{id:guid}/movement-sync"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["POST /api/v1/cases/{id:guid}/movements"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { occurredAt = "2026-10-19T12:00:00Z", description = "Sentença" }),
            ["GET /api/v1/cases/{id:guid}"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["PUT /api/v1/cases/{id:guid}"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { parties }),
            ["DELETE /api/v1/cases/{id:guid}"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["POST /api/v1/cases/{id:guid}/close"] = async owner =>
                new CrossTenantCase(await CaseOfAsync(owner), new { reasonId = Guid.CreateVersion7(), outcome = "settlement", closedOn = "2026-09-01" }),
            ["POST /api/v1/cases/{id:guid}/reopen"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["POST /api/v1/cases/{id:guid}/status"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { status = "suspended" }),
            ["POST /api/v1/cases/{id:guid}/relations"] = async owner =>
                new CrossTenantCase(await CaseOfAsync(owner), new { childCaseId = await CaseOfAsync(owner), kind = "appeal" }),
            ["DELETE /api/v1/cases/{id:guid}/relations/{relationId:guid}"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/{id:guid}/restrictions"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["PUT /api/v1/cases/{id:guid}/restrictions"] = async owner =>
                new CrossTenantCase(await CaseOfAsync(owner), new { allowed = Array.Empty<object>(), blocked = Array.Empty<object>() }),
            ["GET /api/v1/cases/{id:guid}/claims"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["POST /api/v1/cases/{id:guid}/claims"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), claim),
            ["PUT /api/v1/cases/{id:guid}/claims/{claimId:guid}"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), claim),
            ["DELETE /api/v1/cases/{id:guid}/claims/{claimId:guid}"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/{id:guid}/claims/{claimId:guid}/history"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/{id:guid}/risk"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/{id:guid}/risk/history"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["POST /api/v1/cases/{id:guid}/risk-assessments"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { probability = "remote" }),
            ["GET /api/v1/cases/{id:guid}/settlements"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/{id:guid}/hearings"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["POST /api/v1/cases/{id:guid}/hearings"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), hearing),
            ["PUT /api/v1/cases/{id:guid}/hearings/{hearingId:guid}"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), hearing),
            ["POST /api/v1/cases/{id:guid}/hearings/{hearingId:guid}/reschedule"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { newDate = "2026-12-01T10:00:00Z" }),
            ["POST /api/v1/cases/{id:guid}/hearings/{hearingId:guid}/hold"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { text = "x" }),
            ["POST /api/v1/cases/{id:guid}/hearings/{hearingId:guid}/cancel"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { text = "x" }),
            ["GET /api/v1/cases/{id:guid}/timeline"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["GET /api/v1/cases/imports/{importId:guid}"] = async owner => new CrossTenantCase(await ImportOfAsync(owner)),
            ["POST /api/v1/cases/imports/{importId:guid}/confirm"] = async owner => new CrossTenantCase(await ImportOfAsync(owner)),
            ["GET /api/v1/cases/imports/{importId:guid}/errors"] = async owner => new CrossTenantCase(await ImportOfAsync(owner)),
        };
        foreach (var (route, create) in routes)
        {
            cases.Add(route, create);
        }

        return cases;
    }

    private Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> WithCaseWorkflowRoutes(Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> cases)
    {
        var routes = new Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>>(StringComparer.Ordinal)
        {
            ["POST /api/v1/cases/{id:guid}/settlements"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { direction = "clientPays" }),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/offers"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { party = "office", amount = 1m, offeredOn = "2026-10-01" }),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/submit"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { agreedAmount = 1m, installments = new[] { new { dueOn = "2026-11-01", amount = 1m } } }),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/approve"] = async owner => new CrossTenantCase(await CaseOfAsync(owner)),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/return"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { reason = "x" }),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/homologate"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { homologatedOn = "2026-10-01" }),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/reject"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { reason = "x" }),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/cancel"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { reason = "x" }),
            ["POST /api/v1/cases/{id:guid}/settlements/{settlementId:guid}/installments/{installmentId:guid}/pay"] = async owner => new CrossTenantCase(await CaseOfAsync(owner), new { paidOn = "2026-11-01", paidAmount = 1m }),
        };
        foreach (var (route, create) in routes)
        {
            cases.Add(route, create);
        }

        return cases;
    }

    /// <summary>Importação já validada (pronta para confirmar) da organização do dono, gravada direto no banco.</summary>
    private Task<Guid> SourceOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/publication-sources", new { kind = "name", label = "Maria", value = "Maria" });

    private static object CalendarEntryBody(string name) => new
    {
        name,
        kind = "holiday",
        scope = "office",
        startsOn = "2026-10-28",
        endsOn = "2026-10-28",
        recursYearly = false,
    };

    private Task<Guid> CalendarEntryOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/schedule/calendar-entries", CalendarEntryBody($"Dia {Guid.CreateVersion7():N}"));

    private async Task<Guid> PublicationOfAsync(SeededMember owner)
    {
        var source = await SourceOfAsync(owner);
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "security-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PublicationsDbContext>();
        var day = new DateOnly(2026, 10, 19);
        var publication = Publication.Receive(new(source, "DJE", day, day, "Texto " + Guid.CreateVersion7()), new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero)).Value;
        db.Publications.Add(publication);
        await db.SaveChangesAsync(Ct);
        return publication.Id;
    }

    private async Task<Guid> NotificationOfAsync(SeededMember owner)
    {
        var publication = await PublicationOfAsync(owner);
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "security-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var notification = Notification.Create($"publication:{publication}", "publications.received", publication, null,
            new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(Ct);
        return notification.Id;
    }

    private async Task<Guid> ImportOfAsync(SeededMember owner)
    {
        var id = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "security-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LitigationDbContext>();
        var import = CaseImport.Start(id, "processos.csv", Guid.CreateVersion7());
        import.Validated(1, 0, "[]");
        db.CaseImports.Add(import);
        await db.SaveChangesAsync(Ct);
        return id;
    }

    /// <summary>Caso extrajudicial (sem CNJ) da organização do dono, com uma pessoa dela como cliente.</summary>
    private async Task<Guid> CaseOfAsync(SeededMember owner) =>
        await PostAsOwnerAsync(owner, "/api/v1/cases", new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = await PersonOfAsync(owner), pole = "active", isClient = true } },
        });

    private async Task<Guid> DeadlineOfAsync(SeededMember owner)
    {
        var caseId = await CaseOfAsync(owner);
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "security-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(owner.OrganizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var deadline = Deadline.Create(caseId, Guid.CreateVersion7(), "Prazo de teste", new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 27), owner.UserId, true, owner.UserId,
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero)).Value;
        db.Deadlines.Add(deadline);
        await db.SaveChangesAsync(Ct);
        return deadline.Id;
    }

    private IReadOnlyList<ApiEndpoint> Endpoints() =>
        [.. _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [HttpMethods.Get]).Select(method => new ApiEndpoint(
                method,
                e.RoutePattern.RawText!,
                e.Metadata.GetMetadata<RequiredPermissionAttribute>()?.Permission,
                e.Metadata.GetMetadata<PublicEndpointAttribute>() is not null)))];

    [GeneratedRegex(@"\{[^}]+\}", RegexOptions.None, 1000)]
    private static partial Regex RouteParameter();

    private static string Fill(string route, Guid id) => RouteParameter().Replace(route, id.ToString());

    private async Task<HttpResponseMessage> SendAsync(ApiEndpoint endpoint, string path, SeededMember? caller, object? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), path);
        if (caller is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeader, caller.UserId.ToString());
            request.Headers.Add(TenantResolutionMiddleware.HeaderName, caller.OrganizationId.ToString());
        }

        if (endpoint.Method is "POST" or "PUT" or "PATCH")
        {
            request.Content = JsonContent.Create(body ?? new { });
        }

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>Membro com todas as permissões atribuíveis numa organização nova.</summary>
    private Task<SeededMember> FullAccessMemberAsync() =>
        TestOrganizations.CreateWithMemberAsync(dependencies, [.. PermissionCatalog.Assignable.Select(p => p.Name)]);

    /// <summary>
    /// Cadastro de estrutura da organização do dono, pela própria API. O nome leva um sufixo único porque a mesma
    /// organização recebe vários casos.
    /// </summary>
    private async Task<Guid> CreateAsOwnerAsync(SeededMember owner, string resource, object body)
    {
        var values = System.Text.Json.JsonSerializer.SerializeToNode(body)!.AsObject();
        values["name"] = $"{values["name"]} {Guid.CreateVersion7():N}";
        if (values.ContainsKey("code"))
        {
            values["code"] = $"C{Guid.CreateVersion7():N}"[..20];
        }

        return await PostAsOwnerAsync(owner, $"/api/v1/organization/{resource}", values);
    }

    private sealed record CreatedResource(Guid Id);

    /// <summary>Recurso da organização do dono criado pela própria API (resposta 201 com <c>id</c>).</summary>
    private async Task<Guid> PostAsOwnerAsync(SeededMember owner, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(TestAuthenticationHandler.UserHeader, owner.UserId.ToString());
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, owner.OrganizationId.ToString());
        using var response = await _client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<CreatedResource>(Ct))!.Id;
    }

    private Task<Guid> PersonOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/people", new { kind = "company", name = $"Pessoa {Guid.CreateVersion7():N}", roles = ClientRole });

    private Task<Guid> ConflictCheckOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/conflict-checks", new { name = $"Consulta {Guid.CreateVersion7():N}", side = "client" });

    private Task<Guid> OpportunityOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/opportunities", new { title = "Oportunidade", prospect = new { kind = "company", name = $"Interessado {Guid.CreateVersion7():N}" } });

    private async Task<Guid> ProposalOfAsync(SeededMember owner) =>
        await PostAsOwnerAsync(owner, $"/api/v1/opportunities/{await OpportunityOfAsync(owner)}/proposals", new
        {
            title = "Proposta",
            items = new[] { new { description = "Mensal", kind = "monthly", quantity = 1, unitAmount = 100m } },
        });

    /// <summary>Modelo gravado direto no banco: o upload passa pelo antivírus, que não roda nos SecurityTests.</summary>
    private async Task<Guid> ProposalTemplateOfAsync(SeededMember owner)
    {
        var template = ProposalTemplate.Create(Guid.CreateVersion7(), $"Modelo {Guid.CreateVersion7():N}", "modelo.docx", 1, new string('0', 64));
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "security-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        db.ProposalTemplates.Add(template);
        await db.SaveChangesAsync(Ct);
        return template.Id;
    }

    private Task<Guid> LookupTableOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/lookup-tables", new { name = $"Tabela {Guid.CreateVersion7():N}" });

    /// <summary>Convite pendente da organização do dono, para o perfil dele.</summary>
    private Task<Guid> InvitationOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/organization/invitations", new
        {
            email = $"{Guid.CreateVersion7():N}@exemplo.com.br",
            roles = new[] { new { roleId = owner.RoleId, scope = "own" } },
        });

    private async Task<Guid> CreateJobAsync(Guid organization)
    {
        var id = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(organization, "security-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Jobs.Add(Job.Enqueue(id, "documents.import"));
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> CreateSessionAsync(Guid userId)
    {
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        db.Users.Add(ApplicationUser.Register(userId, $"{userId:N}@exemplo.com.br", "Dono da sessão", new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero)));
        var session = Session.Start(Guid.CreateVersion7(), userId, "203.0.113.9", "SecurityTests", new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        db.Sessions.Add(session);
        await db.SaveChangesAsync(Ct);
        return session.Id;
    }

    [Fact]
    public void Discovery_finds_the_api_endpoints()
    {
        var endpoints = Endpoints();

        endpoints.Count.ShouldBeGreaterThan(10);
        endpoints.ShouldContain(e => e.Route == "/api/v1/jobs/{id:guid}");
    }

    [Fact]
    public async Task Every_protected_endpoint_rejects_anonymous_requests()
    {
        var failures = new List<string>();
        foreach (var endpoint in Endpoints().Where(e => !e.Public))
        {
            using var response = await SendAsync(endpoint, Fill(endpoint.Route, Guid.CreateVersion7()), caller: null, body: null);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                failures.Add($"{endpoint.Method} {endpoint.Route} → {(int)response.StatusCode}");
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_office_endpoint_refuses_a_member_without_the_permission()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies);
        var failures = new List<string>();
        foreach (var endpoint in Endpoints().Where(e => e.OfficeScoped))
        {
            using var response = await SendAsync(endpoint, Fill(endpoint.Route, Guid.CreateVersion7()), member, body: null);
            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{endpoint.Method} {endpoint.Route} → {(int)response.StatusCode}");
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_route_with_an_id_has_a_cross_tenant_case()
    {
        var cases = CrossTenantCases;
        var missing = Endpoints()
            .Where(e => !e.Public && e.HasRouteParameter)
            .Select(e => $"{e.Method} {e.Route}")
            .Where(key => !cases.ContainsKey(key))
            .ToList();

        missing.ShouldBeEmpty("Endpoint com parâmetro de rota sem caso entre organizações nos SecurityTests.");
    }

    /// <summary>Critério de aceite da Etapa 3: o usuário da Org A recebe 404 em qualquer recurso da Org B.</summary>
    [Fact]
    public async Task Resources_of_another_organization_or_user_answer_404()
    {
        var owner = await FullAccessMemberAsync();
        var intruder = await FullAccessMemberAsync();
        var cases = CrossTenantCases;
        var failures = new List<string>();

        foreach (var endpoint in Endpoints().Where(e => !e.Public && e.HasRouteParameter))
        {
            var target = await cases[$"{endpoint.Method} {endpoint.Route}"](owner);
            using var response = await SendAsync(endpoint, Fill(endpoint.Route, target.Id), intruder, target.Body);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                failures.Add($"{endpoint.Method} {endpoint.Route} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Lists_never_include_data_of_another_organization()
    {
        var owner = await FullAccessMemberAsync();
        var reader = await FullAccessMemberAsync();
        var secrets = new[] { owner.OrganizationId, owner.MemberId, owner.RoleId, owner.UserId }.Select(id => id.ToString()).ToList();
        var failures = new List<string>();

        foreach (var endpoint in Endpoints().Where(e => !e.Public && !e.HasRouteParameter && e.Method == HttpMethods.Get))
        {
            using var response = await SendAsync(endpoint, endpoint.Route, reader, body: null);
            var content = await response.Content.ReadAsStringAsync(Ct);
            if (secrets.Exists(secret => content.Contains(secret, StringComparison.OrdinalIgnoreCase)))
            {
                failures.Add($"GET {endpoint.Route} expõe dados de outra organização");
            }
        }

        failures.ShouldBeEmpty();
    }
}
