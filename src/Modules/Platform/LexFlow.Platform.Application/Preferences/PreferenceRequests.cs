using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Domain.Preferences;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Preferences;

/// <summary>Uma preferência de tela do usuário: a chave e o valor JSON.</summary>
public sealed record PreferenceResponse(string Key, JsonElement Value, DateTimeOffset UpdatedAt);

public sealed record GetPreferenceQuery(string Key) : IQuery<PreferenceResponse>;

public sealed record SetPreferenceCommand(string Key, JsonElement Value) : ICommand<PreferenceResponse>;

internal sealed class GetPreferenceHandler(IPlatformDbContext db, ICurrentUser user) : IQueryHandler<GetPreferenceQuery, PreferenceResponse>
{
    public async Task<Result<PreferenceResponse>> Handle(GetPreferenceQuery query, CancellationToken cancellationToken)
    {
        if (!UserPreference.IsValidKey(query.Key))
        {
            return UserPreference.KeyInvalid;
        }

        var userId = user.UserId ?? throw new InvalidOperationException("Preferência sem usuário autenticado.");
        var preference = await db.Preferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId && p.Key == query.Key, cancellationToken);
        return preference is null ? UserPreference.NotFound : preference.ToResponse();
    }
}

/// <summary>Cria ou troca a preferência do usuário no escritório ativo.</summary>
internal sealed class SetPreferenceHandler(IPlatformDbContext db, ICurrentUser user, IIdGenerator ids, IClock clock) : ICommandHandler<SetPreferenceCommand, PreferenceResponse>
{
    public async Task<Result<PreferenceResponse>> Handle(SetPreferenceCommand command, CancellationToken cancellationToken)
    {
        if (command.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return UserPreference.ValueInvalid;
        }

        var userId = user.UserId ?? throw new InvalidOperationException("Preferência sem usuário autenticado.");
        var json = command.Value.GetRawText();
        var preference = await db.Preferences.SingleOrDefaultAsync(p => p.UserId == userId && p.Key == command.Key, cancellationToken);
        if (preference is null)
        {
            var created = UserPreference.Create(ids.NewId(), userId, command.Key, json, clock.UtcNow);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            preference = created.Value;
            db.Preferences.Add(preference);
        }
        else
        {
            var set = preference.Set(json, clock.UtcNow);
            if (set.IsFailure)
            {
                return set.Error!;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return preference.ToResponse();
    }
}

internal static class PreferenceMapping
{
    public static PreferenceResponse ToResponse(this UserPreference p)
    {
        using var document = JsonDocument.Parse(p.Value);
        return new PreferenceResponse(p.Key, document.RootElement.Clone(), p.UpdatedAt);
    }
}
