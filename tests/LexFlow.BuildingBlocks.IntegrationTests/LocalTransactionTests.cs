using System.Data;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Sample.Domain;
using LexFlow.Sample.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.BuildingBlocks.IntegrationTests;

public sealed class LocalTransactionTests : IAsyncDisposable
{
    private static readonly string[] Modules = ["Sample", "SecondarySample"];
    private readonly PersistenceFixture _fixture;
    private readonly TestTenant _tenant = new() { OrganizationId = Guid.CreateVersion7() };
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly Guid _matterId = Guid.CreateVersion7();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public LocalTransactionTests(PersistenceFixture fixture)
    {
        _fixture = fixture;
        _provider = fixture.BuildProvider(_tenant, new TestUser { UserId = Guid.CreateVersion7() }, PersistenceFixture.NewClock());
        _scope = _provider.CreateAsyncScope();
    }

    private SampleDbContext Primary => _scope.ServiceProvider.GetRequiredService<SampleDbContext>();

    private SecondarySampleDbContext Secondary => _scope.ServiceProvider.GetRequiredService<SecondarySampleDbContext>();

    private ILocalTransactionCoordinator Coordinator => _scope.ServiceProvider.GetRequiredService<ILocalTransactionCoordinator>();

    [Fact]
    public async Task Distinct_contexts_share_one_connection_and_transaction_and_commit_with_audit()
    {
        // A autorização pode ter consultado o banco antes da abertura da transação.
        await Primary.Timeline.AnyAsync(_ct);
        await Secondary.Timeline.AnyAsync(_ct);
        await using (var transaction = await Coordinator.BeginAsync(Modules, _ct))
        {
            ReferenceEquals(Primary.Database.GetDbConnection(), Secondary.Database.GetDbConnection()).ShouldBeTrue();
            ReferenceEquals(Primary.Database.CurrentTransaction!.GetDbTransaction(), Secondary.Database.CurrentTransaction!.GetDbTransaction()).ShouldBeTrue();
            await SaveBothAsync();
            await transaction.CommitAsync(_ct);
        }

        (await CountAsync()).ShouldBe(2L);
        (await AuditCountAsync()).ShouldBe(2L);
        AssertDetached();
    }

    [Fact]
    public async Task Explicit_rollback_removes_both_saves_and_their_audit()
    {
        await using (var transaction = await Coordinator.BeginAsync(Modules, _ct))
        {
            await SaveBothAsync();
            await transaction.RollbackAsync(_ct);
        }

        await AssertRolledBackAsync();
    }

    [Fact]
    public async Task Discard_without_commit_rolls_back_and_contexts_can_be_used_again()
    {
        await using (await Coordinator.BeginAsync(Modules, _ct))
        {
            await SaveBothAsync();
        }

        await AssertRolledBackAsync();
        await SaveBothAsync();
        (await CountAsync()).ShouldBe(2L);
        (await Primary.Timeline.CountAsync(x => x.MatterId == _matterId, _ct)).ShouldBe(2);
        (await Secondary.Timeline.CountAsync(x => x.MatterId == _matterId, _ct)).ShouldBe(2);
    }

