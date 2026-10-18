using System.Globalization;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Application.Indexes;
using LexFlow.Platform.Contracts.Indexes;
using LexFlow.Platform.Domain.Indexes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LexFlow.Platform.IntegrationTests;

/// <summary>
/// Índices monetários com a migration real (seed de 01/2000 a 09/2026) e o critério 3 da Etapa 5: a correção confere
/// com 10 casos calculados à mão. Os fatores de IPCA-E e INPC foram conferidos também na Calculadora do Cidadão do BCB
/// em 2026-10-18 (lá o mês final é incluído: "01/2020 a 12/2020" equivale aqui a corrigir de 01/2020 até uma data de
/// 01/2021).
/// </summary>
public sealed class MonetaryIndexTests(PlatformDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private ServiceProvider Provider(string? connectionString = null, DateTimeOffset? now = null) =>
        PlatformDatabaseFixture.BuildProvider(
            connectionString ?? fixture.ConnectionString,
            new TestTenant(),
            new TestUser(),
            new FakeTimeProvider(now ?? new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero)));

    [Theory]
    [InlineData(MonetaryIndexKind.IpcaE, "2000-05-01", 317)]
    [InlineData(MonetaryIndexKind.Inpc, "2000-01-01", 321)]
    [InlineData(MonetaryIndexKind.Selic, "2000-01-01", 321)]
    [InlineData(MonetaryIndexKind.Tr, "2000-01-01", 321)]
    public async Task The_seed_has_every_month_until_september_2026(MonetaryIndexKind kind, string first, int count)
    {
        await using var provider = Provider();
        await using var scope = provider.CreateAsyncScope();
        var months = await scope.ServiceProvider.GetRequiredService<IPlatformDbContext>().MonetaryIndexes
            .Where(i => i.Kind == kind).OrderBy(i => i.Month).Select(i => i.Month).ToListAsync(Ct);

        months.Count.ShouldBe(count);
        months[0].ShouldBe(DateOnly.Parse(first, CultureInfo.InvariantCulture));
        months[^1].ShouldBe(new DateOnly(2026, 9, 1));
        months.Zip(months.Skip(1)).ShouldAllBe(pair => pair.First.AddMonths(1) == pair.Second);
    }

    /// <summary>
    /// Os 10 casos do critério 3. Colunas: índice, valor, início da correção, data do cálculo, início dos juros, taxa
    /// (% a.m.), fator, valor corrigido, meses de juros, juros e total. A conta à mão de cada um está na ADR 0027.
    /// </summary>
    [Theory]
    [InlineData("C1", MonetaryIndexCode.Inpc, "10000.00", "2020-01-15", "2021-01-15", null, null, "1.054473253", "10544.73", "0", "0.00", "10544.73")]
    [InlineData("C2", MonetaryIndexCode.IpcaE, "5000.00", "2015-03-10", "2016-03-10", null, null, "1.108377834", "5541.89", "0", "0.00", "5541.89")]
    [InlineData("C3", MonetaryIndexCode.Inpc, "1000.00", "2026-07-01", "2026-09-01", null, null, "0.996700320", "1000.00", "0", "0.00", "1000.00")]
    [InlineData("C4", MonetaryIndexCode.Inpc, "2500.00", "2026-06-01", "2026-10-01", null, null, "1.006280085", "2515.70", "0", "0.00", "2515.70")]
    [InlineData("C5", MonetaryIndexCode.IpcaE, "20000.00", "2024-01-10", "2025-03-20", "2024-02-05", "1", "1.061111380", "21222.23", "13.5", "2865.00", "24087.23")]
    [InlineData("C6", MonetaryIndexCode.Inpc, "1000.00", "2000-01-01", "2026-10-01", null, null, "4.956878629", "4956.88", "0", "0.00", "4956.88")]
    [InlineData("C7", MonetaryIndexCode.Selic, "50000.00", "2023-01-01", "2024-01-01", null, null, "1.123100000", "56155.00", "0", "0.00", "56155.00")]
    [InlineData("C8", MonetaryIndexCode.Tr, "100000.00", "2023-01-01", "2024-01-01", "2023-01-31", "0.5", "1.017600161", "101760.02", "11.033333", "5613.76", "107373.78")]
    [InlineData("C9", MonetaryIndexCode.IpcaE, "3000.00", "2026-09-05", "2026-09-28", "2026-09-05", "1", "1.000000000", "3000.00", "0.766667", "23.00", "3023.00")]
    [InlineData("C10", MonetaryIndexCode.IpcaE, "7531.29", "2019-06-30", "2024-06-30", "2019-08-31", "1", "1.324273098", "9973.48", "58", "5784.62", "15758.10")]
    public async Task Correction_matches_the_hand_calculated_cases(
        string name,
        MonetaryIndexCode index,
        string principal,
        string correctFrom,
        string until,
        string? interestFrom,
        string? rate,
        string factor,
        string corrected,
        string interestMonths,
        string interest,
        string total)
    {
        await using var provider = Provider();
        await using var scope = provider.CreateAsyncScope();
        var correction = scope.ServiceProvider.GetRequiredService<IMonetaryCorrection>();

        var result = await correction.CalculateAsync(
            new MonetaryCorrectionRequest(Dec(principal), index, Date(correctFrom), Date(until), interestFrom is null ? null : Date(interestFrom), rate is null ? null : Dec(rate)),
            Ct);

        result.IsSuccess.ShouldBeTrue(name);
        result.Value.Factor.ShouldBe(Dec(factor), name);
        result.Value.CorrectedAmount.ShouldBe(Dec(corrected), name);
        result.Value.InterestMonths.ShouldBe(Dec(interestMonths), name);
        result.Value.InterestAmount.ShouldBe(Dec(interest), name);
        result.Value.Total.ShouldBe(Dec(total), name);
        result.Value.DeflationFloorApplied.ShouldBe(name == "C3", name);
    }

    [Fact]
    public async Task The_memory_of_calculation_lists_each_month_and_a_missing_month_is_an_error()
    {
        await using var provider = Provider();
        await using var scope = provider.CreateAsyncScope();
        var correction = scope.ServiceProvider.GetRequiredService<IMonetaryCorrection>();

        var results = await correction.CalculateManyAsync(
            [
                new MonetaryCorrectionRequest(100m, MonetaryIndexCode.Selic, new DateOnly(2023, 1, 1), new DateOnly(2023, 3, 1), null, null),
                new MonetaryCorrectionRequest(100m, MonetaryIndexCode.Inpc, new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 15), null, null),
                new MonetaryCorrectionRequest(100m, (MonetaryIndexCode)99, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), null, null),
            ],
            Ct);

        results[0].Value.Months.Select(m => (m.Month, m.Percent, m.AccumulatedFactor))
            .ShouldBe([(new DateOnly(2023, 1, 1), 1.12m, 1.0112m), (new DateOnly(2023, 2, 1), 0.92m, 1.0204m)]);
        results[1].Error!.Code.ShouldBe("platform.monetary_index_missing");
        results[2].Error!.Code.ShouldBe("platform.monetary_index_invalid");
    }

    [Fact]
    public async Task The_import_fills_only_the_missing_closed_months_and_falls_back_when_a_source_fails()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        await PlatformDatabaseFixture.ScalarOnAsync<int>(connectionString, "DELETE FROM platform.monetary_indexes WHERE month >= DATE '2026-08-01'");

        var failing = new StubSource("bcb", fail: true, MonetaryIndexKind.Selic, MonetaryIndexKind.Tr);
        var backup = new StubSource("ipeadata", fail: false, MonetaryIndexKind.Selic, MonetaryIndexKind.Tr);
        var ibge = new StubSource("ibge", fail: false, MonetaryIndexKind.IpcaE, MonetaryIndexKind.Inpc);

        // 16/10/2026: o último mês fechado é setembro; outubro (o mês corrente) nunca entra.
        var first = await ImportAsync(connectionString, [ibge, failing, backup]);
        first.Imported.Values.ShouldAllBe(count => count == 2);
        backup.Requests.ShouldContain((MonetaryIndexKind.Selic, new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1)));
        failing.Requests.Count.ShouldBe(2);

        var sources = await PlatformDatabaseFixture.ScalarOnAsync<string>(
            connectionString,
            "SELECT string_agg(DISTINCT source, ',' ORDER BY source) FROM platform.monetary_indexes WHERE month >= DATE '2026-08-01'");
        sources.ShouldBe("ibge,ipeadata");

        // A rodada seguinte não acha mês novo: nada muda.
        (await ImportAsync(connectionString, [ibge, failing, backup])).Imported.Values.ShouldAllBe(count => count == 0);
    }

    [Fact]
    public async Task Without_any_working_source_the_import_keeps_the_table_as_it_is()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var summary = await ImportAsync(connectionString, [new StubSource("bcb", fail: true, Enum.GetValues<MonetaryIndexKind>())], new DateTimeOffset(2026, 12, 20, 12, 0, 0, TimeSpan.Zero));

        summary.Imported.Values.ShouldAllBe(count => count == 0);
        (await PlatformDatabaseFixture.ScalarOnAsync<long>(connectionString, "SELECT count(*) FROM platform.monetary_indexes")).ShouldBe(1280);
    }

    private async Task<IndexImportSummary> ImportAsync(string connectionString, IReadOnlyList<IMonetaryIndexSource> sources, DateTimeOffset? now = null)
    {
        await using var provider = Provider(connectionString, now);
        await using var scope = provider.CreateAsyncScope();
        var importer = ActivatorUtilities.CreateInstance<MonetaryIndexImporter>(scope.ServiceProvider, sources.AsEnumerable());
        return await importer.ImportAsync(Ct);
    }

    private static decimal Dec(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static DateOnly Date(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    /// <summary>Fonte de teste: devolve 0,50% para cada mês pedido, ou falha como uma rede fora do ar.</summary>
    private sealed class StubSource(string name, bool fail, params MonetaryIndexKind[] kinds) : IMonetaryIndexSource
    {
        public List<(MonetaryIndexKind Kind, DateOnly First, DateOnly Last)> Requests { get; } = [];

        public string Name => name;

        public bool Supports(MonetaryIndexKind kind) => kinds.Contains(kind);

        public Task<IReadOnlyList<IndexValue>> FetchAsync(MonetaryIndexKind kind, DateOnly firstMonth, DateOnly lastMonth, CancellationToken cancellationToken)
        {
            Requests.Add((kind, firstMonth, lastMonth));
            if (fail)
            {
                throw new HttpRequestException("fora do ar");
            }

            var values = new List<IndexValue>();
            for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
            {
                values.Add(new IndexValue(month, 0.5m));
            }

            // Um mês fora do intervalo (ex.: o corrente, ainda parcial) é ignorado.
            values.Add(new IndexValue(lastMonth.AddMonths(1), 0.3m));
            return Task.FromResult<IReadOnlyList<IndexValue>>(values);
        }
    }
}
