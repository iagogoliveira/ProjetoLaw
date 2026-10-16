using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Platform.IntegrationTests;

/// <summary>Migrations testadas do zero, reaplicadas e revertidas (definição de pronto, seção 1.4).</summary>
public sealed class MigrationTests(PlatformDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    /// <summary>Outros testes inserem feriados temporários; aqui só interessam os do seed.</summary>
    private static readonly Guid[] SeededHolidayIds = [.. ReferenceData.NationalHolidays.Select(h => h.Id)];

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
        holidays.Count.ShouldBe(10);
        HolidayCalendar.For(2026, holidays).Count.ShouldBe(10);
        HolidayCalendar.For(2023, holidays).Count.ShouldBe(9); // Consciência Negra nacional desde 2024
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

    [Fact]
    public void Reference_ids_are_deterministic() =>
        ReferenceData.IdFor("court:8.26").ShouldBe(ReferenceData.IdFor("court:8.26"));
}