    [Fact]
    public async Task Exception_after_both_saves_rolls_everything_back()
    {
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await using var transaction = await Coordinator.BeginAsync(Modules, _ct);
            await SaveBothAsync();
            throw new InvalidOperationException("falha após as duas gravações");
        });

        await AssertRolledBackAsync();
    }

    [Fact]
    public async Task Cancelled_commit_is_rolled_back_during_disposal_with_uncancelled_token()
    {
        await using (var transaction = await Coordinator.BeginAsync(Modules, _ct))
        {
            await SaveBothAsync();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_ct);
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => transaction.CommitAsync(cancellation.Token));
        }

        await AssertRolledBackAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Undefined_office_is_rejected(string? organization)
    {
        _tenant.OrganizationId = organization is null ? null : Guid.Parse(organization);

        await Should.ThrowAsync<InvalidOperationException>(() => Coordinator.BeginAsync(Modules, _ct));
        AssertDetached();
    }

    [Fact]
    public async Task Changed_office_before_commit_cannot_commit_either_context()
    {
        var original = _tenant.OrganizationId;
        await using (var transaction = await Coordinator.BeginAsync(Modules, _ct))
        {
            await SaveBothAsync();
            _tenant.OrganizationId = Guid.CreateVersion7();
            await Should.ThrowAsync<InvalidOperationException>(() => transaction.CommitAsync(_ct));
        }

        _tenant.OrganizationId = original;
        await AssertRolledBackAsync();
    }

    [Fact]
    public async Task Independent_transaction_cannot_be_joined_or_committed_by_coordinator()
    {
        await using var independent = await Secondary.Database.BeginTransactionAsync(_ct);

        await Should.ThrowAsync<InvalidOperationException>(() => Coordinator.BeginAsync(Modules, _ct));
        Secondary.Database.CurrentTransaction.ShouldBeSameAs(independent);
        Primary.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task Already_open_connection_is_rejected_without_taking_ownership()
    {
        await Secondary.Database.OpenConnectionAsync(_ct);
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() => Coordinator.BeginAsync(Modules, _ct));
            Secondary.Database.GetDbConnection().State.ShouldBe(ConnectionState.Open);
        }
        finally
        {
            await Secondary.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task Unrelated_unsaved_changes_are_not_enlisted()
    {
        Secondary.Timeline.Add(new MatterTimelineEntry(Guid.CreateVersion7(), _matterId, "pendente"));

        await Should.ThrowAsync<InvalidOperationException>(() => Coordinator.BeginAsync(Modules, _ct));
        Secondary.ChangeTracker.HasChanges().ShouldBeTrue();
        (await CountAsync()).ShouldBe(0L);
    }

    [Theory]
    [InlineData("Sample", "Sample")]
    [InlineData("Sample", "Missing")]
    [InlineData("Sample", " ")]
    public async Task Invalid_participants_fail_before_a_transaction_is_opened(string first, string second)
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Coordinator.BeginAsync([first, second], _ct));
        AssertDetached();
    }

    [Fact]
    public async Task Single_module_does_not_opt_in_to_shared_transaction()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Coordinator.BeginAsync(["Sample"], _ct));
        AssertDetached();
    }

    [Fact]
    public async Task Nested_coordination_is_rejected_and_outer_transaction_can_finish()
    {
        await using var transaction = await Coordinator.BeginAsync(Modules, _ct);
        await Should.ThrowAsync<InvalidOperationException>(() => Coordinator.BeginAsync(Modules, _ct));
        await SaveBothAsync();
        await transaction.CommitAsync(_ct);

        (await CountAsync()).ShouldBe(2L);
    }

    [Fact]
    public async Task Coordinator_can_start_another_transaction_after_rollback()
    {
        await using (var first = await Coordinator.BeginAsync(Modules, _ct))
        {
            await SaveBothAsync();
            await first.RollbackAsync(_ct);
        }

        await using (var second = await Coordinator.BeginAsync(Modules, _ct))
        {
            await SaveBothAsync();
            await second.CommitAsync(_ct);
        }

        (await CountAsync()).ShouldBe(2L);
        (await AuditCountAsync()).ShouldBe(2L);
        AssertDetached();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Different_office_or_database_in_a_participant_is_rejected(bool otherOffice)
    {
        await using var provider = _fixture.BuildProvider(_tenant, new TestUser(), PersistenceFixture.NewClock(), configure: services =>
            services.Replace(ServiceDescriptor.Scoped(sp => new SecondarySampleDbContext(
                OtherOptions(sp, otherOffice), new TestTenant { OrganizationId = otherOffice ? Guid.CreateVersion7() : _tenant.OrganizationId }))));
        await using var scope = provider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<ILocalTransactionCoordinator>();

        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(Modules, _ct));
        scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.CurrentTransaction.ShouldBeNull();
    }

    private DbContextOptions<SecondarySampleDbContext> OtherOptions(IServiceProvider services, bool sameDatabase) =>
        sameDatabase ? services.GetRequiredService<DbContextOptions<SecondarySampleDbContext>>()
            : new DbContextOptionsBuilder<SecondarySampleDbContext>().UseNpgsql("Host=unrelated;Database=other;Username=other").Options;

    private async Task SaveBothAsync()
    {
        Primary.Timeline.Add(new MatterTimelineEntry(Guid.CreateVersion7(), _matterId, "principal"));
        await Primary.SaveChangesAsync(_ct);
        Secondary.Timeline.Add(new MatterTimelineEntry(Guid.CreateVersion7(), _matterId, "secundário"));
        await Secondary.SaveChangesAsync(_ct);
    }

    private Task<object?> CountAsync() => _fixture.ScalarAsync("SELECT count(*) FROM sample.timeline WHERE matter_id = $1", _matterId);

    private Task<object?> AuditCountAsync() => _fixture.ScalarAsync(
        "SELECT count(*) FROM audit.audit_logs WHERE organization_id = $1 AND entity_type = 'MatterTimelineEntry'", _tenant.OrganizationId!.Value);

    private async Task AssertRolledBackAsync()
    {
        (await CountAsync()).ShouldBe(0L);
        (await AuditCountAsync()).ShouldBe(0L);
        Primary.ChangeTracker.Entries().ShouldBeEmpty();
        Secondary.ChangeTracker.Entries().ShouldBeEmpty();
        AssertDetached();
    }

    private void AssertDetached()
    {
        Primary.Database.CurrentTransaction.ShouldBeNull();
        Secondary.Database.CurrentTransaction.ShouldBeNull();
        ReferenceEquals(Primary.Database.GetDbConnection(), Secondary.Database.GetDbConnection()).ShouldBeFalse();
        Primary.Database.GetDbConnection().State.ShouldBe(ConnectionState.Closed);
        Secondary.Database.GetDbConnection().State.ShouldBe(ConnectionState.Closed);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
