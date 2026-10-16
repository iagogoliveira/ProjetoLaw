using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Platform.Domain.Courts;

/// <summary>
/// Tribunal ou órgão identificado na numeração única do CNJ (Res. 65/2008): segmento <c>J</c> + tribunal <c>TR</c>.
/// Dado de referência mantido pela plataforma (sem tenant).
/// </summary>
public sealed class Court : Entity<Guid>
{
    private Court()
    {
    }

    public Court(Guid id, int segment, string code, string acronym, string name, string? state)
        : base(id)
    {
        Segment = segment;
        Code = code;
        Acronym = acronym;
        Name = name;
        State = state;
    }

    /// <summary>Segmento do Judiciário (J): 1 a 9.</summary>
    public int Segment { get; private set; }

    /// <summary>Código do tribunal (TR) com 2 dígitos.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Acronym { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>UF da sede, quando houver (nulo nos tribunais superiores e conselhos).</summary>
    public string? State { get; private set; }

    public bool Matches(CnjNumber number)
    {
        ArgumentNullException.ThrowIfNull(number);
        return number.Segment == Segment && string.Equals(number.Court, Code, StringComparison.Ordinal);
    }
}
