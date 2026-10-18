using System.Net;
using System.Text.Json;
using LexFlow.Organization.Domain.Roles;
using Npgsql;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Escopo de dados dos processos (seção 7.2) e casos sigilosos (seção 6.3; decisões de 2026-10-18) pela API real:
/// cada usuário vê e altera só os processos ao alcance do escopo da permissão; nos sigilosos, bloqueado nunca vê,
/// permitido vê (a lista basta) e quem gerencia restrições vê, salvo bloqueio. Negar um sigiloso vai para a auditoria.
/// </summary>
public sealed class CaseAccessTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly string[] Reader = ["cases.read", "cases.edit"];

    private static readonly string[] Owner =
    [
        "cases.read", "cases.create", "cases.edit", "cases.delete", "cases.restrictions.manage", "crm.people.read", "crm.people.create",
        "settings.structure.manage", "settings.users.read",
    ];

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

    /// <summary>
    /// Escritório com: área Trabalhista, filial SP, equipe T (com o advogado X), e processos A (área, responsável X),
    /// B (filial), C (equipe T responsável) e D (nada além do dono).
    /// </summary>
    private sealed record Office(
        SeededMember Owner,
        SeededMember Lawyer,
        Guid Area,
        Guid Branch,
        Guid Team,
        Guid Client,
        Guid CaseA,
        Guid CaseB,
        Guid CaseC,
        Guid CaseD);

    private async Task<Office> OfficeAsync()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(dependencies, Owner);
        var org = owner.OrganizationId;
        var lawyer = await TestOrganizations.AddScopedMemberAsync(dependencies, org, Guid.CreateVersion7(), DataScope.Own, null, Reader);
        var area = await _api.CreateAsync("/api/v1/organization/practice-areas", owner.UserId, org, new { name = $"Trabalhista {Guid.CreateVersion7():N}" });
        var branch = await _api.CreateAsync("/api/v1/organization/branches", owner.UserId, org, new { name = $"SP {Guid.CreateVersion7():N}" });
        var team = await _api.CreateAsync("/api/v1/organization/teams", owner.UserId, org, new { name = $"Equipe {Guid.CreateVersion7():N}" });
        (await _api.SendAsync(HttpMethod.Put, $"/api/v1/organization/teams/{team}/members", owner.UserId, org, new { memberIds = new[] { lawyer.MemberId } }))
            .Status.ShouldBe(HttpStatusCode.OK);
        var client = await _api.CreateAsync("/api/v1/people", owner.UserId, org, new { kind = "company", name = "Cliente", roles = ClientRole });

        Task<Guid> CaseAsync(Guid? practiceAreaId = null, Guid? branchId = null, object? assignment = null) =>
            _api.CreateAsync("/api/v1/cases", owner.UserId, org, new
            {
                kind = "extrajudicial",
                practiceAreaId,
                branchId,
                parties = new[] { new { personId = client, pole = "active", isClient = true } },
                assignments = assignment is null ? Array.Empty<object>() : [assignment],
            });

        return new Office(
            owner,
            lawyer,
            area,
            branch,
            team,
            client,
            await CaseAsync(practiceAreaId: area, assignment: new { role = "lawyer", userId = lawyer.UserId }),
            await CaseAsync(branchId: branch),
            await CaseAsync(assignment: new { role = "team", teamId = team }),
            await CaseAsync());
    }

    private async Task<SeededMember> MemberAsync(Office office, DataScope scope, Guid? reference, params string[] permissions) =>
        await TestOrganizations.AddScopedMemberAsync(dependencies, office.Owner.OrganizationId, Guid.CreateVersion7(), scope, reference, permissions.Length == 0 ? Reader : permissions);

    private async Task<HashSet<Guid>> VisibleAsync(Office office, SeededMember member, string query = "")
    {
        var (status, page) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/cases{query}", member.UserId, office.Owner.OrganizationId);
        status.ShouldBe(HttpStatusCode.OK, page.ToString());
        return [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

    private async Task<HttpStatusCode> GetAsync(Office office, SeededMember member, Guid caseId) =>
        (await _api.SendAsync(HttpMethod.Get, $"/api/v1/cases/{caseId}", member.UserId, office.Owner.OrganizationId)).Status;

    private Task<(HttpStatusCode Status, JsonElement Body)> RestrictAsync(Office office, Guid caseId, object allowed, object blocked) =>
        _api.SendAsync(HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions", office.Owner.UserId, office.Owner.OrganizationId, new { allowed, blocked });

    [Fact]
    public async Task Each_scope_reaches_only_its_cases()
    {
        var office = await OfficeAsync();
        var areaReader = await MemberAsync(office, DataScope.PracticeArea, office.Area);
        var branchReader = await MemberAsync(office, DataScope.Branch, office.Branch);
        var teamReader = await MemberAsync(office, DataScope.Team, office.Team);
        var all = new HashSet<Guid> { office.CaseA, office.CaseB, office.CaseC, office.CaseD };

        (await VisibleAsync(office, office.Lawyer)).ShouldBe([office.CaseA], ignoreOrder: true);
        (await VisibleAsync(office, areaReader)).ShouldBe([office.CaseA], ignoreOrder: true);
        (await VisibleAsync(office, branchReader)).ShouldBe([office.CaseB], ignoreOrder: true);
        (await VisibleAsync(office, teamReader)).ShouldBe([office.CaseA, office.CaseC], ignoreOrder: true, "a equipe é responsável por C, e um membro dela, por A");
        (await VisibleAsync(office, office.Owner)).IsSupersetOf(all).ShouldBeTrue();

        (await GetAsync(office, office.Lawyer, office.CaseA)).ShouldBe(HttpStatusCode.OK);
        (await GetAsync(office, office.Lawyer, office.CaseD)).ShouldBe(HttpStatusCode.NotFound);
        (await _api.SendAsync(HttpMethod.Post, $"/api/v1/cases/{office.CaseD}/status", office.Lawyer.UserId, office.Owner.OrganizationId, new { status = "suspended" }))
            .Status.ShouldBe(HttpStatusCode.NotFound, "fora do escopo também não se altera");
        (await _api.SendAsync(HttpMethod.Post, $"/api/v1/cases/{office.CaseA}/status", office.Lawyer.UserId, office.Owner.OrganizationId, new { status = "suspended" }))
            .Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_blocked_user_never_sees_a_restricted_case_and_the_denial_is_audited()
    {
        var office = await OfficeAsync();
        var partner = await MemberAsync(office, DataScope.Organization, null);
        var (status, restrictions) = await RestrictAsync(office, office.CaseD, Array.Empty<object>(), new[] { new { userId = partner.UserId } });

        status.ShouldBe(HttpStatusCode.OK, restrictions.ToString());
        restrictions.GetProperty("blocked")[0].GetProperty("userId").GetGuid().ShouldBe(partner.UserId);
        (await VisibleAsync(office, partner)).ShouldNotContain(office.CaseD);
        (await GetAsync(office, partner, office.CaseD)).ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync(office, partner, office.CaseB)).ShouldBe(HttpStatusCode.OK, "o escopo de escritório continua valendo nos outros");

        await using var connection = new NpgsqlConnection(dependencies.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM audit.audit_logs WHERE action = 'AccessDenied' AND entity_type = 'LegalCase' AND entity_id = $1 AND user_id = $2", connection);
        command.Parameters.Add(new NpgsqlParameter { Value = office.CaseD.ToString() });
        command.Parameters.Add(new NpgsqlParameter { Value = partner.UserId });
        ((long)(await command.ExecuteScalarAsync(Ct))!).ShouldBe(1);
    }

    [Fact]
    public async Task With_allowed_people_only_they_and_restriction_managers_see_the_case()
    {
        var office = await OfficeAsync();
        var partner = await MemberAsync(office, DataScope.Organization, null);
        var specialist = await MemberAsync(office, DataScope.Own, null);
        var manager = await MemberAsync(office, DataScope.Own, null, "cases.read", "cases.restrictions.manage");
        var blockedManager = await MemberAsync(office, DataScope.Organization, null, "cases.read", "cases.restrictions.manage");

        (await RestrictAsync(office, office.CaseA, new[] { new { userId = specialist.UserId } }, new[] { new { userId = blockedManager.UserId } }))
            .Status.ShouldBe(HttpStatusCode.OK);

        (await GetAsync(office, specialist, office.CaseA)).ShouldBe(HttpStatusCode.OK, "a lista basta, mesmo fora do escopo");
        (await GetAsync(office, partner, office.CaseA)).ShouldBe(HttpStatusCode.NotFound, "o escopo de escritório não basta num sigiloso");
        (await GetAsync(office, office.Lawyer, office.CaseA)).ShouldBe(HttpStatusCode.NotFound, "ser responsável também não");
        (await GetAsync(office, manager, office.CaseA)).ShouldBe(HttpStatusCode.OK);
        (await GetAsync(office, blockedManager, office.CaseA)).ShouldBe(HttpStatusCode.NotFound);
        (await VisibleAsync(office, specialist)).ShouldBe([office.CaseA], ignoreOrder: true);
        var (_, restricted) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/cases/{office.CaseA}", specialist.UserId, office.Owner.OrganizationId);
        restricted.GetProperty("isRestricted").GetBoolean().ShouldBeTrue();

        // Listas vazias tiram o sigilo.
        (await RestrictAsync(office, office.CaseA, Array.Empty<object>(), Array.Empty<object>())).Status.ShouldBe(HttpStatusCode.OK);
        (await GetAsync(office, partner, office.CaseA)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_team_in_the_lists_counts_for_every_member()
    {
        var office = await OfficeAsync();
        var partner = await MemberAsync(office, DataScope.Organization, null);

        (await RestrictAsync(office, office.CaseB, Array.Empty<object>(), new[] { new { teamId = office.Team } })).Status.ShouldBe(HttpStatusCode.OK);
        (await RestrictAsync(office, office.CaseD, new[] { new { teamId = office.Team } }, Array.Empty<object>())).Status.ShouldBe(HttpStatusCode.OK);

        (await GetAsync(office, office.Lawyer, office.CaseD)).ShouldBe(HttpStatusCode.OK, "o advogado é da equipe permitida");
        (await GetAsync(office, partner, office.CaseD)).ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync(office, partner, office.CaseB)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Restrictions_accept_only_members_and_teams_of_the_office()
    {
        var office = await OfficeAsync();
        var stranger = await TestOrganizations.CreateWithMemberAsync(dependencies, Reader);

        var (memberStatus, member) = await RestrictAsync(office, office.CaseA, new[] { new { userId = stranger.UserId } }, Array.Empty<object>());
        var (teamStatus, team) = await RestrictAsync(office, office.CaseA, new[] { new { teamId = Guid.CreateVersion7() } }, Array.Empty<object>());
        var (twiceStatus, twice) = await RestrictAsync(office, office.CaseA, new[] { new { userId = office.Lawyer.UserId } }, new[] { new { userId = office.Lawyer.UserId } });
        var (lawyerStatus, _) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/cases/{office.CaseA}/restrictions", office.Lawyer.UserId, office.Owner.OrganizationId, new { allowed = Array.Empty<object>() });

        memberStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(member).ShouldBe("litigation.responsible_invalid");
        teamStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(team).ShouldBe("litigation.team_invalid");
        twiceStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(twice).ShouldBe("litigation.restriction_duplicated");
        lawyerStatus.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_list_filters_by_responsible_area_kind_and_party()
    {
        var office = await OfficeAsync();
        var org = office.Owner.OrganizationId;
        var other = await _api.CreateAsync("/api/v1/people", office.Owner.UserId, org, new { kind = "company", name = "Outro cliente", roles = ClientRole });
        var judicial = await _api.CreateAsync("/api/v1/cases", office.Owner.UserId, org, new
        {
            kind = "judicial",
            cnjNumber = "2238058-98.2025.8.26.8434",
            practiceAreaId = office.Area,
            parties = new[] { new { personId = other, pole = "active", isClient = true } },
        });

        (await VisibleAsync(office, office.Owner, $"?responsibleId={office.Lawyer.UserId}")).ShouldBe([office.CaseA], ignoreOrder: true);
        (await VisibleAsync(office, office.Owner, $"?practiceAreaId={office.Area}")).ShouldBe([office.CaseA, judicial], ignoreOrder: true);
        (await VisibleAsync(office, office.Owner, $"?practiceAreaId={office.Area}&responsibleId={office.Lawyer.UserId}")).ShouldBe([office.CaseA], ignoreOrder: true);
        (await VisibleAsync(office, office.Owner, "?kind=Judicial")).ShouldBe([judicial], ignoreOrder: true);
        (await VisibleAsync(office, office.Owner, $"?teamId={office.Team}")).ShouldBe([office.CaseC], ignoreOrder: true);
        (await VisibleAsync(office, office.Owner, $"?personId={other}")).ShouldBe([judicial], ignoreOrder: true);
        (await VisibleAsync(office, office.Owner, $"?branchId={office.Branch}")).ShouldBe([office.CaseB], ignoreOrder: true);
        var (badStatus, bad) = await _api.SendAsync(HttpMethod.Get, "/api/v1/cases?kind=nenhum", office.Owner.UserId, org);
        badStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(bad).ShouldBe("litigation.filter_invalid");
    }
}
