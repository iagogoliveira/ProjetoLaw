using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LexFlow.ArchitectureTests;

/// <summary>
/// Procura no IL chamadas a APIs proibidas pelo CLAUDE.md. Complementa o BannedApiAnalyzers (BannedSymbols.txt):
/// mesmo que um projeto desligue o analisador, o teste falha.
/// </summary>
internal static class BannedApiScanner
{
    /// <summary>Membros proibidos em todo o código de produção: "TipoDeclarante::NomeDoMembro".</summary>
    private static readonly HashSet<string> BannedEverywhere = new(StringComparer.Ordinal)
    {
        "System.DateTime::get_Now",
        "System.DateTime::get_UtcNow",
        "System.DateTime::get_Today",
        "System.DateTimeOffset::get_Now",
        "System.DateTimeOffset::get_UtcNow",
        "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions::FromSqlRaw",
        "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions::ExecuteSqlRaw",
        "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions::ExecuteSqlRawAsync",
    };

    /// <summary>Permitido só no módulo Platform.</summary>
    private const string IgnoreQueryFilters = "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions::IgnoreQueryFilters";

    /// <summary>Retorna "Tipo.Método chama Membro" para cada uso proibido nos tipos cujo namespace começa com <paramref name="namespacePrefix"/>.</summary>
    public static IReadOnlyList<string> FindViolations(Assembly assembly, string namespacePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var allowIgnoreQueryFilters = assembly.GetName().Name?.StartsWith("LexFlow.Platform.", StringComparison.Ordinal) == true;
        using var module = ModuleDefinition.ReadModule(assembly.Location);

        return [.. module.GetTypes()
            .Where(t => t.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal))
            .SelectMany(t => t.Methods.Where(m => m.HasBody).Select(m => (Type: t, Method: m)))
            .SelectMany(x => x.Method.Body.Instructions
                .Where(i => i.OpCode.Code is Code.Call or Code.Callvirt or Code.Newobj or Code.Ldftn)
                .Select(i => i.Operand)
                .OfType<MethodReference>()
                .Select(target => $"{target.DeclaringType.FullName}::{target.Name}")
                .Where(member => BannedEverywhere.Contains(member) || (!allowIgnoreQueryFilters && string.Equals(member, IgnoreQueryFilters, StringComparison.Ordinal)))
                .Select(member => $"{x.Type.FullName}.{x.Method.Name} chama {member}"))
            .Distinct(StringComparer.Ordinal)];
    }
}
