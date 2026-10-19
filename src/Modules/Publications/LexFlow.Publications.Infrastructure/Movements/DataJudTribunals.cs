using LexFlow.Platform.Contracts.Courts;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Publications.Infrastructure.Movements;

internal sealed class DataJudTribunals(ICourtDirectory courts)
{
    private static readonly HashSet<string> Aliases = new(
        ("stj tst tse trf1 trf2 trf3 trf4 trf5 trf6 " +
         "tjac tjal tjam tjap tjba tjce tjdft tjes tjgo tjma tjmg tjms tjmt tjpa tjpb tjpe tjpi tjpr tjrj tjrn tjro tjrr tjrs tjsc tjse tjsp tjto " +
         "tjmmg tjmrs tjmsp " + string.Join(' ', Enumerable.Range(1, 24).Select(n => "trt" + n.ToString(System.Globalization.CultureInfo.InvariantCulture))) + " " +
         "tre-ac tre-al tre-am tre-ap tre-ba tre-ce tre-dft tre-es tre-go tre-ma tre-mg tre-ms tre-mt tre-pa tre-pb tre-pe tre-pi tre-pr tre-rj tre-rn tre-ro tre-rr tre-rs tre-sc tre-se tre-sp tre-to")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    public async Task<Result<string>> ResolveAsync(CnjNumber cnj, CancellationToken cancellationToken)
    {
        var court = await courts.FindAsync(cnj.Segment, cnj.Court, cancellationToken);
        var alias = court?.Acronym.ToLowerInvariant();
        if (string.Equals(alias, "tre-df", StringComparison.Ordinal)) { alias = "tre-dft"; }
        return alias is not null && Aliases.Contains(alias) ? alias :
            Error.BusinessRule("publications.tribunal_unsupported", "Tribunal sem endpoint público DataJud cadastrado.");
    }
}
