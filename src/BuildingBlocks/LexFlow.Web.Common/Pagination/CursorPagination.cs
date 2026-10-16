using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Web.Common.Pagination;

/// <summary>Página de uma listagem por cursor (seção 4.4). <see cref="NextCursor"/> nulo indica a última página.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

/// <summary>Parâmetros <c>?cursor=&amp;limit=</c> já validados.</summary>
public sealed record CursorPageRequest(string? Cursor, int Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static readonly Error LimitOutOfRange =
        Error.Validation("pagination.limit_out_of_range", $"O limite deve estar entre 1 e {MaxLimit}.");

    public static Result<CursorPageRequest> Create(string? cursor, int? limit)
    {
        var value = limit ?? DefaultLimit;
        return value is < 1 or > MaxLimit ? LimitOutOfRange : new CursorPageRequest(cursor, value);
    }
}

/// <summary>
/// Cursor opaco: posição serializada em JSON e codificada em Base64Url. Não é segredo (a consulta sempre
/// filtra por tenant), mas é validado para que cursores adulterados virem 400 e não exceção.
/// </summary>
public static class CursorCodec
{
    public static readonly Error InvalidCursor = Error.Validation("pagination.invalid_cursor", "Cursor inválido.");

    public static string Encode<T>(T position) => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(position));

    public static Result<T> Decode<T>(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 1024)
        {
            return InvalidCursor;
        }

        try
        {
            var bytes = Base64Url.DecodeFromChars(cursor);
            return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytes)) is { } position
                ? position
                : InvalidCursor;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return InvalidCursor;
        }
    }
}
