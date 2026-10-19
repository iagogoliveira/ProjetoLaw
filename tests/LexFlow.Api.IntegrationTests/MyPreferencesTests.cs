using System.Net;
using System.Text.Json;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Preferências de tela do usuário (Etapa 5, PR 5E1; decisão de 2026-10-18: no servidor, por usuário).</summary>
public sealed class MyPreferencesTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly string[] Profile = ["identity.profile.read", "identity.profile.manage"];
    private static readonly string[] OnlyFolder = ["folder"];

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

    [Fact]
    public async Task Each_user_keeps_their_own_preferences_per_office()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(dependencies, Profile);
        var colleague = await TestOrganizations.AddMemberAsync(dependencies, owner.OrganizationId, Guid.CreateVersion7(), Profile);
        var other = await TestOrganizations.CreateWithMemberAsync(dependencies, Profile);
        var preference = new
        {
            key = "cases.list",
            value = new { columns = new[] { "folder", "cnj", "status" }, filters = new[] { new { name = "Trabalhistas", status = "active" } } },
        };

        var (emptyStatus, empty) = await _api.SendAsync(HttpMethod.Get, "/api/v1/me/preferences?key=cases.list", owner.UserId, owner.OrganizationId);
        var (saveStatus, saved) = await _api.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", owner.UserId, owner.OrganizationId, preference);
        await _api.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", owner.UserId, owner.OrganizationId,
            new { key = "cases.list", value = new { columns = OnlyFolder } });
        var (_, read) = await _api.SendAsync(HttpMethod.Get, "/api/v1/me/preferences?key=cases.list", owner.UserId, owner.OrganizationId);
        var (colleagueStatus, _) = await _api.SendAsync(HttpMethod.Get, "/api/v1/me/preferences?key=cases.list", colleague.UserId, owner.OrganizationId);
        var (otherStatus, _) = await _api.SendAsync(HttpMethod.Get, "/api/v1/me/preferences?key=cases.list", other.UserId, other.OrganizationId);

        emptyStatus.ShouldBe(HttpStatusCode.NotFound);
        OrganizationApi.Code(empty).ShouldBe("platform.preference_not_found");
        saveStatus.ShouldBe(HttpStatusCode.OK, saved.ToString());
        saved.GetProperty("value").GetProperty("filters")[0].GetProperty("name").GetString().ShouldBe("Trabalhistas");
        read.GetProperty("value").GetProperty("columns").EnumerateArray().Select(c => c.GetString()).ShouldBe(["folder"]);
        read.GetProperty("value").TryGetProperty("filters", out _).ShouldBeFalse();
        colleagueStatus.ShouldBe(HttpStatusCode.NotFound);
        otherStatus.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Keys_and_values_are_checked()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(dependencies, Profile);

        var (badKeyStatus, badKey) = await _api.SendAsync(HttpMethod.Get, "/api/v1/me/preferences?key=Cases%20List", owner.UserId, owner.OrganizationId);
        var (badPutKeyStatus, badPutKey) = await _api.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", owner.UserId, owner.OrganizationId, new { key = "../x", value = new { } });
        var (scalarStatus, scalar) = await _api.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", owner.UserId, owner.OrganizationId, new { key = "cases.list", value = 42 });
        var (bigStatus, big) = await _api.SendAsync(HttpMethod.Put, "/api/v1/me/preferences", owner.UserId, owner.OrganizationId,
            new { key = "cases.list", value = new { text = new string('x', 17_000) } });
        var (_, stillEmpty) = await _api.SendAsync(HttpMethod.Get, "/api/v1/me/preferences?key=cases.list", owner.UserId, owner.OrganizationId);

        badKeyStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(badKey).ShouldBe("platform.preference_key_invalid");
        badPutKeyStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(badPutKey).ShouldBe("platform.preference_key_invalid");
        scalarStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(scalar).ShouldBe("platform.preference_value_invalid");
        bigStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(big).ShouldBe("platform.preference_value_invalid");
        OrganizationApi.Code(stillEmpty).ShouldBe("platform.preference_not_found");
        JsonSerializer.Serialize(stillEmpty).ShouldNotBeNullOrEmpty();
    }
}
