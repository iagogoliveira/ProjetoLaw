using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Publications.Application;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using LexFlow.Scheduling.Contracts;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] Permissions = ["settings.integrations.manage", "publications.read", "publications.triage", "publications.distribute", "publications.close",
        "cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.deadlines.read", "schedule.deadlines.manage", "schedule.tasks.read", "schedule.tasks.manage"];
    private static readonly string[] ClientRole = ["client"];
    private readonly WorkflowClock _clock = new();
    private readonly WorkflowFailure _failure = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services =>
        {
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton(_failure);
            services.Decorate<ISchedulingIntake, FailingSchedulingIntake>();
            services.ConfigureDbContext<PublicationsDbContext>((_, options) => options.AddInterceptors(new TriageSaveFailure(_failure)));
        });
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
    private Task<Guid> SourceAsync(SeededMember member) => _api.CreateAsync("/api/v1/publication-sources", member.UserId, member.OrganizationId,
        new { kind = "name", label = "Escritório", value = "Nome monitorado" });
    private async Task<Guid> CaseAsync(SeededMember member, Guid? lawyer = null, string? cnj = null)
    {
        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "company", name = "Cliente", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = cnj is null ? "extrajudicial" : "judicial",
            cnjNumber = cnj,
            instance = "first",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = lawyer ?? member.UserId } },
        });
    }
    private async Task InOfficeAsync(SeededMember office, Func<IServiceProvider, Task> action)
    {
        using var ambient = AmbientContext.Begin(office.OrganizationId, "workflow-test");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(office.OrganizationId);
        await action(scope.ServiceProvider);
    }
    private async Task<Guid> ReceiveAsync(SeededMember member, Guid source, string? text = null)
    {
        var id = Guid.Empty;
        await InOfficeAsync(member, async services =>
        {
            var result = await services.GetRequiredService<ICommandHandler<ReceivePublicationCommand, Guid>>()
                .Handle(new ReceivePublicationCommand(new(source, "DJE", _clock.Today, _clock.Today, text ?? $"Texto {Guid.CreateVersion7():N}")), Ct);
            result.IsSuccess.ShouldBeTrue(result.Error?.Code);
            id = result.Value;
        });
        return id;
    }
    private async Task CompleteCaptureAsync(SeededMember office, Guid sourceId, string? error = null)
    {
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<PublicationsDbContext>();
            var source = await db.Sources.SingleAsync(s => s.Id == sourceId, Ct);
            var run = CaptureRun.Start(sourceId, _clock.Today, _clock.UtcNow, source.ActivatedAt ?? source.CreatedAt).Value;
            run.Finish(0, error, _clock.UtcNow);
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
        });
    }
    private object DeadlineInput(SeededMember office, Guid caseId, bool confirmed = true, Guid? assignee = null) => new
    {
        classification = "deadlineNotice",
        action = "deadline",
        confirmed,
        caseId,
        deadline = new { title = "Contestação", startOn = _clock.Today, internalDueOn = _clock.Today.AddDays(10), fatalDueOn = _clock.Today.AddDays(15), assigneeUserId = assignee ?? office.UserId },
    };
    private Task<(HttpStatusCode Status, JsonElement Body)> TriageAsync(SeededMember member, Guid publication, object input) =>
        SendAsync(member, HttpMethod.Post, $"/api/v1/publications/{publication}/triage", input);
    private static object Discard(string? reason = "Homônimo", bool confirmed = true) => new { classification = "other", action = "discard", confirmed, reason };

    private sealed class WorkflowClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, LegalTimeZone).DateTime);
    }
    private sealed class WorkflowFailure { public bool AfterTarget { get; set; } public bool AfterBothSaves { get; set; } }
    private sealed class FailingSchedulingIntake(ISchedulingIntake inner, WorkflowFailure failure) : ISchedulingIntake
    {
        public async Task<Result<Guid>> CreateDeadlineAsync(DeadlineDraft draft, CancellationToken cancellationToken)
        {
            var result = await inner.CreateDeadlineAsync(draft, cancellationToken);
            return result.IsSuccess && failure.AfterTarget ? Error.BusinessRule("test.after_destination", "Falha injetada após gravar o destino.") : result;
        }
        public Task<Result<Guid>> CreateTaskAsync(TaskDraft draft, CancellationToken cancellationToken) => inner.CreateTaskAsync(draft, cancellationToken);
    }
    private sealed class TriageSaveFailure(WorkflowFailure failure) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (failure.AfterBothSaves && eventData.Context!.ChangeTracker.Entries<PublicationTriage>().Any())
            { throw new InvalidOperationException("Falha injetada após gravar triagem e destino."); }
            return ValueTask.FromResult(result);
        }
    }
}
