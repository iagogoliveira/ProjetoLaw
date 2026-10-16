using LexFlow.Application.Abstractions.Idempotency;
using LexFlow.Platform.Infrastructure.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LexFlow.Platform.IntegrationTests;

/// <summary><c>platform.idempotency_keys</c> com a migration real: chave por organização, 24 h, corrida entre requisições.</summary>
public sealed class IdempotencyStoreTests(PlatformDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);

    private readonly TestTenant _tenant = new() { OrganizationId = Guid.CreateVersion7() };
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

    private ServiceProvider Provider(string? connectionString = null) =>
        PlatformDatabaseFixture.BuildProvider(connectionString ?? fixture.ConnectionString, _tenant, new TestUser { UserId = Guid.CreateVersion7() }, _time);

    [Fact]
    public async Task First_request_starts_and_completed_request_is_replayed()
    {
        await using var provider = Provider();
        var store = provider.GetRequiredService<IIdempotencyStore>();

        (await store.BeginAsync("emitir-nfse-1", HashA, Ct)).Outcome.ShouldBe(IdempotencyOutcome.Started);
        (await store.BeginAsync("emitir-nfse-1", HashA, Ct)).Outcome.ShouldBe(IdempotencyOutcome.InProgress);

        await store.CompleteAsync("emitir-nfse-1", new StoredResponse(201, "application/json", """{"id":1}"""), Ct);
        var replay = await store.BeginAsync("emitir-nfse-1", HashA, Ct);

        replay.Outcome.ShouldBe(IdempotencyOutcome.Replay);
        replay.Response.ShouldBe(new StoredResponse(201, "application/json", """{"id":1}"""));
    }

    [Fact]
    public async Task Same_key_with_another_request_hash_is_key_reused()
    {
        await using var provider = Provider();
        var store = provider.GetRequiredService<IIdempotencyStore>();
        await store.BeginAsync("faturar-1", HashA, Ct);

        (await store.BeginAsync("faturar-1", HashB, Ct)).Outcome.ShouldBe(IdempotencyOutcome.KeyReused);
    }

    [Fact]
    public async Task Keys_are_isolated_per_organization()
    {
        await using (var provider = Provider())
        {
            await provider.GetRequiredService<IIdempotencyStore>().BeginAsync("mesma-chave", HashA, Ct);
        }

        _tenant.OrganizationId = Guid.CreateVersion7();
        await using var other = Provider();

        (await other.GetRequiredService<IIdempotencyStore>().BeginAsync("mesma-chave", HashB, Ct)).Outcome.ShouldBe(IdempotencyOutcome.Started);
    }

    [Fact]
    public async Task Abandoned_key_can_be_used_again()
    {
        await using var provider = Provider();
        var store = provider.GetRequiredService<IIdempotencyStore>();
        await store.BeginAsync("importar-1", HashA, Ct);

        await store.AbandonAsync("importar-1", Ct);

        (await store.BeginAsync("importar-1", HashB, Ct)).Outcome.ShouldBe(IdempotencyOutcome.Started);
    }

    [Fact]
    public async Task Expired_key_starts_over_after_24_hours()
    {
        await using var provider = Provider();
        var store = provider.GetRequiredService<IIdempotencyStore>();
        await store.BeginAsync("gerar-peca-1", HashA, Ct);
        await store.CompleteAsync("gerar-peca-1", new StoredResponse(202, null, null), Ct);

        _time.Advance(TimeSpan.FromHours(24));

        (await store.BeginAsync("gerar-peca-1", HashB, Ct)).Outcome.ShouldBe(IdempotencyOutcome.Started);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_start_only_once()
    {
        await using var provider = Provider();
        var store = provider.GetRequiredService<IIdempotencyStore>();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.BeginAsync("corrida-1", HashA, Ct)));

        outcomes.Count(o => o.Outcome == IdempotencyOutcome.Started).ShouldBe(1);
        outcomes.Count(o => o.Outcome == IdempotencyOutcome.InProgress).ShouldBe(9);
    }

    [Fact]
    public async Task Cleanup_job_removes_expired_keys_of_all_organizations()
    {
        var database = await fixture.CreateMigratedDatabaseAsync();
        var organizations = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };
        foreach (var organization in organizations)
        {
            _tenant.OrganizationId = organization;
            await using var provider = Provider(database);
            await provider.GetRequiredService<IIdempotencyStore>().BeginAsync("expira", HashA, Ct);
        }

        _time.Advance(TimeSpan.FromHours(25));
        _tenant.OrganizationId = null; // job de plataforma: sem tenant
        await using (var provider = Provider(database))
        await using (var scope = provider.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IdempotencyKeysCleanupJob>().RunAsync()).ShouldBe(2);
        }

        (await PlatformDatabaseFixture.ScalarOnAsync<long>(database, "SELECT count(*) FROM platform.idempotency_keys")).ShouldBe(0);
    }
}
