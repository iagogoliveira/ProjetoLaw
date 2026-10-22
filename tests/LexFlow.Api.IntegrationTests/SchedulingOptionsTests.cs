using System.Net;

namespace LexFlow.Api.IntegrationTests;

public sealed class SchedulingOptionsTests(DependenciesFixture dependencies)
{
    private static readonly string[] TaskPermissions = ["schedule.tasks.read"];
    private static readonly string[] EventPermissions = ["schedule.events.read"];

    [Fact]
    public async Task Options_allow_business_readers_without_case_or_settings_permissions_and_isolate_tenants()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        var taskOffice = await TestOrganizations.CreateWithMemberAsync(dependencies, TaskPermissions);
        var eventOffice = await TestOrganizations.CreateWithMemberAsync(dependencies, EventPermissions);
        var taskReader = await TestOrganizations.AddMemberAsync(dependencies, taskOffice.OrganizationId,
            (await OidcTestClient.CreateUserAsync(factory)).Id, TaskPermissions);
        var eventReader = await TestOrganizations.AddMemberAsync(dependencies, eventOffice.OrganizationId,
            (await OidcTestClient.CreateUserAsync(factory)).Id, EventPermissions);
        using var client = factory.CreateClient();
        var api = new OrganizationApi(client);
        var (taskStatus, tasks) = await api.SendAsync(HttpMethod.Get, "/api/v1/tasks/options", taskReader.UserId, taskReader.OrganizationId);
        taskStatus.ShouldBe(HttpStatusCode.OK, tasks.ToString());
        tasks.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("userId").GetGuid()).ShouldBe([taskReader.UserId]);
        tasks.GetProperty("members")[0].GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
        tasks.GetProperty("members")[0].TryGetProperty("email", out _).ShouldBeFalse();
        var (eventStatus, events) = await api.SendAsync(HttpMethod.Get, "/api/v1/schedule/options", eventReader.UserId, eventReader.OrganizationId);
        eventStatus.ShouldBe(HttpStatusCode.OK, events.ToString());
        events.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("userId").GetGuid()).ShouldBe([eventReader.UserId]);
        var (deniedTask, _) = await api.SendAsync(HttpMethod.Get, "/api/v1/tasks/options", eventReader.UserId, eventReader.OrganizationId);
        deniedTask.ShouldBe(HttpStatusCode.Forbidden);
        var (deniedEvent, _) = await api.SendAsync(HttpMethod.Get, "/api/v1/schedule/options", taskReader.UserId, taskReader.OrganizationId);
        deniedEvent.ShouldBe(HttpStatusCode.Forbidden);
    }
}
