using LexFlow.Application.Abstractions;
using LexFlow.Publications.Application.Movements;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Capture;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Infrastructure.Movements;

internal sealed class ConfiguredMovementProvider(IMovementPageProvider dataJud, IMovementSecrets secrets, IClock clock, FakeCaptureOptions fake) : IConfiguredMovementProvider
{
    public Task<Result<MovementPage>> FetchAsync(MovementIntegration configuration, MovementQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Provider == MovementProviderKind.Fake)
        {
            Result<MovementPage> result = fake.Available ? new MovementPage("fake", fake.Empty ? [] :
                [new CapturedMovement(query.Cnj + ":fake:" + clock.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture),
                    new DateTimeOffset(clock.Today.ToDateTime(new TimeOnly(0, 0)), TimeSpan.Zero), "Movimentação simulada")], null) : FakePublicationProvider.Unavailable;
            return Task.FromResult(result);
        }
        if (configuration.Provider != MovementProviderKind.DataJud || configuration.ProtectedApiKey is null)
        { return Task.FromResult<Result<MovementPage>>(FakePublicationProvider.Unavailable); }
        var key = secrets.Unprotect(configuration.ProtectedApiKey);
        return key.IsFailure ? Task.FromResult<Result<MovementPage>>(key.Error) : dataJud.FetchAsync(query, key.Value, cancellationToken);
    }
}
