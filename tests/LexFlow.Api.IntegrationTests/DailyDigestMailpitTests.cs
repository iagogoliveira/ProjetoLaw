using System.Net;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Platform.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class DailyDigestMailpitTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] Permissions = ["schedule.tasks.read", "schedule.tasks.manage"];
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.6")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/livez")))
        .Build();
    private readonly DigestClock _clock = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _apiHttp = null!;
    private HttpClient _mailHttp = null!;
    private OrganizationApi _api = null!;

    public async ValueTask InitializeAsync()
    {
        await _mailpit.StartAsync(Ct);
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services =>
        {
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton(new EmailSettings
            {
                Host = _mailpit.Hostname,
                Port = _mailpit.GetMappedPublicPort(1025),
            });
        });
        _apiHttp = _factory.CreateClient();
        _api = new OrganizationApi(_apiHttp);
        _mailHttp = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
    }

    public async ValueTask DisposeAsync()
    {
        _mailHttp?.Dispose();
        _apiHttp?.Dispose();
        if (_factory is not null) { await _factory.DisposeAsync(); }
        await _mailpit.DisposeAsync();
    }

    [Fact]
    public async Task Daily_digest_is_delivered_through_real_smtp_to_mailpit_once()
    {
        var email = TestAccounts.NewEmail();
        var user = await TestAccounts.CreateAsync(dependencies, email);
        var original = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
        var member = await TestOrganizations.AddMemberAsync(dependencies, original.OrganizationId, user.Id, Permissions);
        const string taskTitle = "Mailpit daily task";
        var task = await _api.SendAsync(HttpMethod.Post, "/api/v1/tasks", member.UserId, member.OrganizationId,
            new { title = taskTitle, dueOn = "2026-10-21", assigneeUserId = member.UserId, priority = "normal" });
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        var preference = await _api.SendAsync(HttpMethod.Put, "/api/v1/me/notification-settings", member.UserId, member.OrganizationId,
            new { dailyDigestEnabled = true, includeAgenda = true, includeDeadlines = true, includePublications = true, mutedKinds = Array.Empty<string>() });
        preference.Status.ShouldBe(HttpStatusCode.OK, preference.Body.ToString());

        await SendDigestAsync(member);
        await SendDigestAsync(member);

        using var list = JsonDocument.Parse(await _mailHttp.GetStringAsync("/api/v1/messages", Ct));
        var messages = list.RootElement.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(1);
        var summary = messages[0];
        summary.GetProperty("To")[0].GetProperty("Address").GetString().ShouldBe(email);
        summary.GetProperty("Subject").GetString().ShouldBe("LexFlow — resumo diário");

        var id = summary.GetProperty("ID").GetString();
        id.ShouldNotBeNullOrWhiteSpace();
        using var detail = JsonDocument.Parse(await _mailHttp.GetStringAsync($"/api/v1/message/{Uri.EscapeDataString(id)}", Ct));
        detail.RootElement.GetProperty("Subject").GetString().ShouldBe("LexFlow — resumo diário");
        detail.RootElement.GetProperty("Text").GetString()!.ShouldContain(taskTitle);
        detail.RootElement.GetProperty("Text").GetString()!.ShouldContain("Agenda do dia");
    }

    private async Task SendDigestAsync(SeededMember member)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DailyDigestJob>()
            .RunAsync(member.OrganizationId, member.UserId, new DateOnly(2026, 10, 21));
    }

    private sealed class DigestClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, LegalTimeZone).DateTime);
    }
}
