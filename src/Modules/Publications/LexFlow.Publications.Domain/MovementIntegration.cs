using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Domain;

public enum MovementProviderKind { Disabled, Fake, DataJud }

public sealed class MovementIntegration : Entity<Guid>, ITenantOwned, IAuditable
{
    private MovementIntegration() { }
    public Guid OrganizationId { get; private set; }
    public MovementProviderKind Provider { get; private set; }
    public Guid Revision { get; private set; }
    [Sensitive]
    public string? ProtectedApiKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public static MovementIntegration Create() => new() { Id = Guid.CreateVersion7(), Revision = Guid.CreateVersion7() };

    public Result Configure(MovementProviderKind provider, string? protectedApiKey)
    {
        if (!Enum.IsDefined(provider) || (provider == MovementProviderKind.DataJud && string.IsNullOrWhiteSpace(protectedApiKey)))
        {
            return Error.Validation("publications.integration_invalid", "DataJud exige uma chave de acesso válida.");
        }
        Provider = provider;
        ProtectedApiKey = protectedApiKey;
        Revision = Guid.CreateVersion7();
        return Result.Success;
    }
}
