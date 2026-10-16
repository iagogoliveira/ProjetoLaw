using System.Reflection;

namespace LexFlow.ArchitectureTests;

internal sealed record ModuleAssembly(Assembly Assembly, string Module, ModuleLayer Layer)
{
    public string Name => LexFlowAssemblies.Name(Assembly);
}
