using System.Text.Json;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.Sample.Domain;
using LexFlow.Sample.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.IntegrationTests;

/// <summary>
/// Critério de aceite da Etapa 1: <c>SaveChanges</c> gera linha em <c>audit.audit_logs</c> com antes/depois.
/// Aqui a tabela vem do modelo EF do contexto de exemplo; o encadeamento por hash (trigger) é testado no módulo Platform.
/// </summary>
public sealed class AuditTrailTests(PersistenceFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly TestTenant _tenant = new() { OrganizationId = Guid.CreateVersion7() };
    private readonly TestUser _user = new() { UserId = Guid.CreateVersion7() };
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = PersistenceFixture.NewClock();

    private async Task RunAsync(Func<SampleDbContext, Task> action)
    {
        await using var provider = fixture.BuildProvider(_tenant, _user, _time);
        await using var scope = provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<SampleDbContext>());
    }

    private async Task<List<AuditLogEntry>> AuditOfAsync(Guid id)
    {
        List<AuditLogEntry> result = [];
        await RunAsync(async db => result = await db.Set<AuditLogEntry>()
            .Where(a => a.EntityId == id.ToString())
            .OrderBy(a => a.Id)
            .ToListAsync(Ct));
        return result;
    }

    [Fact]
    public async Task Save_changes_writes_audit_rows_with_before_and_after()
    {
        var id = Guid.CreateVersion7();
        await RunAsync(async db =>
        {
            db.Matters.Add(new Matter(id, "Ação de cobrança", "52601815906"));
            await db.SaveChangesAsync(Ct);
        });
        _time.Advance(TimeSpan.FromSeconds(1));
        await RunAsync(async db =>
        {
            (await db.Matters.SingleAsync(m => m.Id == id, Ct)).Rename("Ação monitória");
            await db.SaveChangesAsync(Ct);
        });

        var audit = await AuditOfAsync(id);

        audit.Count.ShouldBe(2);
        audit[0].Action.ShouldBe(AuditActions.Created);
        Changes(audit[0]).GetProperty("title").GetProperty("new").GetString().ShouldBe("Ação de cobrança");

        var update = audit[1];
        update.Action.ShouldBe(AuditActions.Updated);
        update.EntityType.ShouldBe(nameof(Matter));
        update.OrganizationId.ShouldBe(_tenant.OrganizationId);
        update.UserId.ShouldBe(_user.UserId);
        update.Ip.ShouldBe("203.0.113.10");
        update.UserAgent.ShouldBe("LexFlow.Tests");
        update.CorrelationId.ShouldBe("test-correlation");
        update.OccurredAt.ShouldBe(_time.GetUtcNow());

        var title = Changes(update).GetProperty("title");
        title.GetProperty("old").GetString().ShouldBe("Ação de cobrança");
        title.GetProperty("new").GetString().ShouldBe("Ação monitória");
    }

    [Fact]
    public async Task Sensitive_and_not_audited_fields_never_enter_changes()
    {
        var id = Guid.CreateVersion7();
        await RunAsync(async db =>
        {
            db.Matters.Add(new Matter(id, "Ação", "52601815906"));
            await db.SaveChangesAsync(Ct);
        });
        await RunAsync(async db =>
        {
            var matter = await db.Matters.SingleAsync(m => m.Id == id, Ct);
            matter.ChangeCpf("08301661305");
            matter.Annotate("texto interno");
            await db.SaveChangesAsync(Ct);
        });

        var audit = await AuditOfAsync(id);

        audit.ShouldHaveSingleItem("só o insert; a alteração só tocou campos sensíveis/não auditados");
        audit[0].Changes.ShouldNotContain("52601815906");
        audit[0].Changes.ShouldNotContain("clientCpf");
        audit[0].Changes.ShouldNotContain("version");
    }

    [Fact]
    public async Task Soft_delete_is_audited_as_deleted()
    {
        var id = Guid.CreateVersion7();
        await RunAsync(async db =>
        {
            db.Matters.Add(new Matter(id, "Ação", null));
            await db.SaveChangesAsync(Ct);
        });
        _time.Advance(TimeSpan.FromSeconds(1));
        await RunAsync(async db =>
        {
            db.Matters.Remove(await db.Matters.SingleAsync(m => m.Id == id, Ct));
            await db.SaveChangesAsync(Ct);
        });

        var deletion = (await AuditOfAsync(id))[^1];

        deletion.Action.ShouldBe(AuditActions.Deleted);
        Changes(deletion).GetProperty("isDeleted").GetProperty("new").GetBoolean().ShouldBeTrue();
    }

    private static JsonElement Changes(AuditLogEntry entry) => JsonDocument.Parse(entry.Changes).RootElement;
}
