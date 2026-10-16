using LexFlow.Web.Common.Pagination;

namespace LexFlow.BuildingBlocks.UnitTests.Web;

public sealed class CursorPaginationTests
{
    private sealed record Position(DateTimeOffset CreatedAt, Guid Id);

    [Fact]
    public void Cursor_round_trips_the_position()
    {
        var position = new Position(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero), Guid.CreateVersion7());

        var cursor = CursorCodec.Encode(position);

        cursor.ShouldNotContain("+");
        cursor.ShouldNotContain("/");
        CursorCodec.Decode<Position>(cursor).Value.ShouldBe(position);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não-é-base64!")]
    [InlineData("bm90IGpzb24")] // "not json"
    [InlineData("bnVsbA")] // "null"
    public void Tampered_cursor_is_a_validation_error(string cursor) =>
        CursorCodec.Decode<Position>(cursor).Error.ShouldBe(CursorCodec.InvalidCursor);

    [Fact]
    public void Oversized_cursor_is_rejected() =>
        CursorCodec.Decode<Position>(new string('a', 2000)).Error.ShouldBe(CursorCodec.InvalidCursor);

    [Theory]
    [InlineData(null, 50)]
    [InlineData(1, 1)]
    [InlineData(200, 200)]
    public void Limit_defaults_to_50_and_accepts_up_to_200(int? limit, int expected) =>
        CursorPageRequest.Create(null, limit).Value.Limit.ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void Limit_out_of_range_is_rejected(int limit) =>
        CursorPageRequest.Create(null, limit).Error.ShouldBe(CursorPageRequest.LimitOutOfRange);
}
