using LexFlow.Publications.Contracts;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Infrastructure.Capture;

public sealed class FakeCaptureOptions
{
    public bool Available { get; set; } = true;
    public bool Empty { get; set; }
    public string Cnj { get; set; } = "2238058-98.2025.8.26.8434";
}

public sealed class FakePublicationProvider(FakeCaptureOptions options) : IPublicationProvider, IMovementProvider
{
    public static readonly Error Unavailable = Error.BusinessRule("publications.provider_unavailable", "Provedor de captura indisponível.");
    public Task<Result<IReadOnlyList<CapturedPublication>>> FetchAsync(CaptureQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Result<IReadOnlyList<CapturedPublication>> result = !options.Available ? Unavailable : options.Empty ? Array.Empty<CapturedPublication>() :
            new List<CapturedPublication>
            {
                new("Diário simulado", query.Day, query.Day, $"Intimação do processo {options.Cnj}. Fonte {query.Kind}: {query.Value}."),
                new("Diário simulado", query.Day, query.Day, $"Expediente sem CNJ. Fonte {query.Kind}: {query.Value}."),
            };
        return Task.FromResult(result);
    }
    public Task<Result<IReadOnlyList<CapturedMovement>>> FetchAsync(string cnj, DateOnly since, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Result<IReadOnlyList<CapturedMovement>> result = !options.Available ? Unavailable : options.Empty ? Array.Empty<CapturedMovement>() :
            new List<CapturedMovement> { new(cnj + ":" + since.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture),
                new DateTimeOffset(since.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero), "Movimentação simulada") };
        return Task.FromResult(result);
    }
}
internal sealed class DisabledPublicationProvider : IPublicationProvider, IMovementProvider
{
    public Task<Result<IReadOnlyList<CapturedPublication>>> FetchAsync(CaptureQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<IReadOnlyList<CapturedPublication>>>(FakePublicationProvider.Unavailable);
    public Task<Result<IReadOnlyList<CapturedMovement>>> FetchAsync(string cnj, DateOnly since, CancellationToken cancellationToken) =>
        Task.FromResult<Result<IReadOnlyList<CapturedMovement>>>(FakePublicationProvider.Unavailable);
}
