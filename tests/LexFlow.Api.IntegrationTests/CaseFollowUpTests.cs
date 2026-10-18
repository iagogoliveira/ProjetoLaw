using System.Net;
using System.Text.Json;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Risco, acordos e audiências do processo (Etapa 5, PR 5C2) pela API real, com as decisões de 2026-10-18: avaliar
/// risco pede <c>cases.risk.assess</c>; os termos finais do acordo vão à aprovação (<c>cases.settlements.approve</c>,
/// inclusive para quem os enviou) antes da homologação; a redesignação mantém a audiência e guarda a data anterior.
/// </summary>
public sealed class CaseFollowUpTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly string[] Everything =
    [
        "cases.read", "cases.create", "cases.edit", "cases.risk.assess", "cases.settlements.approve", "crm.people.create", "settings.custom_fields.read",
    ];

    private static readonly string[] Editor = ["cases.read", "cases.edit"];
    private static readonly string[] ClientRole = ["client"];
    private static readonly string[] WitnessRole = ["witness"];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record Office(Guid Organization, Guid User, Guid Case, Guid ClaimType, Guid Witness);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(Office office, HttpMethod method, string path, object? body = null, Guid? user = null) =>
        _api.SendAsync(method, path, user ?? office.User, office.Organization, body);

    private async Task<Office> OfficeAsync()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, Everything);
        using (AmbientContext.Begin(member.OrganizationId, "follow-up-tests"))
        await using (var provider = dependencies.PlatformServices())
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LookupTableSeeder>().SeedAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().SaveChangesAsync(Ct);
        }

        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "company", name = "Indústria Beta S.A.", roles = ClientRole });
        var witness = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "individual", name = "José Testemunha", roles = WitnessRole });
        var caseId = await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = "extrajudicial",
            claimValue = 500_000m,
            parties = new object[] { new { personId = client, pole = "passive", isClient = true } },
            assignments = new object[] { new { role = "lawyer", userId = member.UserId } },
        });
        var (_, tables) = await _api.SendAsync(HttpMethod.Get, "/api/v1/lookup-tables", member.UserId, member.OrganizationId);
        var claimType = tables.EnumerateArray()
            .Single(t => t.GetProperty("systemKey").ValueKind == JsonValueKind.String && t.GetProperty("systemKey").GetString() == SystemLookupTables.Claims)
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        return new Office(member.OrganizationId, member.UserId, caseId, claimType, witness);
    }

    private Task<Guid> ClaimAsync(Office office, decimal requested) =>
        _api.CreateAsync($"/api/v1/cases/{office.Case}/claims", office.User, office.Organization, new
        {
            claimTypeId = office.ClaimType,
            valuations = new object[] { new { instance = "first", requested } },
        });

    [Fact]
    public async Task Risk_totals_come_from_the_claims_and_assessing_needs_its_own_permission()
    {
        var office = await OfficeAsync();
        var overtime = await ClaimAsync(office, 60_000m);
        var damages = await ClaimAsync(office, 40_000m);
        var editor = await TestOrganizations.AddScopedMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), DataScope.Organization, null, Editor);

        var (caseStatus, _) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/risk-assessments", new { probability = "possible", estimatedLoss = 90_000m });
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/risk-assessments", new { claimId = overtime, probability = "possible", estimatedLoss = 10_000m });
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/risk-assessments", new { claimId = overtime, probability = "probable", estimatedLoss = 25_000m, rationale = "Cartões de ponto contra nós" });
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/risk-assessments", new { claimId = damages, probability = "possible", estimatedLoss = 15_000m });
        var (forbiddenStatus, _) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/risk-assessments", new { probability = "remote" }, editor.UserId);
        var (otherClaimStatus, otherClaim) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/risk-assessments", new { claimId = Guid.CreateVersion7(), probability = "remote" });
        var (_, risk) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/risk", user: editor.UserId);
        var (_, history) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/risk/history");

        caseStatus.ShouldBe(HttpStatusCode.Created);
        forbiddenStatus.ShouldBe(HttpStatusCode.Forbidden);
        otherClaimStatus.ShouldBe(HttpStatusCode.NotFound);
        OrganizationApi.Code(otherClaim).ShouldBe("litigation.claim_not_found");
        risk.GetProperty("case").GetProperty("accounting").GetString().ShouldBe("contingentLiability");
        risk.GetProperty("claims").GetArrayLength().ShouldBe(2);
        risk.GetProperty("provision").GetDecimal().ShouldBe(25_000m);
        risk.GetProperty("contingentLiability").GetDecimal().ShouldBe(15_000m);
        history.GetArrayLength().ShouldBe(4);
        history[0].GetProperty("rationale").ValueKind.ShouldBe(JsonValueKind.Null);
        history.EnumerateArray().ShouldAllBe(h => h.GetProperty("assessedBy").GetGuid() == office.User);
    }

    [Fact]
    public async Task A_settlement_is_negotiated_approved_homologated_and_fulfilled()
    {
        var office = await OfficeAsync();
        await ClaimAsync(office, 100_000m);
        var editor = await TestOrganizations.AddScopedMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), DataScope.Organization, null, Editor);
        var basePath = $"/api/v1/cases/{office.Case}/settlements";

        var (startStatus, started) = await SendAsync(office, HttpMethod.Post, basePath,
            new { direction = "clientPays", firstOffer = new { party = "counterparty", amount = 90_000m, offeredOn = "2026-09-01" } });
        var id = started.GetProperty("id").GetGuid();
        await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/offers", new { party = "office", amount = 40_000m, offeredOn = "2026-09-10" }, editor.UserId);
        var (submitStatus, submitted) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/submit", new
        {
            agreedAmount = 60_000m,
            installments = new object[] { new { dueOn = "2026-11-10", amount = 30_000m }, new { dueOn = "2026-12-10", amount = 30_000m } },
        }, editor.UserId);
        var (editorApproveStatus, _) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/approve", user: editor.UserId);
        var (approveStatus, _) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/approve");
        var (homologateStatus, homologated) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/homologate", new { homologatedOn = "2026-10-05" });
        var installments = homologated.GetProperty("installments").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/installments/{installments[0]}/pay", new { paidOn = "2026-11-10", paidAmount = 30_000m });
        var (_, fulfilled) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/installments/{installments[1]}/pay", new { paidOn = "2026-12-10", paidAmount = 30_000m });
        var (_, list) = await SendAsync(office, HttpMethod.Get, basePath);

        startStatus.ShouldBe(HttpStatusCode.Created, started.ToString());
        started.GetProperty("difference").GetDecimal().ShouldBe(10_000m); // pedido 100 mil × última proposta 90 mil
        submitStatus.ShouldBe(HttpStatusCode.OK, submitted.ToString());
        submitted.GetProperty("status").GetString().ShouldBe("awaitingApproval");
        submitted.GetProperty("submittedBy").GetGuid().ShouldBe(editor.UserId);
        editorApproveStatus.ShouldBe(HttpStatusCode.Forbidden);
        approveStatus.ShouldBe(HttpStatusCode.OK);
        homologateStatus.ShouldBe(HttpStatusCode.OK, homologated.ToString());
        fulfilled.GetProperty("status").GetString().ShouldBe("fulfilled");
        fulfilled.GetProperty("requestedAmount").GetDecimal().ShouldBe(100_000m);
        fulfilled.GetProperty("difference").GetDecimal().ShouldBe(40_000m);
        fulfilled.GetProperty("differencePercent").GetDecimal().ShouldBe(40m);
        fulfilled.GetProperty("offers").GetArrayLength().ShouldBe(2);
        list.GetArrayLength().ShouldBe(1);

        var published = _factory.PublishedEvents.OfType<SettlementHomologatedIntegrationEvent>().Single(e => e.SettlementId == id);
        published.Direction.ShouldBe("ClientPays");
        published.AgreedAmount.ShouldBe(60_000m);
        published.Installments.Select(i => i.DueOn).ShouldBe([new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10)]);
    }

    [Fact]
    public async Task The_settlement_rules_are_enforced_by_the_api()
    {
        var office = await OfficeAsync();
        var basePath = $"/api/v1/cases/{office.Case}/settlements";
        var id = await _api.CreateAsync(basePath, office.User, office.Organization, new { direction = "clientReceives" });

        var (mismatchStatus, mismatch) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/submit",
            new { agreedAmount = 1_000m, installments = new object[] { new { dueOn = "2026-11-10", amount = 999m } } });
        var (earlyStatus, early) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/homologate", new { homologatedOn = "2026-10-05" });
        var (rejectStatus, rejected) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/reject", new { reason = "Sem proposta razoável" });
        var (lateStatus, late) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/offers", new { party = "office", amount = 1m, offeredOn = "2026-10-01" });
        var (missingStatus, _) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{Guid.CreateVersion7()}/cancel", new { reason = "x" });

        mismatchStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(mismatch).ShouldBe("litigation.settlement_installments_mismatch");
        earlyStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(early).ShouldBe("litigation.settlement_not_approved");
        rejectStatus.ShouldBe(HttpStatusCode.OK);
        rejected.GetProperty("status").GetString().ShouldBe("rejected");
        rejected.GetProperty("requestedAmount").GetDecimal().ShouldBe(500_000m); // sem pedidos, o valor da causa
        lateStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(late).ShouldBe("litigation.settlement_not_negotiating");
        missingStatus.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_rescheduled_hearing_keeps_its_history_until_it_is_held()
    {
        var office = await OfficeAsync();
        var basePath = $"/api/v1/cases/{office.Case}/hearings";
        object Hearing(Guid? lawyer = null, IReadOnlyList<Guid>? witnesses = null) => new
        {
            type = "instruction",
            format = "virtual",
            scheduledAt = "2026-11-10T14:00:00-03:00",
            meetingLink = "https://meet.example.com/abc",
            lawyerUserId = lawyer ?? office.User,
            representative = "Maria Preposta",
            witnessPersonIds = witnesses ?? [office.Witness],
        };

        var (createdStatus, created) = await SendAsync(office, HttpMethod.Post, basePath, Hearing());
        var id = created.GetProperty("id").GetGuid();
        var (badLawyerStatus, badLawyer) = await SendAsync(office, HttpMethod.Post, basePath, Hearing(lawyer: Guid.CreateVersion7()));
        var (badWitnessStatus, badWitness) = await SendAsync(office, HttpMethod.Post, basePath, Hearing(witnesses: [Guid.CreateVersion7()]));
        var (rescheduleStatus, rescheduled) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/reschedule",
            new { newDate = "2026-11-24T10:00:00-03:00", reason = "Pedido do juízo" });
        var (holdStatus, held) = await SendAsync(office, HttpMethod.Post, $"{basePath}/{id}/hold", new { text = "Instrução encerrada; razões finais em 10 dias" });
        var (editStatus, edit) = await SendAsync(office, HttpMethod.Put, $"{basePath}/{id}", Hearing());
        var (_, list) = await SendAsync(office, HttpMethod.Get, basePath);

        createdStatus.ShouldBe(HttpStatusCode.Created, created.ToString());
        created.GetProperty("witnessPersonIds")[0].GetGuid().ShouldBe(office.Witness);
        badLawyerStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(badLawyer).ShouldBe("litigation.responsible_invalid");
        badWitnessStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(badWitness).ShouldBe("litigation.person_not_found");
        rescheduleStatus.ShouldBe(HttpStatusCode.OK, rescheduled.ToString());
        rescheduled.GetProperty("scheduledAt").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.FromHours(-3)));
        var change = rescheduled.GetProperty("reschedules")[0];
        change.GetProperty("previousDate").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.FromHours(-3)));
        change.GetProperty("reason").GetString().ShouldBe("Pedido do juízo");
        holdStatus.ShouldBe(HttpStatusCode.OK);
        held.GetProperty("status").GetString().ShouldBe("held");
        editStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(edit).ShouldBe("litigation.hearing_closed");
        list.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Risk_settlements_and_hearings_follow_the_reach_of_the_case()
    {
        var office = await OfficeAsync();
        var outsider = await TestOrganizations.AddScopedMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), DataScope.Own, null, Everything);

        foreach (var path in new[] { "risk", "risk/history", "settlements", "hearings" })
        {
            var (status, _) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/{path}", user: outsider.UserId);
            status.ShouldBe(HttpStatusCode.NotFound, path);
        }

        var (assessStatus, _) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/risk-assessments", new { probability = "remote" }, outsider.UserId);
        assessStatus.ShouldBe(HttpStatusCode.NotFound);
    }
}
