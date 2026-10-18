using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Domain.Numbering;

/// <summary>
/// Numeração interna dos processos do escritório (Etapa 5, tarefa 2): o formato com os marcadores <c>{AREA}</c>,
/// <c>{ANO}</c> e <c>{SEQ:00000}</c> e a sigla de cada área. A sequência é contada por área e ano (decisão de
/// 2026-10-18), num contador por organização incrementado na mesma transação do cadastro.
/// </summary>
public sealed partial class CaseNumbering : Entity<Guid>, ITenantOwned, IAuditable
{
    public const string DefaultFormat = "{AREA}-{ANO}-{SEQ:00000}";
    public const int MaxFormatLength = 60;

    /// <summary>Sigla usada quando o processo não tem área.</summary>
    public const string NoAreaCode = "GERAL";

    private CaseNumbering()
    {
    }

    private CaseNumbering(Guid id)
        : base(id)
    {
    }

    public Guid OrganizationId { get; private set; }

    public string Format { get; private set; } = DefaultFormat;

    /// <summary>Siglas das áreas por id (JSON); a área sem sigla usa a derivada do nome.</summary>
    public string AreaCodes { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    [GeneratedRegex(@"\{(?<name>[A-Z]+)(:(?<pad>0+))?\}", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Token();

    [GeneratedRegex("^[A-Z0-9]{1,10}$", RegexOptions.None, 1000)]
    private static partial Regex AreaCode();

    public static CaseNumbering Default(Guid id) => new(id);

    public IReadOnlyDictionary<Guid, string> Codes =>
        JsonSerializer.Deserialize<Dictionary<Guid, string>>(AreaCodes) ?? [];

    /// <summary>Troca o formato e as siglas (o que já foi numerado não muda).</summary>
    public Result Configure(string format, IReadOnlyDictionary<Guid, string> areaCodes)
    {
        ArgumentNullException.ThrowIfNull(areaCodes);
        var trimmed = (format ?? string.Empty).Trim();
        var tokens = Token().Matches(trimmed).Select(m => m.Groups["name"].Value).ToList();
        if (trimmed.Length == 0 || trimmed.Length > MaxFormatLength
            || tokens.Count(t => string.Equals(t, "SEQ", StringComparison.Ordinal)) != 1
            || tokens.Any(t => t is not ("AREA" or "ANO" or "SEQ"))
            || Token().Replace(trimmed, string.Empty).Contains('{', StringComparison.Ordinal))
        {
            return LitigationErrors.NumberingFormatInvalid;
        }

        var codes = areaCodes.ToDictionary(c => c.Key, c => c.Value.Trim().ToUpperInvariant());
        if (codes.Values.Any(c => !AreaCode().IsMatch(c)))
        {
            return LitigationErrors.AreaCodeInvalid;
        }

        Format = trimmed;
        AreaCodes = JsonSerializer.Serialize(codes);
        return Result.Success;
    }

    /// <summary>A sigla da área: a configurada, ou as quatro primeiras letras do nome sem acentos.</summary>
    public string CodeOf(Guid? areaId, string? areaName)
    {
        if (areaId is null)
        {
            return NoAreaCode;
        }

        if (Codes.TryGetValue(areaId.Value, out var code))
        {
            return code;
        }

        var letters = new string([.. (areaName ?? string.Empty).Normalize(NormalizationForm.FormD).Where(char.IsAsciiLetterOrDigit)]).ToUpperInvariant();
        return letters.Length == 0 ? NoAreaCode : letters[..Math.Min(4, letters.Length)];
    }

    /// <summary>A pasta interna com a sigla, o ano e o número da sequência.</summary>
    public string Render(string areaCode, int year, long sequence) =>
        Token().Replace(Format, m => m.Groups["name"].Value switch
        {
            "AREA" => areaCode,
            "ANO" => year.ToString(CultureInfo.InvariantCulture),
            _ => sequence.ToString(m.Groups["pad"].Success ? m.Groups["pad"].Value : "0", CultureInfo.InvariantCulture),
        });
}
