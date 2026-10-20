using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LexFlow.Platform.IntegrationTests;

/// <summary>Migrations testadas do zero, reaplicadas e revertidas (definição de pronto, seção 1.4).</summary>
public sealed class MigrationTests(PlatformDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    /// <summary>Outros testes inserem feriados temporários; aqui só interessam os do seed.</summary>
    private static readonly Guid[] SeededHolidayIds = [.. ReferenceData.Holidays.Select(h => h.Id)];

    private async Task<T> WithContextAsync<T>(string connectionString, Func<PlatformDbContext, Task<T>> action)
    {
        await using var provider = PlatformDatabaseFixture.BuildProvider(connectionString, new TestTenant(), new TestUser());
        await using var scope = provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    [Fact]
    public async Task All_migrations_are_applied_from_scratch_and_reapplying_is_a_no_op()
    {
        var pending = await WithContextAsync(fixture.ConnectionString, async db =>
        {
            await db.Database.MigrateAsync(Ct);
            return (await db.Database.GetPendingMigrationsAsync(Ct)).ToList();
        });

        pending.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("pgcrypto")]
    [InlineData("unaccent")]
    [InlineData("pg_trgm")]
    [InlineData("vector")]
    public async Task Required_extensions_are_installed(string extension) =>
        (await fixture.ScalarAsync<long>("SELECT count(*) FROM pg_extension WHERE extname = $1", extension)).ShouldBe(1);

    [Fact]
    public async Task Migrations_can_be_reverted_and_reapplied()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();

        await WithContextAsync(connectionString, async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(cancellationToken: Ct);
            await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
            (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
            await migrator.MigrateAsync(cancellationToken: Ct);
            (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
            (await db.Courts.CountAsync(Ct)).ShouldBe(ReferenceData.Courts.Count);
            return true;
        });
    }

    /// <summary>Cada migration aplicada sobre a versão anterior (cenário de deploy), não só do zero.</summary>
    [Fact]
    public async Task Migrations_apply_incrementally_one_by_one()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();

        await WithContextAsync(connectionString, async db =>
        {
            var migrator = db.GetService<IMigrator>();
            var all = db.Database.GetMigrations().ToList();
            all.Count.ShouldBeGreaterThanOrEqualTo(2);
            foreach (var migration in all)
            {
                await migrator.MigrateAsync(migration, cancellationToken: Ct);
                (await db.Database.GetAppliedMigrationsAsync(Ct)).Last().ShouldBe(migration);
            }

            (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
            return true;
        });
    }

    [Fact]
    public async Task Reference_data_is_seeded()
    {
        var (courts, holidays) = await WithContextAsync(fixture.ConnectionString, async db =>
            (await db.Courts.AsNoTracking().ToListAsync(Ct),
             await db.Holidays.AsNoTracking().Where(h => SeededHolidayIds.Contains(h.Id)).ToListAsync(Ct)));

        // 11 superiores/conselhos/TJMs + 6 TRFs + 24 TRTs + 27 TREs + 27 TJs + 12 CJMs
        courts.Count.ShouldBe(107);
        courts.Count.ShouldBe(ReferenceData.Courts.Count);
        courts.Select(c => (c.Segment, c.Code)).Distinct().Count().ShouldBe(courts.Count);
        courts.Single(c => c.Segment == 8 && c.Code == "26").Acronym.ShouldBe("TJSP");
        courts.Single(c => c.Segment == 8 && c.Code == "07").Acronym.ShouldBe("TJDFT");
        courts.Single(c => c.Segment == 5 && c.Code == "15").State.ShouldBe("SP");
        holidays.Count.ShouldBe(ReferenceData.Holidays.Count);
        var national = holidays.Where(h => h.Scope == HolidayScope.National && h.Kind == HolidayKind.Holiday).ToList();
        national.Count.ShouldBe(10);
        HolidayCalendar.For(2026, national).Count.ShouldBe(10);
        HolidayCalendar.For(2023, national).Count.ShouldBe(9); // Consciência Negra nacional desde 2024
        var recess = holidays.Single(h => h.Kind == HolidayKind.Suspension);
        recess.DatesIn(2026).ShouldBe([.. Enumerable.Range(0, 32).Select(i => new DateOnly(2026, 12, 20).AddDays(i))]);
        holidays.Count(h => h.Scope == HolidayScope.FederalJustice).ShouldBe(6);
        holidays.Where(h => h.Scope == HolidayScope.State).ShouldAllBe(h => h.StateCode != null && h.StateCode.Length == 2);
        holidays.Single(h => h.StateCode == "SP").DateIn(2026).ShouldBe(new DateOnly(2026, 7, 9));
    }

    [Theory]
    [InlineData("2238058-98.2025.8.26.8434", "TJSP")]
    [InlineData("6616920-53.2022.4.01.1974", "TRF1")]
    [InlineData("3745690-86.2025.5.02.8348", "TRT2")]
    [InlineData("2504184-24.2010.6.26.6597", "TRE-SP")]
    [InlineData("7489586-33.2023.1.00.1272", "STF")]
    [InlineData("1854470-27.2022.9.13.5663", "TJMMG")]
    [InlineData("2669221-52.2017.7.01.5601", "1ª CJM")]
    public async Task Every_segment_of_a_cnj_number_resolves_to_a_court(string cnj, string acronym)
    {
        var number = CnjNumber.Create(cnj).Value;

        var court = await WithContextAsync(fixture.ConnectionString, db =>
            db.Courts.AsNoTracking().SingleAsync(c => c.Segment == number.Segment && c.Code == number.Court, Ct));

        court.Acronym.ShouldBe(acronym);
    }

    /// <summary>
    /// Migration de dados do PR 4C: escritórios já criados ganham "Origens de oportunidade" e "Motivos de perda" com os
    /// itens sugeridos, uma vez só; quem já tinha uma tabela com o mesmo nome fica como estava.
    /// </summary>
    [Fact]
    public async Task Existing_offices_receive_the_opportunity_lookup_tables()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await WithContextAsync(connectionString, async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261017153021_CustomFieldsAndLookupTables", cancellationToken: Ct);
            return true;
        });

        var (office, taken) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(Ct);
            await using var seed = new NpgsqlCommand(
                """
                INSERT INTO platform.lookup_tables (id, organization_id, name, system_key, is_active, created_at, created_by) VALUES
                    (gen_random_uuid(), $1, 'Fases', 'case_phases', TRUE, now(), $3),
                    (gen_random_uuid(), $2, 'Fases', 'case_phases', TRUE, now(), $3),
                    (gen_random_uuid(), $2, 'Motivos de perda', NULL, TRUE, now(), $3);
                """,
                connection);
            seed.Parameters.Add(new NpgsqlParameter { Value = office });
            seed.Parameters.Add(new NpgsqlParameter { Value = taken });
            seed.Parameters.Add(new NpgsqlParameter { Value = Guid.Empty });
            await seed.ExecuteNonQueryAsync(Ct);
        }

        await WithContextAsync(connectionString, async db =>
        {
            await db.Database.MigrateAsync(Ct);
            return true;
        });

        await using var check = new NpgsqlConnection(connectionString);
        await check.OpenAsync(Ct);
        async Task<long> CountAsync(string sql, Guid organization)
        {
            await using var command = new NpgsqlCommand(sql, check);
            command.Parameters.Add(new NpgsqlParameter { Value = organization });
            return (long)(await command.ExecuteScalarAsync(Ct))!;
        }

        const string Tables = "SELECT count(*) FROM platform.lookup_tables WHERE organization_id = $1 AND system_key IN ('opportunity_origins', 'loss_reasons')";
        const string Items = "SELECT count(*) FROM platform.lookup_items i JOIN platform.lookup_tables t ON t.id = i.table_id WHERE i.organization_id = $1 AND t.system_key IN ('opportunity_origins', 'loss_reasons')";
        (await CountAsync(Tables, office)).ShouldBe(2);
        (await CountAsync(Items, office)).ShouldBe(13);
        (await CountAsync(Tables, taken)).ShouldBe(1, "a tabela do escritório com o mesmo nome fica, e só a de origens é criada");
        (await CountAsync(Items, taken)).ShouldBe(7);
    }

    [Fact]
    public void Reference_ids_are_deterministic() =>
        ReferenceData.IdFor("court:8.26").ShouldBe(ReferenceData.IdFor("court:8.26"));
}
