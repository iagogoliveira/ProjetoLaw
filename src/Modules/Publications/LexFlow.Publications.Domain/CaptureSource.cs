using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Publications.Domain;

public enum CaptureSourceKind { Oab, Name, Cnpj }

public sealed record CaptureSourceInput(CaptureSourceKind Kind, string Label, string Value, bool IsActive = true);

public sealed class CaptureSource : Entity<Guid>, ITenantOwned, IAuditable
{
    private CaptureSource() { }
    public Guid OrganizationId { get; private set; }
    public CaptureSourceKind Kind { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<CaptureSource> Create(CaptureSourceInput input)
    {
        var source = new CaptureSource { Id = Guid.CreateVersion7() };
        var result = source.Update(input);
        return result.IsFailure ? result.Error : source;
    }

    public Result Update(CaptureSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Kind) || string.IsNullOrWhiteSpace(input.Label) || input.Label.Trim().Length > 200
            || string.IsNullOrWhiteSpace(input.Value) || input.Value.Trim().Length > 200)
        {
            return PublicationErrors.InvalidSource;
        }

        var value = input.Value.Trim();
        if (input.Kind == CaptureSourceKind.Oab)
        {
            var oab = OabNumber.Parse(value);
            if (oab.IsFailure) { return PublicationErrors.InvalidSource; }
            value = oab.Value.Formatted;
        }

        if (input.Kind == CaptureSourceKind.Cnpj)
        {
            var cnpj = Cnpj.Create(value);
            if (cnpj.IsFailure) { return PublicationErrors.InvalidSource; }
            value = cnpj.Value.Value;
        }

        Kind = input.Kind;
        Label = input.Label.Trim();
        Value = value;
        IsActive = input.IsActive;
        return Result.Success;
    }
}
