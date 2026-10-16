using System.Reflection;
using LexFlow.Application.Abstractions.Messaging;

namespace LexFlow.ArchitectureTests;

/// <summary>Seção 13.1: handlers são <c>internal sealed</c> (só o pipeline os chama, nunca outro módulo diretamente).</summary>
public sealed class HandlerConventionTests
{
    private static readonly Type[] HandlerInterfaces = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    [Fact]
    public void Handlers_are_internal_and_sealed()
    {
        var violations = LexFlowAssemblies.All.SelectMany(a => FindViolations(a, LexFlowAssemblies.Prefix)).ToList();

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Rule_detects_public_or_unsealed_handlers()
    {
        var violations = FindViolations(typeof(HandlerConventionTests).Assembly, "ArchFixture.Handlers");

        violations.ShouldBe(["ArchFixture.Handlers.PublicHandler", "ArchFixture.Handlers.UnsealedHandler"], ignoreOrder: true);
    }

    internal static IReadOnlyList<string> FindViolations(Assembly assembly, string namespacePrefix) =>
        [.. assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            .Where(t => t.IsPublic || t.IsNestedPublic || !t.IsSealed)
            .Select(t => t.FullName!)];
}
