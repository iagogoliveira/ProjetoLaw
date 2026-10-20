using System.Net;

namespace LexFlow.Api.IntegrationTests;

public sealed class DeadlineReviewSettingsTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        _api = new OrganizationApi(_client);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Deadline_review_policy_is_defaulted_and_isolated_per_organization()
    {
        var (firstOrganization, firstAdmin) = await _api.OnboardAsync();
        var (secondOrganization, secondAdmin) = await _api.OnboardAsync();

        var (firstDefaultStatus, firstDefault) = await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/deadline-review", firstAdmin, firstOrganization);
        var (setStatus, updated) = await _api.SendAsync(HttpMethod.Put, "/api/v1/organization/deadline-review", firstAdmin, firstOrganization, new { requireDeadlineReview = true });
        var (firstGetStatus, firstSettings) = await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/deadline-review", firstAdmin, firstOrganization);
        var (secondGetStatus, secondSettings) = await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/deadline-review", secondAdmin, secondOrganization);
        var (unsetStatus, unset) = await _api.SendAsync(HttpMethod.Put, "/api/v1/organization/deadline-review", firstAdmin, firstOrganization, new { requireDeadlineReview = false });

        firstDefaultStatus.ShouldBe(HttpStatusCode.OK);
        firstDefault.GetProperty("requireDeadlineReview").GetBoolean().ShouldBeFalse();
        setStatus.ShouldBe(HttpStatusCode.OK);
        updated.GetProperty("requireDeadlineReview").GetBoolean().ShouldBeTrue();
        firstGetStatus.ShouldBe(HttpStatusCode.OK);
        firstSettings.GetProperty("requireDeadlineReview").GetBoolean().ShouldBeTrue();
        secondGetStatus.ShouldBe(HttpStatusCode.OK);
        secondSettings.GetProperty("requireDeadlineReview").GetBoolean().ShouldBeFalse();
        unsetStatus.ShouldBe(HttpStatusCode.OK);
        unset.GetProperty("requireDeadlineReview").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Deadline_review_settings_require_organization_management_permission()
    {
        var (organization, admin) = await _api.OnboardAsync();
        var member = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7(), "cases.read");

        var (getStatus, getProblem) = await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/deadline-review", member.UserId, organization);
        var (putStatus, putProblem) = await _api.SendAsync(HttpMethod.Put, "/api/v1/organization/deadline-review", member.UserId, organization, new { requireDeadlineReview = true });

        getStatus.ShouldBe(HttpStatusCode.Forbidden);
        OrganizationApi.Code(getProblem).ShouldBe("auth.forbidden");
        putStatus.ShouldBe(HttpStatusCode.Forbidden);
        OrganizationApi.Code(putProblem).ShouldBe("auth.forbidden");
        (await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/deadline-review", admin, organization)).Body
            .GetProperty("requireDeadlineReview").GetBoolean().ShouldBeFalse();
    }
}
