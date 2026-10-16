using LexFlow.Identity.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Identity.Application.Abstractions;

/// <summary>
/// Acesso aos dados do módulo pelos handlers (implementado pelo <c>IdentityModuleDbContext</c>). Os usuários são
/// lidos e gravados pelo <c>UserManager</c> do ASP.NET Core Identity, sobre o mesmo contexto.
/// </summary>
public interface IIdentityDbContext
{
    DbSet<Session> Sessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
