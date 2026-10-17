using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.UnitTests.Web;

/// <summary>Organização ativa validada contra a pertença e a política de permissão real (Etapa 3, PR 3B).</summary>
public sealed class TenantAndPermissionTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private sealed class FakeMemberships(MembershipSnapshot snapshot) : IMembershipLookup
    {
        public Task<MembershipSnapshot> GetAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class FakeChecker(PermissionDecision decision) : IPermissionChecker
    {
        public List<PermissionCheck> Checks { get; } = [];

        public Task<PermissionDecision> CheckAsync(PermissionCheck check, CancellationToken cancellationToken)
        {
            Checks.Add(check);
            return Task.FromResult(decision);
        }
    }

    private static Task<WebApplication> StartAsync(MembershipSnapshot? memberships, IPermissionChecker? checker) =>
        TestWebHost.StartAsync(
            app =>
            {
                app.MapGet("/tenant", (ITenantContext tenant) => Results.Ok(new { tenant.OrganizationId }));
                app.MapGet("/protected", (ICurrentUser user, ITenantContext tenant) => new { user.UserId, tenant.OrganizationId })
                    .RequirePermission("sample.things.read");
            },
            withTestAuthentication: true,
            services =>
            {
                if (memberships is not null)
                {
                    services.AddSingleton<IMembershipLookup>(new FakeMemberships(memberships));
                }

                if (checker is not null)
                {
                    services.AddSingleton(checker);
                }
            });

    private static HttpRequestMessage Get(string path, Guid user, Guid? organization = null, string? header = null, string? amr = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestWebHost.UserHeader, user.ToString());
        if (organization is { } org)
        {
            request.Headers.Add(TestWebHost.OrganizationHeader, org.ToString());
        }

        if (header is not null)
        {
            request.Headers.Add(TenantResolutionMiddleware.HeaderName, header);
        }

        if (amr is not null)
        {
            request.Headers.Add(TestWebHost.AmrHeader, amr);
        }

        return request;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;

    [Fact]
    public async Task Requested_organization_is_used_when_the_user_is_a_member()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        await using var app = await StartAsync(new MembershipSnapshot([first, second], first), new FakeChecker(PermissionDecision.Allowed));
        using var client = app.GetTestClient();
        using var request = Get("/protected", Guid.CreateVersion7(), header: second.ToString());

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BodyAsync(response)).GetProperty("organizationId").GetGuid().ShouldBe(second);
    }

    [Fact]
    public async Task Claim_is_also_validated_against_membership()
    {
        var member = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();
        await using var app = await StartAsync(new MembershipSnapshot([member], null), new FakeChecker(PermissionDecision.Allowed));
        using var client = app.GetTestClient();
        using var allowed = Get("/protected", user, organization: member);
        using var refused = Get("/protected", user, organization: Guid.CreateVersion7());

        using var ok = await client.SendAsync(allowed, Ct);
        using var forbidden = await client.SendAsync(refused, Ct);

        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BodyAsync(ok)).GetProperty("userId").GetGuid().ShouldBe(user);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await BodyAsync(forbidden)).GetProperty("code").GetString().ShouldBe("organization.not_a_member");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("0196f2a1-0000-7000-8000-000000000000")]
    public async Task Organization_the_user_does_not_belong_to_is_refused(string header)
    {
        await using var app = await StartAsync(new MembershipSnapshot([Guid.CreateVersion7()], null), new FakeChecker(PermissionDecision.Allowed));
        using var client = app.GetTestClient();
        using var request = Get("/tenant", Guid.CreateVersion7(), header: header);

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await BodyAsync(response)).GetProperty("code").GetString().ShouldBe("organization.not_a_member");
    }

    [Fact]
    public async Task Without_a_request_the_preferred_or_the_only_organization_is_active()
    {
        var only = Guid.CreateVersion7();
        var preferred = Guid.CreateVersion7();
        await using var single = await StartAsync(new MembershipSnapshot([only], null), null);
        await using var several = await StartAsync(new MembershipSnapshot([Guid.CreateVersion7(), preferred], preferred), null);
        await using var ambiguous = await StartAsync(new MembershipSnapshot([Guid.CreateVersion7(), Guid.CreateVersion7()], null), null);

        async Task<JsonElement> TenantOf(WebApplication app)
        {
            using var client = app.GetTestClient();
            using var request = Get("/tenant", Guid.CreateVersion7());
            using var response = await client.SendAsync(request, Ct);
            return (await BodyAsync(response)).GetProperty("organizationId");
        }

        (await TenantOf(single)).GetGuid().ShouldBe(only);
        (await TenantOf(several)).GetGuid().ShouldBe(preferred);
        (await TenantOf(ambiguous)).ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Without_membership_lookup_nothing_from_the_client_is_trusted()
    {
        await using var app = await StartAsync(null, null);
        using var client = app.GetTestClient();
        using var request = Get("/tenant", Guid.CreateVersion7(), organization: Guid.CreateVersion7());

        using var response = await client.SendAsync(request, Ct);

        (await BodyAsync(response)).GetProperty("organizationId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Checker_receives_user_tenant_permission_and_mfa()
    {
        var organization = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();
        var checker = new FakeChecker(PermissionDecision.Allowed);
        await using var app = await StartAsync(new MembershipSnapshot([organization], null), checker);
        using var client = app.GetTestClient();
        using var request = Get("/protected", user, amr: "pwd,mfa");

        (await client.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        checker.Checks.Single().ShouldBe(new PermissionCheck(user, organization, "sample.things.read", MultiFactorAuthenticated: true));
    }

    [Fact]
    public async Task Denied_permission_is_403_and_missing_mfa_has_its_own_code()
    {
        var organization = Guid.CreateVersion7();
        await using var denied = await StartAsync(new MembershipSnapshot([organization], null), new FakeChecker(PermissionDecision.Denied));
        await using var mfa = await StartAsync(new MembershipSnapshot([organization], null), new FakeChecker(PermissionDecision.MfaRequired));
        await using var unconfigured = await StartAsync(new MembershipSnapshot([organization], null), null);

        async Task<(HttpStatusCode Status, string? Code)> CallAsync(WebApplication app)
        {
            using var client = app.GetTestClient();
            using var request = Get("/protected", Guid.CreateVersion7());
            using var response = await client.SendAsync(request, Ct);
            return (response.StatusCode, (await BodyAsync(response)).GetProperty("code").GetString());
        }

        (await CallAsync(denied)).ShouldBe((HttpStatusCode.Forbidden, "auth.forbidden"));
        (await CallAsync(mfa)).ShouldBe((HttpStatusCode.Forbidden, "organization.mfa_required"));
        (await CallAsync(unconfigured)).ShouldBe((HttpStatusCode.Forbidden, "auth.forbidden"));
    }
}
