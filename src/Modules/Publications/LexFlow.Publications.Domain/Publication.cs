using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Publications.Domain;

public enum PublicationStatus { Pending, Triaged }

public sealed record PublicationInput(Guid SourceId, string Journal, DateOnly AvailableOn, DateOnly PublishedOn, string Text);

public sealed partial class Publication : Entity<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    private Publication() { }
    public Guid OrganizationId { get; private set; }
    public Guid SourceId { get; private set; }
    public string Journal { get; private set; } = string.Empty;
    public DateOnly AvailableOn { get; private set; }
    public DateOnly PublishedOn { get; private set; }
    [NotAudited]
    public string Text { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public string? ExtractedCnj { get; private set; }
    public string[] CandidateCnjs { get; private set; } = [];
    public Guid? LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public PublicationStatus Status { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<Publication> Receive(PublicationInput input, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.SourceId == Guid.Empty || string.IsNullOrWhiteSpace(input.Journal) || input.Journal.Trim().Length > 200
            || string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 200_000 || input.AvailableOn == default
            || input.PublishedOn < input.AvailableOn)
        {
            return PublicationErrors.InvalidPublication;
        }

        var normalized = Whitespace().Replace(input.Text.Normalize(NormalizationForm.FormKC), " ").Trim().ToUpperInvariant();
        var key = input.Journal.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant() + "\n"
            + input.PublishedOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "\n" + normalized;
        return new Publication
        {
            Id = Guid.CreateVersion7(),
            SourceId = input.SourceId,
            Journal = input.Journal.Trim(),
            AvailableOn = input.AvailableOn,
            PublishedOn = input.PublishedOn,
            Text = input.Text,
            Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))),
            ExtractedCnj = ExtractCnj(input.Text),
            CandidateCnjs = ExtractCandidates(input.Text),
            ReceivedAt = receivedAt.ToUniversalTime(),
        };
    }

    /// <summary>Só um CNJ válido distinto permite associação automática; ambiguidade permanece para conferência.</summary>
    public static string? ExtractCnj(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var numbers = ExtractCandidates(text);
        return numbers.Length == 1 ? numbers[0] : null;
    }

    private static string[] ExtractCandidates(string text) => CnjPattern().Matches(text).Select(m => CnjNumber.Create(m.Value))
        .Where(r => r.IsSuccess).Select(r => r.Value.Digits).Distinct(StringComparer.Ordinal).ToArray();

    public Result Link(Guid caseId)
    {
        if (caseId == Guid.Empty) { return PublicationErrors.InvalidPublication; }
        LegalCaseId = caseId;
        return Result.Success;
    }

    [GeneratedRegex(@"(?<![0-9])[0-9]{7}-?[0-9]{2}\.?[0-9]{4}\.?[0-9]\.?[0-9]{2}\.?[0-9]{4}(?![0-9])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CnjPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Whitespace();
}
