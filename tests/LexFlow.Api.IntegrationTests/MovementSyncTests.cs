using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Publications.Application.Movements;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Movements;
using LexFlow.Publications.Infrastructure.Persistence;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class MovementSyncTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] Permissions = ["settings.integrations.manage", "cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create"];
    private static readonly string[] Integrators = ["settings.integrations.manage"];
    private static readonly string[] Readers = ["cases.read"];
    private static readonly string[] ClientRole = ["client"];
    private const string Cnj = "2238058-98.2025.8.26.8434";
    private readonly SyncClock _clock = new();
    private readonly ScriptedProvider _provider = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;
    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services =>
        {
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton<IConfiguredMovementProvider>(_provider);
        });
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }
    public async ValueTask DisposeAsync() { _http.Dispose(); await _factory.DisposeAsync(); }
    private Task<SeededMember> OfficeAsync() => TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember office, HttpMethod method, string path, object? input = null) =>
        _api.SendAsync(method, path, office.UserId, office.OrganizationId, input);
    private async Task WithOfficeAsync(SeededMember office, Func<IServiceProvider, Task> work)
    {
        using var ambient = AmbientContext.Begin(office.OrganizationId, "movement-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office.OrganizationId);
        await work(scope.ServiceProvider);
    }
    private Task RunAsync(SeededMember office, Guid id) => WithOfficeAsync(office, sp => sp.GetRequiredService<MovementSyncJob>().RunAsync(id));
    private async Task<Guid> CaseAsync(SeededMember office, string? cnj = Cnj)
    {
        var person = await _api.CreateAsync("/api/v1/people", office.UserId, office.OrganizationId, new { kind = "company", name = "Cliente", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", office.UserId, office.OrganizationId, new
        {
            kind = cnj is null ? "extrajudicial" : "judicial",
            cnjNumber = cnj,
            instance = "first",
            parties = new[] { new { personId = person, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = office.UserId } },
        });
    }
    [Fact]
    public async Task Credentials_are_protected_scoped_preserved_replaced_and_removed_without_echo()
    {
        var office = await OfficeAsync();
        string? protectedKey = null;
        (await SendAsync(office, HttpMethod.Get, "/api/v1/movement-integration")).Body.GetProperty("provider").GetString().ShouldBe("disabled");
        var result = await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "dataJud", apiKey = "first-test-key" });
        result.Status.ShouldBe(HttpStatusCode.OK);
        result.Body.GetProperty("hasApiKey").GetBoolean().ShouldBeTrue();
        result.Body.ToString().ShouldNotContain("first-test-key");
        await WithOfficeAsync(office, async sp =>
        {
            var value = await sp.GetRequiredService<PublicationsDbContext>().MovementIntegrations.SingleAsync(Ct);
            value.ProtectedApiKey!.ShouldNotContain("first-test-key");
            protectedKey = value.ProtectedApiKey;
            sp.GetRequiredService<IMovementSecrets>().Unprotect(value.ProtectedApiKey!).Value.ShouldBe("first-test-key");
            var changes = await sp.GetRequiredService<PlatformDbContext>().Set<AuditLogEntry>().Where(a => a.OrganizationId == office.OrganizationId
                && a.EntityType == nameof(MovementIntegration)).Select(a => a.Changes).ToListAsync(Ct);
            changes.ShouldNotBeEmpty();
            string.Join(' ', changes).ShouldNotContain("ProtectedApiKey");
            string.Join(' ', changes).ShouldNotContain("first-test-key");
        });
        (await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "dataJud" })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "dataJud", apiKey = "replacement-key" })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "dataJud", clearApiKey = true })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "fake", clearApiKey = true })).Body.GetProperty("hasApiKey").GetBoolean().ShouldBeFalse();
        (await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "dataJud", apiKey = "bad key" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var other = await OfficeAsync();
        await WithOfficeAsync(other, sp =>
        {
            sp.GetRequiredService<IMovementSecrets>().Unprotect(protectedKey!).Error!.Code.ShouldBe("publications.credential_unreadable");
            return Task.CompletedTask;
        });
        (await SendAsync(other, HttpMethod.Get, "/api/v1/movement-integration")).Body.GetProperty("hasApiKey").GetBoolean().ShouldBeFalse();
        var scoped = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null, Integrators);
        (await SendAsync(scoped, HttpMethod.Get, "/api/v1/movement-integration")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(scoped, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "fake" })).Status.ShouldBe(HttpStatusCode.Forbidden);
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), Readers);
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/movement-integration")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task Pages_resume_after_failure_and_later_scans_add_only_new_historical_movements()
    {
        var office = await OfficeAsync();
        var id = await CaseAsync(office);
        await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "fake" });
        var old = new CapturedMovement("old", _clock.UtcNow.AddYears(-6), "Distribuição antiga");
        _provider.Result = new MovementPage("tjsp", [old], "checkpoint");
        await RunAsync(office, id);
        await RunAsync(office, id);
        _provider.Calls.ShouldBe(1);
        _clock.Advance();
        _provider.Result = Error.BusinessRule("publications.provider_unavailable", "Falha simulada");
        await RunAsync(office, id);
        var failed = (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movement-sync")).Body;
        failed.GetProperty("hasPendingPage").GetBoolean().ShouldBeTrue();
        failed.GetProperty("lastSuccessAt").ValueKind.ShouldBe(JsonValueKind.Null);
        failed.GetProperty("errorCode").GetString().ShouldBe("publications.provider_unavailable");
        _clock.Advance();
        _provider.Result = new MovementPage("tjsp", [old, new("second", old.OccurredAt.AddDays(1), "Segundo")], null);
        await RunAsync(office, id);
        _provider.Cursor.ShouldBe("checkpoint");
        _clock.UtcNow = _clock.UtcNow.AddHours(6);
        _provider.Result = new MovementPage("tjsp", [old, new("late", old.OccurredAt.AddDays(2), "Histórico publicado depois")], null);
        await RunAsync(office, id);
        _provider.Cursor.ShouldBeNull();
        var list = (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movements")).Body.GetProperty("items");
        list.GetArrayLength().ShouldBe(3);
        list.EnumerateArray().All(m => m.GetProperty("occurredAt").GetDateTimeOffset().Year == 2020).ShouldBeTrue();
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movement-sync")).Body.GetProperty("lastSuccessAt").GetDateTimeOffset().ShouldBe(_clock.UtcNow);
        var other = await OfficeAsync();
        (await SendAsync(other, HttpMethod.Get, $"/api/v1/cases/{id}/movement-sync")).Status.ShouldBe(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task Invalid_page_is_atomic_and_disabling_or_nonjudicial_cases_skip_provider()
    {
        var office = await OfficeAsync();
        var id = await CaseAsync(office);
        await RunAsync(office, id);
        _provider.Calls.ShouldBe(0);
        await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "fake" });
        await RunAsync(office, await CaseAsync(office, null));
        _provider.Calls.ShouldBe(0);
        _provider.Result = new MovementPage("tjsp", [new("good", _clock.UtcNow, "Válido"), new("bad", _clock.UtcNow, "")], "next");
        await RunAsync(office, id);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movements")).Body.GetProperty("items").GetArrayLength().ShouldBe(0);
        var state = (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movement-sync")).Body;
        state.GetProperty("hasPendingPage").GetBoolean().ShouldBeFalse();
        state.GetProperty("errorCode").GetString().ShouldBe("publications.movement_invalid");
        await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "disabled" });
        _clock.Advance();
        await RunAsync(office, id);
        _provider.Calls.ShouldBe(1);
    }
    [Fact]
    public async Task Network_failure_and_invalid_cursor_are_retried_without_losing_saved_movements()
    {
        var office = await OfficeAsync();
        var id = await CaseAsync(office);
        await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "fake" });
        _provider.Result = new MovementPage("tjsp", [new("one", _clock.UtcNow, "Ato")], "next");
        await RunAsync(office, id);
        _clock.Advance();
        _provider.Throw = true;
        await RunAsync(office, id);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movement-sync")).Body.GetProperty("errorCode").GetString().ShouldBe("publications.provider_unavailable");
        _provider.Throw = false;
        _clock.Advance();
        await RunAsync(office, id); // O cursor repetido não pode confirmar outra página.
        var invalid = (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movement-sync")).Body;
        invalid.GetProperty("errorCode").GetString().ShouldBe("publications.datajud_cursor_invalid");
        invalid.GetProperty("hasPendingPage").GetBoolean().ShouldBeFalse();
        _clock.Advance();
        _provider.Result = new MovementPage("tjsp", [new("one", _clock.UtcNow, "Ato")], null);
        await RunAsync(office, id);
        _provider.Cursor.ShouldBeNull();
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movements")).Body.GetProperty("items").GetArrayLength().ShouldBe(1);
    }
    [Fact]
    public async Task Dispatcher_captures_active_cases_and_concurrent_jobs_do_not_duplicate()
    {
        var office = await OfficeAsync();
        var id = await CaseAsync(office);
        await SendAsync(office, HttpMethod.Put, "/api/v1/movement-integration", new { provider = "fake" });
        _provider.Result = new MovementPage("tjsp", [new("one", _clock.UtcNow, "Ato")], null);
        await using var dispatchScope = _factory.Services.CreateAsyncScope();
        await dispatchScope.ServiceProvider.GetRequiredService<MovementDispatchJob>().RunAsync();
        await _factory.RunJobsAsync(office.OrganizationId);
        await Task.WhenAll(RunAsync(office, id), RunAsync(office, id));
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{id}/movements")).Body.GetProperty("items").GetArrayLength().ShouldBe(1);
    }
    private sealed class SyncClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(UtcNow.DateTime);
        public void Advance() => UtcNow = UtcNow.AddMinutes(5);
    }
    private sealed class ScriptedProvider : IConfiguredMovementProvider
    {
        public Result<MovementPage> Result { get; set; } = new MovementPage("tjsp", [], null);
        public string? Cursor { get; private set; }
        public int Calls { get; private set; }
        public bool Throw { get; set; }
        public Task<Result<MovementPage>> FetchAsync(MovementIntegration configuration, MovementQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            Cursor = query.Cursor;
            if (Throw) { throw new HttpRequestException("Synthetic failure"); }
            return Task.FromResult(Result);
        }
    }
}
