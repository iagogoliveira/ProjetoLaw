using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.Infrastructure.Common.Persistence.Interceptors;
using LexFlow.Sample.Domain;
using LexFlow.Sample.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.IntegrationTests;

/// <summary>Base de persistência (seção 6): filtros, interceptors, concorrência e eventos de domínio com PostgreSQL real.</summary>
public sealed class PersistenceTests(PersistenceFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly TestTenant _tenant = new() { OrganizationId = Guid.CreateVersion7() };
    private readonly TestUser _user = new() { UserId = Guid.CreateVersion7() };
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = PersistenceFixture.NewClock();

    private async Task<T> InScopeAsync<T>(Func<SampleDbContext, IServiceProvider, Task<T>> action)
    {
        await using var provider = fixture.BuildProvider(_tenant, _user, _time);
        await using var scope = provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<SampleDbContext>(), scope.ServiceProvider);
    }

    private async Task InScopeAsync(Func<SampleDbContext, Task> action)
    {
        await using var provider = fixture.BuildProvider(_tenant, _user, _time);
        await using var scope = provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<SampleDbContext>());
    }

    private async Task<Guid> CreateMatterAsync(string title = "Ação de cobrança", string? cpf = "52601815906")
    {
        var id = Guid.CreateVersion7();
        await InScopeAsync(async db =>
        {
            db.Matters.Add(new Matter(id, title, cpf));
            await db.SaveChangesAsync(Ct);
        });
        return id;
    }

    [Fact]
    public async Task Insert_fills_organization_and_audit_columns()
    {
        var id = await CreateMatterAsync();

        var matter = await InScopeAsync((db, _) => db.Matters.SingleAsync(m => m.Id == id, Ct));

        matter.OrganizationId.ShouldBe(_tenant.OrganizationId!.Value);
        matter.CreatedAt.ShouldBe(_time.GetUtcNow());
        matter.CreatedBy.ShouldBe(_user.UserId!.Value);
        matter.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Queries_only_see_the_active_organization()
    {
        var mine = await CreateMatterAsync();
        var otherTenant = new TestTenant { OrganizationId = Guid.CreateVersion7() };
        await using (var provider = fixture.BuildProvider(otherTenant, _user, _time))
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            db.Matters.Add(new Matter(Guid.CreateVersion7(), "Outro escritório", null));
            await db.SaveChangesAsync(Ct);
            (await db.Matters.AnyAsync(m => m.Id == mine, Ct)).ShouldBeFalse();
        }

        var visible = await InScopeAsync((db, _) => db.Matters.Select(m => m.OrganizationId).Distinct().ToListAsync(Ct));
        visible.ShouldBe([_tenant.OrganizationId!.Value]);
    }

    [Fact]
    public async Task Without_tenant_no_office_data_is_visible_and_writes_are_rejected()
    {
        await CreateMatterAsync();
        _tenant.OrganizationId = null;

        (await InScopeAsync((db, _) => db.Matters.CountAsync(Ct))).ShouldBe(0);
        await Should.ThrowAsync<TenantViolationException>(() => CreateMatterAsync());
    }

    [Fact]
    public async Task Inserting_into_another_organization_is_rejected()
    {
        await Should.ThrowAsync<TenantViolationException>(() => InScopeAsync(async db =>
        {
            db.Matters.Add(new Matter(Guid.CreateVersion7(), "Intruso", null, organizationId: Guid.CreateVersion7()));
            await db.SaveChangesAsync(Ct);
        }));
    }

    [Fact]
    public async Task Updating_an_entity_of_another_organization_is_rejected()
    {
        var id = await CreateMatterAsync();
        var original = _tenant.OrganizationId;
        var loaded = await InScopeAsync((db, _) => db.Matters.AsNoTracking().SingleAsync(m => m.Id == id, Ct));

        _tenant.OrganizationId = Guid.CreateVersion7();
        await Should.ThrowAsync<TenantViolationException>(() => InScopeAsync(async db =>
        {
            db.Matters.Attach(loaded);
            loaded.Rename("Alterado por outro escritório");
            await db.SaveChangesAsync(Ct);
        }));

        _tenant.OrganizationId = original;
        (await InScopeAsync((db, _) => db.Matters.SingleAsync(m => m.Id == id, Ct))).Title.ShouldBe("Ação de cobrança");
    }

    [Fact]
    public async Task Update_fills_updated_columns_and_keeps_created()
    {
        var id = await CreateMatterAsync();
        _time.Advance(TimeSpan.FromHours(1));
        var editor = Guid.CreateVersion7();
        _user.UserId = editor;

        await InScopeAsync(async db =>
        {
            (await db.Matters.SingleAsync(m => m.Id == id, Ct)).Rename("Ação revisional");
            await db.SaveChangesAsync(Ct);
        });

        var matter = await InScopeAsync((db, _) => db.Matters.SingleAsync(m => m.Id == id, Ct));
        matter.UpdatedBy.ShouldBe(editor);
        matter.UpdatedAt.ShouldBe(_time.GetUtcNow());
        matter.CreatedBy.ShouldNotBe(editor);
    }

    [Fact]
    public async Task Remove_is_a_soft_delete_hidden_by_the_filter()
    {
        var id = await CreateMatterAsync();

        await InScopeAsync(async db =>
        {
            db.Matters.Remove(await db.Matters.SingleAsync(m => m.Id == id, Ct));
            await db.SaveChangesAsync(Ct);
        });

        (await InScopeAsync((db, _) => db.Matters.AnyAsync(m => m.Id == id, Ct))).ShouldBeFalse();
        (await fixture.ScalarAsync("SELECT is_deleted FROM sample.matters WHERE id = $1", id)).ShouldBe(true);
        (await fixture.ScalarAsync("SELECT deleted_at FROM sample.matters WHERE id = $1", id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Concurrent_updates_are_detected_with_xmin()
    {
        var id = await CreateMatterAsync();
        await using var provider = fixture.BuildProvider(_tenant, _user, _time);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var db1 = first.ServiceProvider.GetRequiredService<SampleDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<SampleDbContext>();
        var copy1 = await db1.Matters.SingleAsync(m => m.Id == id, Ct);
        var copy2 = await db2.Matters.SingleAsync(m => m.Id == id, Ct);

        copy1.Rename("Primeira edição");
        await db1.SaveChangesAsync(Ct);
        copy2.Rename("Edição concorrente");

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Domain_event_handler_changes_are_saved_in_the_same_save_changes()
    {
        var id = await CreateMatterAsync();

        var timeline = await InScopeAsync((db, _) => db.Timeline.Where(t => t.MatterId == id).ToListAsync(Ct));

        timeline.ShouldHaveSingleItem().Text.ShouldBe("Processo aberto");
        timeline[0].OrganizationId.ShouldBe(_tenant.OrganizationId!.Value);
    }

    [Fact]
    public async Task Unit_of_work_rollback_discards_everything()
    {
        var id = Guid.CreateVersion7();

        await InScopeAsync(async (db, services) =>
        {
            var unitOfWork = services.GetRequiredKeyedService<IUnitOfWork>("Sample");
            await using var transaction = await unitOfWork.BeginTransactionAsync(Ct);
            db.Matters.Add(new Matter(id, "Desfeito", null));
            await db.SaveChangesAsync(Ct);
            unitOfWork.HasActiveTransaction.ShouldBeTrue();
            await transaction.RollbackAsync(Ct);
            return true;
        });

        (await InScopeAsync((db, _) => db.Matters.AnyAsync(m => m.Id == id, Ct))).ShouldBeFalse();
        (await InScopeAsync((db, _) => db.Set<AuditLogEntry>().AnyAsync(a => a.EntityId == id.ToString(), Ct))).ShouldBeFalse();
    }
}
