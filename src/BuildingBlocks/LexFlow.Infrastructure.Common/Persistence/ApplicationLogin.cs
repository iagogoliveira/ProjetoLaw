using Npgsql;

namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>Login com que a API e o Worker conectam (seção <c>Database:ApplicationLogin</c>; a senha vem do cofre).</summary>
public sealed class ApplicationLoginSettings
{
    public const string SectionName = "Database:ApplicationLogin";

    public string? Username { get; init; }

    public string? Password { get; init; }
}

/// <summary>
/// Cria ou atualiza o login da aplicação como membro de <see cref="DatabaseSecurity.ApplicationRole"/>, sempre sem
/// superusuário, sem <c>BYPASSRLS</c> e sem criar papéis ou bancos. Roda com a conexão das migrations (dono). O DDL é
/// montado pelo próprio PostgreSQL com <c>format(%I, %L)</c>, que cita nome e senha: nada é concatenado no C#.
/// </summary>
public static class ApplicationLogin
{
    public static async Task EnsureAsync(NpgsqlConnection admin, string username, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admin);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await using var build = new NpgsqlCommand(
            $"""
            SELECT CASE WHEN EXISTS (SELECT FROM pg_roles WHERE rolname = $1)
                THEN format('ALTER ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE', $1, $2)
                ELSE format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE', $1, $2)
            END,
            format('GRANT {DatabaseSecurity.ApplicationRole} TO %I', $1)
            """,
            admin);
        build.Parameters.AddWithValue(username);
        build.Parameters.AddWithValue(password);

        var statements = new List<string>();
        await using (var reader = await build.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            statements.Add(reader.GetString(0));
            statements.Add(reader.GetString(1));
        }

        foreach (var statement in statements)
        {
            await using var command = new NpgsqlCommand(statement, admin);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
