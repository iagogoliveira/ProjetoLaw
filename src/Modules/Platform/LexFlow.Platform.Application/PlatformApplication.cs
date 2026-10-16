namespace LexFlow.Platform.Application;

/// <summary>Referência ao assembly Application do módulo, para o registro por varredura (<c>AddApplication</c>).</summary>
public static class PlatformApplication
{
    public static readonly System.Reflection.Assembly Assembly = typeof(PlatformApplication).Assembly;
}
