// Violações plantadas para BannedApiTests.Scanner_detects_planted_violations. Fora do prefixo LexFlow.
#pragma warning disable RS0030 // uso proposital de API proibida
namespace ArchFixture.Banned;

internal static class UsesSystemClock
{
    public static DateTime Local() => DateTime.Now;

    public static DateTimeOffset Utc() => DateTimeOffset.UtcNow;
}
