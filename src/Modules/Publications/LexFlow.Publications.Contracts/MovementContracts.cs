using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Contracts;

public sealed record MovementQuery(string Cnj, string? Cursor);
public sealed record MovementPage(string Tribunal, IReadOnlyList<CapturedMovement> Items, string? NextCursor);

/// <summary>Uma página de documentos do processo; inclui todo o histórico presente em cada documento.</summary>
public interface IMovementPageProvider
{
    Task<Result<MovementPage>> FetchAsync(MovementQuery query, string apiKey, CancellationToken cancellationToken);
}
