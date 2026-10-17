using LexFlow.Identity.Infrastructure;
using LexFlow.Identity.Infrastructure.Oidc;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LexFlow.Identity.IntegrationTests;

/// <summary>Cliente OIDC cadastrado pelo MigrationService e limpeza de tokens, sobre as tabelas reais do OpenIddict.</summary>
public sealed class OidcStoreTests(IdentityDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Web_client_is_public_requires_pkce_and_seeding_twice_keeps_a_single_client()
    {
        await using var provider = IdentityDatabaseFixture.BuildProvider(fixture.ConnectionString, new FakeEmailSender());
        for (var run = 0; run < 2; run++)
        {
            await using var seeding = provider.CreateAsyncScope();
            foreach (var seeder in seeding.ServiceProvider.GetServices<IDatabaseSeeder>())
            {
                await seeder.SeedAsync(Ct);
            }
        }

        await using var scope = provider.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = (await applications.FindByClientIdAsync(WebClientSettings.ClientId, Ct))!;

        (await applications.GetClientTypeAsync(client, Ct)).ShouldBe(ClientTypes.Public);
        (await applications.HasRequirementAsync(client, Requirements.Features.ProofKeyForCodeExchange, Ct)).ShouldBeTrue();
        (await applications.GetRedirectUrisAsync(client, Ct)).ShouldBe(["http://localhost:4200/auth/callback"]);
        (await applications.HasPermissionAsync(client, Permissions.GrantTypes.RefreshToken, Ct)).ShouldBeTrue();
        (await applications.HasPermissionAsync(client, Permissions.GrantTypes.ClientCredentials, Ct)).ShouldBeFalse();
        (await fixture.ScalarAsync<long>("SELECT count(*) FROM identity.openiddict_applications WHERE client_id = $1", WebClientSettings.ClientId)).ShouldBe(1);
    }

    /// <summary>O job do Worker roda contra as tabelas reais (a regra de remoção é do próprio OpenIddict).</summary>
    [Fact]
    public async Task Cleanup_job_runs_against_the_real_tables()
    {
        await using var provider = IdentityDatabaseFixture.BuildProvider(fixture.ConnectionString, new FakeEmailSender());
        await using var scope = provider.CreateAsyncScope();

        await Should.NotThrowAsync(() => scope.ServiceProvider.GetRequiredService<OidcTokensCleanupJob>().RunAsync());
    }
}
