using Microsoft.EntityFrameworkCore.Migrations;
using Security = LexFlow.Infrastructure.Common.Persistence.DatabaseSecurity;

#nullable disable

namespace LexFlow.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Segurança do banco (Etapa 3, seção 6.2, item 5): papel da aplicação, tenant da sessão, RLS nas tabelas de
    /// escritório do Platform, trilha de auditoria só de inserção e schemas do Wolverine e do Hangfire.
    /// </summary>
    public partial class DatabaseSecurity : Migration
    {
        private const string App = Security.ApplicationRole;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Security.CreateApplicationRole());

            migrationBuilder.Sql("""
                CREATE FUNCTION platform.current_organization_id() RETURNS uuid
                LANGUAGE sql STABLE AS $$
                    SELECT NULLIF(current_setting('app.organization_id', true), '')::uuid
                $$;
                """);

            migrationBuilder.Sql(Security.GrantSchema("platform"));
            migrationBuilder.Sql(Security.GrantSchema("audit"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("platform", "jobs"));
            migrationBuilder.Sql(Security.EnableTenantIsolation("platform", "idempotency_keys"));

            // Trilha só de inserção para a aplicação (seção 6.4). A leitura vê só a organização da sessão; as linhas da
            // plataforma (sem organização) ficam visíveis só para o dono. Sem FORCE: o encadeamento roda como o dono
            // (SECURITY DEFINER) e precisa ler a última linha de cada cadeia, inclusive a da plataforma.
            migrationBuilder.Sql($"""
                REVOKE UPDATE, DELETE, TRUNCATE ON audit.audit_logs FROM {App};
                ALTER TABLE audit.audit_logs ENABLE ROW LEVEL SECURITY;
                CREATE POLICY audit_read ON audit.audit_logs FOR SELECT
                    USING (organization_id = platform.current_organization_id());
                CREATE POLICY audit_append ON audit.audit_logs FOR INSERT
                    WITH CHECK (organization_id IS NULL OR organization_id = platform.current_organization_id());
                ALTER FUNCTION audit.audit_logs_chain() SECURITY DEFINER SET search_path = audit, public, pg_temp;
                """);

            // Limpeza diária das chaves de idempotência, entre tenants: função do dono, a aplicação só executa.
            migrationBuilder.Sql($"""
                CREATE FUNCTION platform.delete_expired_idempotency_keys(cutoff timestamptz) RETURNS integer
                LANGUAGE sql SECURITY DEFINER SET search_path = platform, pg_temp AS $$
                    WITH deleted AS (DELETE FROM platform.idempotency_keys WHERE expires_at <= cutoff RETURNING 1)
                    SELECT count(*)::integer FROM deleted
                $$;
                REVOKE ALL ON FUNCTION platform.delete_expired_idempotency_keys(timestamptz) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION platform.delete_expired_idempotency_keys(timestamptz) TO {App};
                """);

            // Wolverine e Hangfire criam e evoluem as próprias tabelas na partida: os schemas ficam com o papel da
            // aplicação (sem dados de escritório). Tabelas já criadas por outro usuário passam para o papel.
            migrationBuilder.Sql($"""
                CREATE SCHEMA IF NOT EXISTS wolverine;
                CREATE SCHEMA IF NOT EXISTS hangfire;
                ALTER SCHEMA wolverine OWNER TO {App};
                ALTER SCHEMA hangfire OWNER TO {App};
                DO $$
                DECLARE r record;
                BEGIN
                    FOR r IN SELECT schemaname, tablename FROM pg_tables WHERE schemaname IN ('wolverine', 'hangfire') LOOP
                        EXECUTE format('ALTER TABLE %I.%I OWNER TO {App}', r.schemaname, r.tablename);
                    END LOOP;
                    FOR r IN
                        SELECT n.nspname AS schema_name, p.proname AS function_name, pg_get_function_identity_arguments(p.oid) AS arguments
                        FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                        WHERE n.nspname IN ('wolverine', 'hangfire')
                    LOOP
                        EXECUTE format('ALTER FUNCTION %I.%I(%s) OWNER TO {App}', r.schema_name, r.function_name, r.arguments);
                    END LOOP;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS platform.delete_expired_idempotency_keys(timestamptz);");
            migrationBuilder.Sql("""
                ALTER FUNCTION audit.audit_logs_chain() SECURITY INVOKER RESET search_path;
                DROP POLICY IF EXISTS audit_append ON audit.audit_logs;
                DROP POLICY IF EXISTS audit_read ON audit.audit_logs;
                ALTER TABLE audit.audit_logs DISABLE ROW LEVEL SECURITY;
                """);
            migrationBuilder.Sql(Security.DisableTenantIsolation("platform", "idempotency_keys"));
            migrationBuilder.Sql(Security.DisableTenantIsolation("platform", "jobs"));
            migrationBuilder.Sql(Security.RevokeSchema("audit"));
            migrationBuilder.Sql(Security.RevokeSchema("platform"));
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS platform.current_organization_id();");
        }
    }

}
