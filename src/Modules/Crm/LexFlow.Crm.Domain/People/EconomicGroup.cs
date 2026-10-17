using System.Globalization;
using System.Text;
using LexFlow.SharedKernel.Domain;

namespace LexFlow.Crm.Domain.People;

/// <summary>
/// Grupo econômico (RF-06): cliente matriz com filiais e outros CNPJs vinculados, para consolidar carteira e
/// faturamento e para o conflito de interesses indireto. Os membros são as pessoas com <c>EconomicGroupId</c>.
/// </summary>
public sealed class EconomicGroup : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxNameLength = 200;

    private EconomicGroup()
    {
    }

    private EconomicGroup(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static EconomicGroup Create(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new EconomicGroup(id, name.Trim());
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
}

/// <summary>
/// Texto de busca: sem acentos, em minúsculas e com um espaço entre as palavras ("João  da SILVA" → "joao da silva").
/// É o mesmo para o nome gravado e para o termo pesquisado, então "Joao da Silva" encontra "João da Silva Ltda.".
/// </summary>
public static class SearchText
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true;
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().TrimEnd();
    }
}
