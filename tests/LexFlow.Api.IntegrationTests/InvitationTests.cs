using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Web;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts;
using LexFlow.Organization.Infrastructure.Invitations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Convites por e-mail (RF-01, Etapa 3, tarefa 8) pela API real, com o e-mail enviado por um "Worker" separado:
/// token de uso único, 7 dias, só a conta do e-mail convidado aceita e entra com os perfis do convite.
/// </summary>
public sealed class InvitationTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;
    private OrganizationApi _api = null!;

    private sealed class FakeEmailSender : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Sent { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Enqueue(message);
            return Task.CompletedTask;
        }
    }

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

    private async Task<(Guid Organization, Guid Admin, Guid LawyerRole)> OfficeAsync()
    {
        var (organization, admin) = await _api.OnboardAsync("Souza & Lima Advogados");
        return (organization, admin, await _api.RoleIdAsync(organization, admin, DefaultRoles.Lawyer));
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> InviteAsync(Guid organization, Guid admin, string email, Guid roleId) =>
        _api.SendAsync(HttpMethod.Post, "/api/v1/organization/invitations", admin, organization, new
        {
            email,
            roles = new[] { new { roleId, scope = "own", scopeRefId = (Guid?)null } },
        });

    /// <summary>
    /// Faz o papel do Worker para o último pedido de e-mail do convite e devolve a mensagem e o token do link. A cópia
    /// gravada pela fábrica é a de antes do publicador preencher os metadados: ganha aqui EventId e organização.
    /// </summary>
    private async Task<(EmailMessage? Message, string? Token)> DeliverAsync(Guid organization, Guid invitationId)
    {
        var requested = _factory.PublishedEvents.OfType<InvitationEmailRequestedIntegrationEvent>().Last(e => e.InvitationId == invitationId);
        var email = new FakeEmailSender();
        await using (var worker = dependencies.WorkerServices(email))
        await using (var scope = worker.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<InvitationEmailConsumer>()
                .Handle(requested with { EventId = Guid.CreateVersion7(), OrganizationId = organization }, Ct);
        }

        if (!email.Sent.TryDequeue(out var message))
        {
            return (null, null);
        }

        var html = message.HtmlBody;
        var start = html.IndexOf("href=\"", StringComparison.Ordinal) + "href=\"".Length;
        var link = new Uri(HttpUtility.HtmlDecode(html[start..html.IndexOf('"', start)]));
        link.AbsolutePath.ShouldBe($"/{InvitationEmailComposer.AcceptPath}");
        return (message, HttpUtility.ParseQueryString(link.Query)["token"]);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> AcceptAsync(Guid user, string token) =>
        _api.SendAsync(HttpMethod.Post, "/api/v1/invitations/accept", user, body: new { token });

    private Task<(HttpStatusCode Status, JsonElement Body)> PreviewAsync(string token) =>
        _api.SendAsync(HttpMethod.Post, "/api/v1/invitations/preview", user: null, body: new { token });

    private async Task<string?> StatusOfAsync(Guid organization, Guid admin, Guid invitationId) =>
        (await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/invitations", admin, organization)).Body.EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == invitationId).GetProperty("status").GetString();

    [Fact]
    public async Task Invited_person_accepts_once_and_joins_with_the_roles_of_the_invitation()
    {
        var (organization, admin, lawyer) = await OfficeAsync();
        var account = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail(), fullName: "Beatriz Lima");

        var (status, invitation) = await InviteAsync(organization, admin, account.Email!.ToUpperInvariant(), lawyer);

        status.ShouldBe(HttpStatusCode.Created);
        invitation.GetProperty("email").GetString().ShouldBe(account.Email);
        invitation.GetProperty("status").GetString().ShouldBe("pending");
        invitation.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(invitation.GetProperty("createdAt").GetDateTimeOffset().AddDays(7), TimeSpan.FromSeconds(5));
        var invitationId = invitation.GetProperty("id").GetGuid();

        var (message, token) = await DeliverAsync(organization, invitationId);
        message.ShouldNotBeNull().To.ShouldBe([account.Email]);
        message.Subject.ShouldContain("Souza & Lima Advogados");
        message.HtmlBody.ShouldContain("Souza &amp; Lima Advogados");
        token.ShouldNotBeNullOrEmpty();

        var (previewStatus, preview) = await PreviewAsync(token);
        previewStatus.ShouldBe(HttpStatusCode.OK);
        preview.GetProperty("organizationName").GetString().ShouldBe("Souza & Lima Advogados");
        preview.GetProperty("email").GetString().ShouldBe(account.Email);

        var (acceptStatus, accepted) = await AcceptAsync(account.Id, token);
        acceptStatus.ShouldBe(HttpStatusCode.OK, accepted.ToString());
        accepted.GetProperty("organizationId").GetGuid().ShouldBe(organization);

        var members = (await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/members", admin, organization)).Body;
        var joined = members.EnumerateArray().Single(m => m.GetProperty("userId").GetGuid() == account.Id);
        joined.GetProperty("fullName").GetString().ShouldBe("Beatriz Lima");
        joined.GetProperty("email").GetString().ShouldBe(account.Email);
        joined.GetProperty("roles")[0].GetProperty("roleId").GetGuid().ShouldBe(lawyer);
        joined.GetProperty("roles")[0].GetProperty("scope").GetString().ShouldBe("own");

        var (accessStatus, access) = await _api.SendAsync(HttpMethod.Get, "/api/v1/me/access", account.Id, organization);
        accessStatus.ShouldBe(HttpStatusCode.OK);
        access.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldContain("cases.read");

        (await AcceptAsync(account.Id, token)).Status.ShouldBe(HttpStatusCode.NotFound);
        (await PreviewAsync(token)).Status.ShouldBe(HttpStatusCode.NotFound);
        (await StatusOfAsync(organization, admin, invitationId)).ShouldBe("accepted");
    }

    [Fact]
    public async Task Only_the_confirmed_account_of_the_invited_email_can_accept()
    {
        var (organization, admin, lawyer) = await OfficeAsync();
        var invited = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail(), emailConfirmed: false);
        var someoneElse = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail());
        var invitationId = (await InviteAsync(organization, admin, invited.Email!, lawyer)).Body.GetProperty("id").GetGuid();
        var (_, token) = await DeliverAsync(organization, invitationId);

        var (otherStatus, other) = await AcceptAsync(someoneElse.Id, token!);
        var (unconfirmedStatus, _) = await AcceptAsync(invited.Id, token!);

        otherStatus.ShouldBe(HttpStatusCode.Forbidden);
        OrganizationApi.Code(other).ShouldBe("organization.invitation_email_mismatch");
        unconfirmedStatus.ShouldBe(HttpStatusCode.Forbidden);
        (await StatusOfAsync(organization, admin, invitationId)).ShouldBe("pending");
    }

    [Fact]
    public async Task Expired_invitation_is_refused_and_resending_replaces_the_link()
    {
        var (organization, admin, lawyer) = await OfficeAsync();
        var account = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail());
        var invitationId = (await InviteAsync(organization, admin, account.Email!, lawyer)).Body.GetProperty("id").GetGuid();
        var (_, oldToken) = await DeliverAsync(organization, invitationId);
        await ExpireAsync(invitationId);

        var (expiredStatus, expired) = await AcceptAsync(account.Id, oldToken!);
        expiredStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(expired).ShouldBe("organization.invitation_expired");
        (await PreviewAsync(oldToken!)).Status.ShouldBe(HttpStatusCode.NotFound);
        (await StatusOfAsync(organization, admin, invitationId)).ShouldBe("expired");

        var (resendStatus, resent) = await _api.SendAsync(HttpMethod.Post, $"/api/v1/organization/invitations/{invitationId}/resend", admin, organization);
        resendStatus.ShouldBe(HttpStatusCode.OK);
        resent.GetProperty("status").GetString().ShouldBe("pending");
        var (_, newToken) = await DeliverAsync(organization, invitationId);

        newToken.ShouldNotBe(oldToken);
        (await AcceptAsync(account.Id, oldToken!)).Status.ShouldBe(HttpStatusCode.NotFound);
        (await AcceptAsync(account.Id, newToken!)).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoked_invitation_stops_working_and_duplicates_are_refused()
    {
        var (organization, admin, lawyer) = await OfficeAsync();
        var email = TestAccounts.NewEmail();
        var invitationId = (await InviteAsync(organization, admin, email, lawyer)).Body.GetProperty("id").GetGuid();
        var (_, token) = await DeliverAsync(organization, invitationId);

        var (duplicateStatus, duplicate) = await InviteAsync(organization, admin, email, lawyer);
        duplicateStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(duplicate).ShouldBe("organization.invitation_pending");

        (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/organization/invitations/{invitationId}", admin, organization)).Status.ShouldBe(HttpStatusCode.NoContent);

        var account = await TestAccounts.CreateAsync(dependencies, email);
        (await AcceptAsync(account.Id, token!)).Status.ShouldBe(HttpStatusCode.NotFound);
        (await StatusOfAsync(organization, admin, invitationId)).ShouldBe("revoked");
        var (againStatus, again) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/organization/invitations/{invitationId}", admin, organization);
        againStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(again).ShouldBe("organization.invitation_not_pending");
        (await DeliverAsync(organization, invitationId)).Message.ShouldBeNull("convite cancelado não gera e-mail");
        (await InviteAsync(organization, admin, email, lawyer)).Status.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Members_cannot_be_invited_again_and_invitations_need_roles_of_the_office()
    {
        var (organization, admin, lawyer) = await OfficeAsync();
        var member = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail());
        await TestOrganizations.AddMemberAsync(dependencies, organization, member.Id);

        var (memberStatus, alreadyMember) = await InviteAsync(organization, admin, member.Email!, lawyer);
        var (noRolesStatus, noRoles) = await _api.SendAsync(HttpMethod.Post, "/api/v1/organization/invitations", admin, organization, new { email = TestAccounts.NewEmail(), roles = Array.Empty<object>() });
        var (foreignStatus, foreign) = await InviteAsync(organization, admin, TestAccounts.NewEmail(), Guid.CreateVersion7());

        memberStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(alreadyMember).ShouldBe("organization.already_a_member");
        noRolesStatus.ShouldBe(HttpStatusCode.BadRequest);
        noRoles.GetProperty("errors").GetProperty("roles")[0].GetProperty("code").GetString().ShouldBe("organization.invitation_roles_required");
        foreignStatus.ShouldBe(HttpStatusCode.NotFound);
        OrganizationApi.Code(foreign).ShouldBe("organization.role_not_found");
    }

    /// <summary>Simula a passagem dos 7 dias direto no banco (como dono, que não está sujeito à RLS).</summary>
    private async Task ExpireAsync(Guid invitationId)
    {
        await using var connection = new NpgsqlConnection(dependencies.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var invitation = new NpgsqlCommand("UPDATE org.invitations SET expires_at = now() - interval '1 day' WHERE id = $1", connection);
        invitation.Parameters.AddWithValue(invitationId);
        await invitation.ExecuteNonQueryAsync(Ct);
        await using var token = new NpgsqlCommand("UPDATE org.invitation_tokens SET expires_at = now() - interval '1 day' WHERE invitation_id = $1", connection);
        token.Parameters.AddWithValue(invitationId);
        await token.ExecuteNonQueryAsync(Ct);
    }
}
