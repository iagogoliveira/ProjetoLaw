using LexFlow.Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Identity.Infrastructure.Persistence;

/// <summary><see cref="IUserDirectory"/> sobre <c>identity.users</c>, só leitura e só os campos públicos.</summary>
internal sealed class UserDirectory(IdentityModuleDbContext db) : IUserDirectory
{
    public Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.Email != null)
            .Select(u => new UserSummary(u.Id, u.Email!, u.FullName, u.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => new UserSummary(u.Id, u.Email!, u.FullName, u.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UserSummary>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.Email != null)
            .Select(u => new UserSummary(u.Id, u.Email!, u.FullName, u.EmailConfirmed))
            .ToListAsync(cancellationToken);
}
