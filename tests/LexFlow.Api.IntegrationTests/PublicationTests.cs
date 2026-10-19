using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Publications.Application;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class PublicationTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] Permissions =
        ["settings.integrations.manage", "publications.read", "cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create"];
    private static readonly string[] Reader = ["publications.read", "cases.read", "cases.edit"];
    private static readonly string[] ClientRole = ["client"];
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

    private Task<SeededMember> OfficeAsync() => TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private Task<Guid> SourceAsync(SeededMember member) =>
        _api.CreateAsync("/api/v1/publication-sources", member.UserId, member.OrganizationId, new { kind = "oab", label = "Advogada", value = "SP 123456" });

    private async Task<Guid> CaseAsync(SeededMember member, string? cnj = null, Guid? lawyer = null)
    {
        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId,
            new { kind = "company", name = "Cliente", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = cnj is null ? "extrajudicial" : "judicial",
            cnjNumber = cnj,
            instance = "first",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = lawyer ?? member.UserId } },
        });
    }

    private async Task<Guid> ReceiveAsync(SeededMember member, Guid source, string text)
    {
        using var ambient = AmbientContext.Begin(member.OrganizationId, "test-capture");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(member.OrganizationId);
        var day = new DateOnly(2026, 10, 19);
        var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReceivePublicationCommand, Guid>>()
            .Handle(new ReceivePublicationCommand(new(source, "DJE", day, day, text)), Ct);
        result.IsSuccess.ShouldBeTrue(result.Error?.Code);
        return result.Value;
    }

    [Fact]
    public async Task Sources_can_be_created_read_updated_and_disabled()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        var (_, detail) = await SendAsync(office, HttpMethod.Get, $"/api/v1/publication-sources/{source}");
        detail.GetProperty("value").GetString().ShouldBe("123456/SP");
        var updated = await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}",
            new { kind = "name", label = "Nome", value = "Maria", isActive = false });
        updated.Status.ShouldBe(HttpStatusCode.OK);
        updated.Body.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        var (_, list) = await SendAsync(office, HttpMethod.Get, "/api/v1/publication-sources");
        list.GetArrayLength().ShouldBe(1);
        (await SendAsync(office, HttpMethod.Post, "/api/v1/publication-sources", new { kind = "cnpj", label = "Inválido", value = "111" }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/publication-sources/{Guid.CreateVersion7()}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}",
            new { kind = "oab", label = "Inválido", value = "XX 0" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{Guid.CreateVersion7()}",
            new { kind = "name", label = "Nome", value = "Maria" })).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Intake_links_cnj_keeps_unlinked_items_and_deduplicates_normalized_text()
    {
        var office = await OfficeAsync();
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj);
        var source = await SourceAsync(office);
        var linked = await ReceiveAsync(office, source, "Intimação " + cnj);
        (await ReceiveAsync(office, source, " INTIMAÇÃO   " + cnj)).ShouldBe(linked);
        var unlinked = await ReceiveAsync(office, source, "Sem número CNJ");
        var ambiguous = await ReceiveAsync(office, source, cnj + " 6616920-53.2022.4.01.1974");
        var (_, detail) = await SendAsync(office, HttpMethod.Get, $"/api/v1/publications/{linked}");
        detail.GetProperty("legalCaseId").GetGuid().ShouldBe(caseId);
        detail.GetProperty("text").GetString().ShouldBe("Intimação " + cnj);
        foreach (var id in new[] { unlinked, ambiguous })
        {
            (await SendAsync(office, HttpMethod.Get, $"/api/v1/publications/{id}")).Body.GetProperty("legalCaseId").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        var (_, page) = await SendAsync(office, HttpMethod.Get, "/api/v1/publications?linked=false&status=pending&limit=1");
        page.GetProperty("items").GetArrayLength().ShouldBe(1);
        var cursor = page.GetProperty("nextCursor").GetString();
        cursor.ShouldNotBeNull();
        var (_, next) = await SendAsync(office, HttpMethod.Get, "/api/v1/publications?linked=false&status=pending&limit=1&cursor=" + cursor);
        next.GetProperty("items").GetArrayLength().ShouldBe(1);
        next.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldNotBe(page.GetProperty("items")[0].GetProperty("id").GetGuid());
        next.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        var (_, onlyCase) = await SendAsync(office, HttpMethod.Get, $"/api/v1/publications?caseId={caseId}&linked=true");
        onlyCase.GetProperty("items").GetArrayLength().ShouldBe(1);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/publications/{Guid.CreateVersion7()}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Scoped_publications_and_movements_follow_case_access_and_secrecy()
    {
        var office = await OfficeAsync();
        var reader = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null, Reader);
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj, reader.UserId);
        var otherCase = await CaseAsync(office);
        var source = await SourceAsync(office);
        var publication = await ReceiveAsync(office, source, "Texto " + cnj);
        await ReceiveAsync(office, source, "Não vinculada");
        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/publications/{publication}")).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/publications")).Body.GetProperty("items").GetArrayLength().ShouldBe(1);
        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/cases/{otherCase}/movements")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(reader, HttpMethod.Post, $"/api/v1/cases/{otherCase}/movements",
            new { occurredAt = "2026-10-19T10:00:00Z", description = "Não permitido" })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions",
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = reader.UserId } } })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/publications/{publication}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/publications")).Body.GetProperty("items").GetArrayLength().ShouldBe(0);
        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/cases/{caseId}/movements")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ambiguous_unlinked_publications_cannot_bypass_a_case_block()
    {
        var office = await OfficeAsync();
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj);
        var source = await SourceAsync(office);
        var linked = await ReceiveAsync(office, source, "Publicação " + cnj);
        var ambiguous = await ReceiveAsync(office, source, cnj + " e 6616920-53.2022.4.01.1974");
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), Reader);
        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/publications/{ambiguous}")).Status.ShouldBe(HttpStatusCode.OK);
        var (_, timeline) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/timeline");
        timeline.GetProperty("items").EnumerateArray().ShouldContain(e =>
            string.Equals(e.GetProperty("kind").GetString(), "publications.received", StringComparison.Ordinal)
            && e.GetProperty("referenceId").GetGuid() == linked);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions",
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = reader.UserId } } })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/publications?linked=false")).Body.GetProperty("items").GetArrayLength().ShouldBe(0);
        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/publications/{ambiguous}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Manual_movements_are_paginated_and_contribute_to_the_case_timeline()
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var movementIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            movementIds.Add(await _api.CreateAsync($"/api/v1/cases/{caseId}/movements", office.UserId, office.OrganizationId,
                new { occurredAt = "2026-10-19T10:00:00-03:00", description = "Sentença " + i }));
        }

        var (_, first) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/movements?limit=2");
        first.GetProperty("items").GetArrayLength().ShouldBe(2);
        first.GetProperty("items")[0].GetProperty("occurredAt").GetDateTimeOffset().Offset.ShouldBe(TimeSpan.Zero);
        var (_, second) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/movements?limit=2&cursor=" + first.GetProperty("nextCursor").GetString());
        second.GetProperty("items").GetArrayLength().ShouldBe(2);
        second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        var seen = new HashSet<Guid>();
        string? cursor = null;
        do
        {
            var (_, page) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/timeline?limit=2" + (cursor is null ? "" : "&cursor=" + cursor));
            foreach (var entry in page.GetProperty("items").EnumerateArray().Where(e => string.Equals(e.GetProperty("kind").GetString(), "publications.movement", StringComparison.Ordinal)))
            {
                seen.Add(entry.GetProperty("referenceId").GetGuid()).ShouldBeTrue();
            }
            cursor = page.GetProperty("nextCursor").GetString();
        } while (cursor is not null);
        seen.Order().ShouldBe(movementIds.Order());
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{caseId}/movements", new { occurredAt = "2026-10-19T10:00:00Z", description = " " }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/api/v1/publications?status=invalid")]
    [InlineData("/api/v1/publications?limit=0")]
    [InlineData("/api/v1/publications?cursor=invalid")]
    public async Task Invalid_publication_filters_return_a_stable_validation_error(string path)
    {
        var office = await OfficeAsync();
        var response = await SendAsync(office, HttpMethod.Get, path);
        response.Status.ShouldBe(HttpStatusCode.BadRequest);
        response.Body.GetProperty("code").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Invalid_movement_pagination_and_internal_intake_return_errors_without_writing()
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/movements?limit=201")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/movements?cursor=invalid")).Status.ShouldBe(HttpStatusCode.BadRequest);
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(office.OrganizationId);
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReceivePublicationCommand, Guid>>();
        var day = new DateOnly(2026, 10, 19);
        var invalid = await handler.Handle(new(new(Guid.Empty, "DJE", day, day, "Texto")), Ct);
        invalid.Error!.Code.ShouldBe("publications.invalid");
        var missing = await handler.Handle(new(new(Guid.CreateVersion7(), "DJE", day, day, "Texto")), Ct);
        missing.Error!.Code.ShouldBe("publications.source_not_found");
    }
}
