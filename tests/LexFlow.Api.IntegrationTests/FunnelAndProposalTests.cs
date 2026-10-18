using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Crm.Contracts;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Funil de oportunidades e propostas de honorários (RF-08, Etapa 4, PR 4C) pela API real: aprovação interna, envio
/// com o DOCX congelado no armazenamento, aceite que exige o conflito prosseguido e cria o cliente, e os modelos DOCX.
/// O antivírus é falso (o ClamAV real é testado nos adaptadores).
/// </summary>
public sealed class FunnelAndProposalTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly string[] Seller =
    [
        "crm.people.read", "crm.people.create", "crm.conflicts.check", "crm.opportunities.read", "crm.opportunities.manage",
        "crm.proposals.read", "crm.proposals.manage", "settings.custom_fields.read",
    ];

    private static readonly string[] Approver = ["crm.proposals.read", "crm.proposals.approve"];

    private static readonly string[] WitnessRole = ["witness"];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    /// <summary>Antivírus falso: o arquivo com "EICAR" é infectado.</summary>
    private sealed class FakeScanner : IFileScanner
    {
        public async Task<FileScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(content);
            return (await reader.ReadToEndAsync(cancellationToken)).Contains("EICAR", StringComparison.Ordinal) ? FileScanResult.Infected("Eicar-Test-Signature") : FileScanResult.Clean;
        }
    }

    public async ValueTask InitializeAsync()
    {
        var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IFileScanner, FakeScanner>()));
        _http = factory.CreateClient();
        _api = new OrganizationApi(_http);
        await factory.Services.GetRequiredService<IObjectStorageProvisioner>().EnsureBucketAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record Office(Guid Organization, Guid Seller, Guid Approver, Guid Origin, Guid LossReason);

    /// <summary>Escritório com um vendedor, um aprovador e as tabelas auxiliares do sistema (como faz o Worker no onboarding).</summary>
    private async Task<Office> OfficeAsync()
    {
        var seller = await TestOrganizations.CreateWithMemberAsync(dependencies, Seller);
        var approver = await TestOrganizations.AddMemberAsync(dependencies, seller.OrganizationId, Guid.CreateVersion7(), Approver);
        using (AmbientContext.Begin(seller.OrganizationId, "funnel-tests"))
        await using (var provider = dependencies.PlatformServices())
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LookupTableSeeder>().SeedAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().SaveChangesAsync(Ct);
        }

        var tables = (await _api.SendAsync(HttpMethod.Get, "/api/v1/lookup-tables", seller.UserId, seller.OrganizationId)).Body;
        Guid FirstItem(string key) => tables.EnumerateArray()
            .Single(t => t.GetProperty("systemKey").ValueKind == JsonValueKind.String && t.GetProperty("systemKey").GetString() == key)
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        return new Office(seller.OrganizationId, seller.UserId, approver.UserId, FirstItem(SystemLookupTables.OpportunityOrigins), FirstItem(SystemLookupTables.LossReasons));
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(Office office, HttpMethod method, string path, object? body = null, bool asApprover = false) =>
        _api.SendAsync(method, path, asApprover ? office.Approver : office.Seller, office.Organization, body);

    private Task<Guid> OpportunityAsync(Office office, string name = "Construtora Horizonte S.A.", string? document = "11222333000181", Guid? personId = null) =>
        _api.CreateAsync("/api/v1/opportunities", office.Seller, office.Organization, new
        {
            title = "Contencioso trabalhista",
            personId,
            prospect = personId is null ? new { kind = "company", name, document, email = "contato@horizonte.com.br" } : null,
            originId = office.Origin == Guid.Empty ? (Guid?)null : office.Origin,
            estimatedValue = 60_000m,
            responsibleUserId = office.Seller,
        });

    private Task<Guid> ProposalAsync(Office office, Guid opportunity, Guid? templateId = null) =>
        _api.CreateAsync($"/api/v1/opportunities/{opportunity}/proposals", office.Seller, office.Organization, new
        {
            title = "Proposta 2026",
            scope = "Defesa nas reclamações trabalhistas",
            validUntil = "2099-12-31",
            templateId,
            items = new object[]
            {
                new { description = "Acompanhamento mensal", kind = "monthly", quantity = 12, unitAmount = 3_500m },
                new { description = "Êxito", kind = "success", quantity = 1, percentage = 20m },
            },
        });

    /// <summary>Rascunho → aprovação (pelo aprovador) → envio.</summary>
    private async Task SentAsync(Office office, Guid proposal)
    {
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/submit")).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/approve", asApprover: true)).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/send")).Status.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<(HttpStatusCode Status, string? ContentType, byte[] Content)> DownloadAsync(Office office, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, office.Seller.ToString());
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, office.Organization.ToString());
        using var response = await _http.SendAsync(request, Ct);
        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, await response.Content.ReadAsByteArrayAsync(Ct));
    }

    /// <summary>Códigos dos erros por campo de um ProblemDetails de validação.</summary>
    private static List<string?> FieldCodes(JsonElement problem) =>
        [.. problem.GetProperty("errors").EnumerateObject().SelectMany(f => f.Value.EnumerateArray()).Select(e => e.GetProperty("code").GetString())];

    private static string TextOf(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart!.Document!.Body!.InnerText;
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> UploadAsync(Office office, string name, byte[] content, string fileName = "modelo.docx")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(name), "name");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/proposal-templates") { Content = form };
        request.Headers.Add(TestAuthenticationHandler.UserHeader, office.Seller.ToString());
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, office.Organization.ToString());
        using var response = await _http.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Fact]
    public async Task An_accepted_proposal_needs_approval_by_someone_else_and_a_cleared_conflict_and_creates_the_client()
    {
        var office = await OfficeAsync();
        var opportunity = await OpportunityAsync(office);
        var proposal = await ProposalAsync(office, opportunity);

        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/submit")).Status.ShouldBe(HttpStatusCode.OK);
        var (forbidden, _) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/approve");
        var (earlyAcceptStatus, earlyAccept) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/accept");
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/approve", asApprover: true)).Status.ShouldBe(HttpStatusCode.OK);
        var (sendStatus, sent) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/send");
        var (_, movedOpportunity) = await SendAsync(office, HttpMethod.Get, $"/api/v1/opportunities/{opportunity}");
        var (noCheckStatus, noCheck) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/accept");

        var (checkStatus, check) = await SendAsync(office, HttpMethod.Post, $"/api/v1/opportunities/{opportunity}/conflict-check");
        var (notProceededStatus, notProceeded) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/accept");
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/conflict-checks/{check.GetProperty("id").GetGuid()}/proceed", new { justification = (string?)null })).Status.ShouldBe(HttpStatusCode.OK);
        var (acceptStatus, accepted) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/accept");

        forbidden.ShouldBe(HttpStatusCode.Forbidden, "o vendedor não tem a permissão de aprovar");
        earlyAcceptStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(earlyAccept).ShouldBe("crm.proposal_not_sent");
        sendStatus.ShouldBe(HttpStatusCode.OK);
        sent.GetProperty("status").GetString().ShouldBe("sent");
        sent.GetProperty("total").GetDecimal().ShouldBe(42_000m);
        movedOpportunity.GetProperty("stage").GetString().ShouldBe("proposal");
        noCheckStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(noCheck).ShouldBe("crm.conflict_check_required");
        checkStatus.ShouldBe(HttpStatusCode.Created);
        check.GetProperty("side").GetString().ShouldBe("client");
        check.GetProperty("queryDocument").GetString().ShouldBe("11222333000181");
        OrganizationApi.Code(notProceeded).ShouldBe("crm.conflict_check_required");
        notProceededStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);

        acceptStatus.ShouldBe(HttpStatusCode.OK, accepted.ToString());
        await ClientWasCreatedAsync(office, opportunity, proposal, accepted);
    }

    /// <summary>O interessado virou cliente, a oportunidade fechou ganha e o evento saiu com os itens.</summary>
    private async Task ClientWasCreatedAsync(Office office, Guid opportunity, Guid proposal, JsonElement accepted)
    {
        accepted.GetProperty("proposal").GetProperty("status").GetString().ShouldBe("accepted");
        var client = accepted.GetProperty("clientPersonId").GetGuid();
        var (_, person) = await SendAsync(office, HttpMethod.Get, $"/api/v1/people/{client}");
        person.GetProperty("name").GetString().ShouldBe("Construtora Horizonte S.A.");
        person.GetProperty("document").GetString().ShouldBe("11222333000181");
        person.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["client"]);
        var (_, won) = await SendAsync(office, HttpMethod.Get, $"/api/v1/opportunities/{opportunity}");
        won.GetProperty("stage").GetString().ShouldBe("won");
        won.GetProperty("personId").GetGuid().ShouldBe(client);

        var published = _factory.PublishedEvents.OfType<ProposalAcceptedIntegrationEvent>().Single(e => e.ProposalId == proposal);
        published.ClientPersonId.ShouldBe(client);
        published.Total.ShouldBe(42_000m);
        published.Items.Select(i => i.Kind).ShouldBe(["Monthly", "Success"]);
    }

    /// <summary>Decisão de 2026-10-18 (ADR 0024): o único membro do escritório aprova a própria proposta; com outro membro, não.</summary>
    [Fact]
    public async Task The_only_member_approves_the_own_proposal_until_someone_else_joins()
    {
        var solo = await TestOrganizations.CreateWithMemberAsync(dependencies, [.. Seller, .. Approver]);
        var office = new Office(solo.OrganizationId, solo.UserId, solo.UserId, Guid.Empty, Guid.Empty);
        var first = await ProposalAsync(office, await OpportunityAsync(office, document: null));
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{first}/submit")).Status.ShouldBe(HttpStatusCode.OK);

        var (soloStatus, approved) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{first}/approve");

        soloStatus.ShouldBe(HttpStatusCode.OK, approved.ToString());
        approved.GetProperty("status").GetString().ShouldBe("approved");
        approved.GetProperty("approvedBy").GetGuid().ShouldBe(solo.UserId);

        await TestOrganizations.AddMemberAsync(dependencies, solo.OrganizationId, Guid.CreateVersion7(), Approver);
        var second = await ProposalAsync(office, await OpportunityAsync(office, name: "Outro Interessado", document: null));
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{second}/submit")).Status.ShouldBe(HttpStatusCode.OK);
        var (teamStatus, refused) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{second}/approve");

        teamStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(refused).ShouldBe("crm.proposal_self_approval");
    }

    /// <summary>Regressão (PR 5B): item novo numa edição do rascunho é inserido (o id vem do domínio).</summary>
    [Fact]
    public async Task Editing_a_draft_replaces_its_items()
    {
        var office = await OfficeAsync();
        var proposal = await ProposalAsync(office, await OpportunityAsync(office));

        var (status, updated) = await SendAsync(office, HttpMethod.Put, $"/api/v1/proposals/{proposal}", new
        {
            title = "Proposta revisada",
            items = new object[]
            {
                new { description = "Mensal", kind = "monthly", quantity = 12, unitAmount = 3_000m },
                new { description = "Audiências", kind = "perAct", quantity = 2, unitAmount = 500m },
            },
        });

        status.ShouldBe(HttpStatusCode.OK, updated.ToString());
        updated.GetProperty("items").GetArrayLength().ShouldBe(2);
        updated.GetProperty("total").GetDecimal().ShouldBe(37_000m);
    }

    [Fact]
    public async Task The_sent_document_is_frozen_while_a_draft_is_rendered_on_demand()
    {
        var office = await OfficeAsync();
        var opportunity = await OpportunityAsync(office);
        var proposal = await ProposalAsync(office, opportunity);

        var (draftStatus, contentType, draft) = await DownloadAsync(office, $"/api/v1/proposals/{proposal}/document");
        await SentAsync(office, proposal);
        await SendAsync(office, HttpMethod.Put, $"/api/v1/opportunities/{opportunity}", new
        {
            title = "Outro título",
            prospect = new { kind = "company", name = "Nome Trocado Ltda.", document = "11222333000181" },
        });
        var (_, _, frozen) = await DownloadAsync(office, $"/api/v1/proposals/{proposal}/document");

        draftStatus.ShouldBe(HttpStatusCode.OK);
        contentType.ShouldBe("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        TextOf(draft).ShouldContain("Construtora Horizonte S.A.");
        TextOf(draft).ShouldContain("R$ 42.000,00");
        TextOf(frozen).ShouldContain("Construtora Horizonte S.A.");
        TextOf(frozen).ShouldNotContain("Nome Trocado");
    }

    [Fact]
    public async Task Accepting_reuses_the_person_with_the_same_document_and_adds_the_client_role()
    {
        var office = await OfficeAsync();
        var existing = await _api.CreateAsync("/api/v1/people", office.Seller, office.Organization, new { kind = "company", name = "Construtora Horizonte", document = "11222333000181", roles = WitnessRole });
        var opportunity = await OpportunityAsync(office, name: "Horizonte (novo nome)");
        var proposal = await ProposalAsync(office, opportunity);
        await SentAsync(office, proposal);
        var (_, check) = await SendAsync(office, HttpMethod.Post, $"/api/v1/opportunities/{opportunity}/conflict-check");
        await SendAsync(office, HttpMethod.Post, $"/api/v1/conflict-checks/{check.GetProperty("id").GetGuid()}/proceed", new { justification = "Sem conflito." });

        var (status, accepted) = await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{proposal}/accept");

        status.ShouldBe(HttpStatusCode.OK, accepted.ToString());
        accepted.GetProperty("clientPersonId").GetGuid().ShouldBe(existing);
        var (_, person) = await SendAsync(office, HttpMethod.Get, $"/api/v1/people/{existing}");
        person.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["client", "witness"]);
    }

    [Fact]
    public async Task Accepting_one_proposal_supersedes_the_other_open_ones()
    {
        var office = await OfficeAsync();
        var existing = await _api.CreateAsync("/api/v1/people", office.Seller, office.Organization, new { kind = "company", name = "Cliente Cadastrado", roles = WitnessRole });
        var opportunity = await OpportunityAsync(office, personId: existing);
        var first = await ProposalAsync(office, opportunity);
        var second = await ProposalAsync(office, opportunity);
        await SentAsync(office, first);
        var (_, check) = await SendAsync(office, HttpMethod.Post, $"/api/v1/opportunities/{opportunity}/conflict-check");
        await SendAsync(office, HttpMethod.Post, $"/api/v1/conflict-checks/{check.GetProperty("id").GetGuid()}/proceed", new { justification = "Sem conflito." });

        (await SendAsync(office, HttpMethod.Post, $"/api/v1/proposals/{first}/accept")).Status.ShouldBe(HttpStatusCode.OK);
        var (_, other) = await SendAsync(office, HttpMethod.Get, $"/api/v1/proposals/{second}");
        var (_, list) = await SendAsync(office, HttpMethod.Get, $"/api/v1/proposals?opportunityId={opportunity}&status=Superseded");
        var (newStatus, newProposal) = await SendAsync(office, HttpMethod.Post, $"/api/v1/opportunities/{opportunity}/proposals", new { title = "Mais uma" });

        other.GetProperty("status").GetString().ShouldBe("superseded");
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldBe([second]);
        list.GetProperty("items")[0].GetProperty("prospectName").GetString().ShouldBe("Cliente Cadastrado");
        newStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(newProposal).ShouldBe("crm.opportunity_won");
    }

    [Fact]
    public async Task Losing_needs_a_loss_reason_and_the_kanban_counts_by_stage()
    {
        var office = await OfficeAsync();
        var lost = await OpportunityAsync(office);
        await OpportunityAsync(office, name: "Outro Interessado", document: null);

        var (wrongStatus, wrong) = await SendAsync(office, HttpMethod.Post, $"/api/v1/opportunities/{lost}/lose", new { reasonId = office.Origin, note = "x" });
        var (lostStatus, lostBody) = await SendAsync(office, HttpMethod.Post, $"/api/v1/opportunities/{lost}/lose", new { reasonId = office.LossReason, note = "Preço" });
        var (_, page) = await SendAsync(office, HttpMethod.Get, "/api/v1/opportunities?stage=lost");
        var (_, summary) = await SendAsync(office, HttpMethod.Get, "/api/v1/opportunities/summary");
        var (moveStatus, move) = await SendAsync(office, HttpMethod.Post, $"/api/v1/opportunities/{lost}/stage", new { stage = "won" });

        wrongStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(wrong).ShouldBe("crm.loss_reason_invalid");
        lostStatus.ShouldBe(HttpStatusCode.OK);
        lostBody.GetProperty("lossNote").GetString().ShouldBe("Preço");
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldBe([lost]);
        var byStage = summary.EnumerateArray().ToDictionary(s => s.GetProperty("stage").GetString()!, s => (s.GetProperty("count").GetInt32(), s.GetProperty("estimatedValue").GetDecimal()), StringComparer.Ordinal);
        byStage["lead"].ShouldBe((1, 60_000m));
        byStage["lost"].ShouldBe((1, 60_000m));
        byStage["won"].ShouldBe((0, 0m));
        moveStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(move).ShouldBe("crm.opportunity_stage_invalid");
    }

    [Fact]
    public async Task An_opportunity_refuses_an_unknown_origin_or_a_responsible_from_outside_the_office()
    {
        var office = await OfficeAsync();

        var (originStatus, origin) = await SendAsync(office, HttpMethod.Post, "/api/v1/opportunities", new
        {
            title = "T",
            prospect = new { kind = "individual", name = "João" },
            originId = office.LossReason,
        });
        var (responsibleStatus, responsible) = await SendAsync(office, HttpMethod.Post, "/api/v1/opportunities", new
        {
            title = "T",
            prospect = new { kind = "individual", name = "João" },
            responsibleUserId = Guid.CreateVersion7(),
        });
        var (missingStatus, missing) = await SendAsync(office, HttpMethod.Post, "/api/v1/opportunities", new { title = "T" });

        originStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(origin).ShouldBe("crm.origin_invalid");
        responsibleStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(responsible).ShouldBe("crm.responsible_invalid");
        missingStatus.ShouldBe(HttpStatusCode.BadRequest);
        FieldCodes(missing).ShouldContain("crm.prospect_required");
    }

    [Fact]
    public async Task Merging_and_anonymizing_a_person_carry_over_to_the_linked_opportunities()
    {
        var office = await OfficeAsync();
        var admin = await TestOrganizations.AddMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), "crm.people.read", "crm.people.merge", "crm.people.anonymize");
        var keep = await _api.CreateAsync("/api/v1/people", office.Seller, office.Organization, new { kind = "company", name = "Horizonte", roles = WitnessRole });
        var duplicate = await _api.CreateAsync("/api/v1/people", office.Seller, office.Organization, new { kind = "company", name = "Horizonte Duplicada", document = "11222333000181", roles = WitnessRole });
        var opportunity = await OpportunityAsync(office, personId: duplicate);

        (await _api.SendAsync(HttpMethod.Post, $"/api/v1/people/{keep}/merge", admin.UserId, office.Organization, new { duplicateId = duplicate })).Status.ShouldBe(HttpStatusCode.OK);
        var (_, merged) = await SendAsync(office, HttpMethod.Get, $"/api/v1/opportunities/{opportunity}");
        (await _api.SendAsync(HttpMethod.Post, $"/api/v1/people/{keep}/anonymize", admin.UserId, office.Organization, new { reason = "Pedido do titular" })).Status.ShouldBe(HttpStatusCode.OK);
        var (_, anonymized) = await SendAsync(office, HttpMethod.Get, $"/api/v1/opportunities/{opportunity}");

        merged.GetProperty("personId").GetGuid().ShouldBe(keep);
        anonymized.GetProperty("prospect").GetProperty("name").GetString()!.ShouldStartWith("Pessoa anonimizada");
        anonymized.GetProperty("prospect").GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
        anonymized.GetProperty("prospect").GetProperty("email").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task An_office_template_is_scanned_checked_and_used_by_the_proposal()
    {
        var office = await OfficeAsync();
        var (_, _, model) = await DownloadAsync(office, "/api/v1/proposal-templates/default");
        using (var stream = new MemoryStream())
        {
            stream.Write(model);
            using (var document = WordprocessingDocument.Open(stream, true))
            {
                var body = document.MainDocumentPart!.Document!.Body!;
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Modelo do Escritório Silva"))));
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Oportunidade: {{oportunidade.titulo}}"))));
                document.MainDocumentPart.Document.Save();
            }

            model = stream.ToArray();
        }

        var (status, uploaded) = await UploadAsync(office, "Modelo Silva", model);
        var (_, templates) = await SendAsync(office, HttpMethod.Get, "/api/v1/proposal-templates");
        var opportunity = await OpportunityAsync(office);
        var proposal = await ProposalAsync(office, opportunity, uploaded.GetProperty("id").GetGuid());
        var (_, _, rendered) = await DownloadAsync(office, $"/api/v1/proposals/{proposal}/document");

        status.ShouldBe(HttpStatusCode.Created, uploaded.ToString());
        templates.GetProperty("templates").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ShouldBe(["Modelo Silva"]);
        templates.GetProperty("markers").EnumerateArray().Select(m => m.GetString()).ShouldContain("cliente.nome");
        TextOf(rendered).ShouldContain("Modelo do Escritório Silva");
        TextOf(rendered).ShouldContain("Oportunidade: Contencioso trabalhista");
    }

    [Fact]
    public async Task Infected_invalid_or_unknown_marker_templates_are_refused()
    {
        var office = await OfficeAsync();
        var (_, _, model) = await DownloadAsync(office, "/api/v1/proposal-templates/default");
        using var stream = new MemoryStream();
        stream.Write(model);
        using (var document = WordprocessingDocument.Open(stream, true))
        {
            document.MainDocumentPart!.Document!.Body!.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("{{processo.numero}}"))));
            document.MainDocumentPart.Document.Save();
        }

        var (unknownStatus, unknown) = await UploadAsync(office, "Com marcador errado", stream.ToArray());
        var (infectedStatus, infected) = await UploadAsync(office, "Vírus", "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"u8.ToArray());
        var (invalidStatus, invalid) = await UploadAsync(office, "Texto", "não sou um docx"u8.ToArray());
        var (extensionStatus, extension) = await UploadAsync(office, "Planilha", model, "modelo.xlsx");

        unknownStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(unknown).ShouldBe("crm.template_unknown_marker");
        unknown.GetProperty("title").GetString()!.ShouldContain("processo.numero");
        infectedStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(infected).ShouldBe("crm.template_infected");
        invalidStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(invalid).ShouldBe("crm.template_invalid");
        extensionStatus.ShouldBe(HttpStatusCode.BadRequest);
        FieldCodes(extension).ShouldBe(["crm.template_invalid"]);
    }
}
