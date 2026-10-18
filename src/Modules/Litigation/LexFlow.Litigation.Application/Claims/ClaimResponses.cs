using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Claims;
using LexFlow.Platform.Contracts.Indexes;

namespace LexFlow.Litigation.Application.Claims;

/// <summary>
/// Monta as respostas dos pedidos. Com a data de atualização, corrige cada valor pela configuração do pedido com o
/// <see cref="IMonetaryCorrection"/> do Platform, numa chamada só para todos os valores.
/// </summary>
internal sealed class ClaimResponseBuilder(IMonetaryCorrection correction)
{
    public async Task<IReadOnlyList<ClaimResponse>> BuildAsync(IReadOnlyList<Claim> claims, DateOnly? updateTo, CancellationToken cancellationToken)
    {
        var requests = new List<MonetaryCorrectionRequest>();
        var slots = new Dictionary<(Guid Claim, CaseInstance Instance, ClaimValueKind Kind), int>();
        if (updateTo is { } until)
        {
            foreach (var claim in claims.Where(c => c.CorrectionIndex is not null))
            {
                foreach (var valuation in claim.Valuations)
                {
                    foreach (var kind in Enum.GetValues<ClaimValueKind>())
                    {
                        if (valuation.Amount(kind) is { } amount)
                        {
                            slots[(claim.Id, valuation.Instance, kind)] = requests.Count;
                            requests.Add(Request(claim, amount, until));
                        }
                    }
                }
            }
        }

        var results = requests.Count == 0 ? [] : await correction.CalculateManyAsync(requests, cancellationToken);
        return [.. claims.Select(claim => Response(claim, slots, results))];
    }

    /// <summary>Soma por instância; os totais atualizados existem só quando a atualização foi pedida.</summary>
    public static IReadOnlyList<ClaimTotalsResponse> Totals(IReadOnlyList<ClaimResponse> items, bool updated) =>
        [.. items.SelectMany(i => i.Valuations)
            .GroupBy(v => v.Instance)
            .OrderBy(g => g.Key)
            .Select(g => new ClaimTotalsResponse(
                g.Key,
                g.Sum(v => v.Requested ?? 0m),
                g.Sum(v => v.Estimated ?? 0m),
                g.Sum(v => v.Awarded ?? 0m),
                updated ? g.Sum(v => v.UpdatedRequested ?? v.Requested ?? 0m) : null,
                updated ? g.Sum(v => v.UpdatedEstimated ?? v.Estimated ?? 0m) : null,
                updated ? g.Sum(v => v.UpdatedAwarded ?? v.Awarded ?? 0m) : null))];

    internal static MonetaryIndexCode ToCode(ClaimCorrectionIndex index) => index switch
    {
        ClaimCorrectionIndex.IpcaE => MonetaryIndexCode.IpcaE,
        ClaimCorrectionIndex.Inpc => MonetaryIndexCode.Inpc,
        ClaimCorrectionIndex.Selic => MonetaryIndexCode.Selic,
        _ => MonetaryIndexCode.Tr,
    };

    private static MonetaryCorrectionRequest Request(Claim claim, decimal amount, DateOnly until) =>
        new(amount, ToCode(claim.CorrectionIndex!.Value), claim.CorrectFrom!.Value, until, claim.InterestFrom, claim.MonthlyInterestPercent);

    private static ClaimResponse Response(
        Claim claim,
        Dictionary<(Guid Claim, CaseInstance Instance, ClaimValueKind Kind), int> slots,
        IReadOnlyList<SharedKernel.Results.Result<MonetaryCorrectionResult>> results)
    {
        var failure = slots.Where(s => s.Key.Claim == claim.Id).Select(s => results[s.Value]).FirstOrDefault(r => r.IsFailure);

        decimal? Updated(ClaimValuation valuation, ClaimValueKind kind) =>
            failure is null && slots.TryGetValue((claim.Id, valuation.Instance, kind), out var slot) ? results[slot].Value.Total : null;

        var valuations = claim.Valuations.OrderBy(v => v.Instance)
            .Select(v => new ClaimValuationResponse(
                v.Instance,
                v.Requested,
                v.Estimated,
                v.Awarded,
                Updated(v, ClaimValueKind.Requested),
                Updated(v, ClaimValueKind.Estimated),
                Updated(v, ClaimValueKind.Awarded)))
            .ToList();
        var settings = claim.CorrectionIndex is { } index
            ? new CorrectionSettings(index, claim.CorrectFrom!.Value, claim.InterestFrom, claim.MonthlyInterestPercent)
            : null;
        return new ClaimResponse(claim.Id, claim.LegalCaseId, claim.ClaimTypeId, claim.Description, settings, valuations, failure?.Error?.Code, claim.CreatedAt);
    }
}
