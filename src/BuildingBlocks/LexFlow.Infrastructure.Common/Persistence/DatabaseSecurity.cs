namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>
/// SQL das migrations de segurança do banco (seção 6.2, item 5). A aplicação conecta com um login membro do papel
/// <see cref="ApplicationRole"/>: sem <c>BYPASSRLS</c>, sem ser dono das tabelas e só com DML. As migrations rodam com
/// o papel dono, que precisa de <c>CREATEROLE</c> e <c>BYPASSRLS</c> (funções de plataforma entre tenants). Os nomes
/// vêm de constantes do código, nunca de entrada externa.
/// </summary>
public static class DatabaseSecurity
{
    /// <summary>Papel de grupo (sem login) com os privilégios da aplicação.</summary>
    public const string ApplicationRole = "lexflow_app";

    /// <summary>Tenant da sessão, gravado pelo interceptor; nulo sem tenant (então nenhuma linha de escritório passa).</summary>
    public const string CurrentOrganizationFunction = "platform.current_organization_id()";

    /// <summary>Cria o papel de grupo se não existir (o papel é do cluster e pode vir de outro banco ou de outra migration paralela).</summary>
    public static string CreateApplicationRole() => $"""
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{ApplicationRole}') THEN
                CREATE ROLE {ApplicationRole} NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
        EXCEPTION WHEN duplicate_object OR unique_violation THEN
            NULL;
        END
        $$;
        """;

    /// <summary>DML nas tabelas do schema (atuais e futuras), nunca no histórico de migrations (quando o schema tiver um).</summary>
    public static string GrantSchema(string schema) => $"""
        GRANT USAGE ON SCHEMA {schema} TO {ApplicationRole};
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {schema} TO {ApplicationRole};
        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {schema} TO {ApplicationRole};
        ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {ApplicationRole};
        ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} GRANT USAGE, SELECT ON SEQUENCES TO {ApplicationRole};
        DO $$
        BEGIN
            IF to_regclass('{schema}.__ef_migrations_history') IS NOT NULL THEN
                REVOKE ALL ON {schema}.__ef_migrations_history FROM {ApplicationRole};
            END IF;
        END
        $$;
        """;

    public static string RevokeSchema(string schema) => $"""
        ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM {ApplicationRole};
        ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} REVOKE USAGE, SELECT ON SEQUENCES FROM {ApplicationRole};
        REVOKE ALL ON ALL TABLES IN SCHEMA {schema} FROM {ApplicationRole};
        REVOKE ALL ON ALL SEQUENCES IN SCHEMA {schema} FROM {ApplicationRole};
        REVOKE USAGE ON SCHEMA {schema} FROM {ApplicationRole};
        """;

    /// <summary>
    /// RLS na tabela de escritório: <c>ENABLE</c> + <c>FORCE</c> e uma política que só deixa ler e gravar linhas da
    /// organização da sessão.
    /// </summary>
    public static string EnableTenantIsolation(string schema, string table) => $"""
        ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON {schema}.{table}
            USING (organization_id = {CurrentOrganizationFunction})
            WITH CHECK (organization_id = {CurrentOrganizationFunction});
        """;

    public static string DisableTenantIsolation(string schema, string table) => $"""
        DROP POLICY IF EXISTS tenant_isolation ON {schema}.{table};
        ALTER TABLE {schema}.{table} NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE {schema}.{table} DISABLE ROW LEVEL SECURITY;
        """;
}
