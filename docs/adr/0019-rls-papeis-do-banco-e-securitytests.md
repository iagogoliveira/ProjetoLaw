# ADR 0019 — Row-Level Security, papéis do banco e SecurityTests

- **Status:** Aceita
- **Data:** 2026-10-17
- **Etapa:** 3 (PR 3C)

## Contexto

A seção 6.2 pede isolamento em três camadas: filtro do EF, Row-Level Security no PostgreSQL e testes automáticos. Para a RLS valer, a aplicação conecta com um papel sem `BYPASSRLS`, que não é dono das tabelas, enquanto as migrations rodam com outro papel. A seção 6.4 pede a trilha de auditoria só de inserção (`REVOKE UPDATE, DELETE`). Até aqui, a API conectava como superusuário, e nenhuma das duas proteções existia.

## Decisão

1. **Papéis:**
   - **`lexflow_app`** é um papel de grupo, sem login, sem `BYPASSRLS` e só com DML nas tabelas. É criado pelas migrations, idempotente e resistente a migrations paralelas no mesmo cluster.
   - **Login da aplicação:** a API e o Worker conectam com um login membro de `lexflow_app` (`lexflow_api` no AppHost). O MigrationService cria o login a partir de `Database:ApplicationLogin`, com a senha vinda do cofre ou do parâmetro do AppHost. O DDL é montado pelo próprio PostgreSQL com `format(%I, %L)`, nunca concatenado no C#.
   - **Dono das tabelas:** as migrations rodam com ele, e ele precisa de `CREATEROLE` e `BYPASSRLS`. No ambiente local é o superusuário do container; em produção, um papel administrativo (runbook do deploy).
2. **Tenant da sessão:**
   - o `TenantSessionInterceptor` grava `app.organization_id` na sessão quando a conexão abre, e de novo antes de um comando se o tenant mudou (onboarding);
   - o valor é de sessão, e não `SET LOCAL`, porque o EF lê fora de transação explícita. O Npgsql descarta o estado ao devolver a conexão ao pool, e toda abertura grava o valor de novo;
   - `platform.current_organization_id()` lê o valor; vazio vira `NULL`, e então nenhuma linha passa.
3. **RLS (`ENABLE` + `FORCE`, política `tenant_isolation`)** em toda tabela `ITenantOwned`: `platform.jobs`, `platform.idempotency_keys` e as 10 tabelas de escritório do schema `org`. Leitura e escrita ficam restritas à organização da sessão.
   - Ficam fora, por serem da plataforma: `identity.*`, `org.organizations` (o id é o próprio tenant, e o usuário lista os nomes dos seus escritórios), `org.user_organizations` e `processed_integration_events`.
   - Um teste lê o modelo do EF e falha se alguma tabela `ITenantOwned` ficar sem RLS forçada.
4. **Auditoria:**
   - a aplicação perde `UPDATE`, `DELETE` e `TRUNCATE` em `audit.audit_logs`;
   - a leitura vê só a organização da sessão, e a inserção aceita a própria organização ou nenhuma (eventos da plataforma);
   - o encadeamento passa a rodar como o dono (`SECURITY DEFINER`), porque precisa ler a última linha de cada cadeia, inclusive a da plataforma, que a aplicação não enxerga. Nessa tabela vale `ENABLE` sem `FORCE`.
5. **Operações de plataforma entre tenants:** usam funções do dono que a aplicação só pode executar. A limpeza diária das chaves de idempotência virou `platform.delete_expired_idempotency_keys`, e com isso o último `IgnoreQueryFilters` do código saiu.
6. **Wolverine e Hangfire:** os schemas `wolverine` e `hangfire` passam ao papel da aplicação, que cria e evolui as próprias tabelas na partida. As tabelas existentes mudam de dono na migration. O painel do Hangfire continua sem rota exposta, e a autorização dele entra quando houver painel.
7. **`LexFlow.SecurityTests`:** roda contra a API conectada como o login da aplicação, com a RLS ativa. Descobre todos os endpoints pelo `EndpointDataSource` e verifica que:
   - todo endpoint protegido responde 401 para requisição anônima;
   - todo endpoint de escritório responde 403 para um membro sem a permissão;
   - toda rota com parâmetro tem um caso "recurso de outra organização ou de outro usuário", que precisa responder 404 (sem o caso, o teste falha apontando a rota);
   - nenhuma listagem traz ids de outra organização.
   O projeto reaproveita a infraestrutura dos testes da API por arquivos vinculados, e o CI passou a rodá-lo junto com os testes de integração.
8. **Os testes de integração da API também conectam como o login da aplicação.** Privilégio esquecido ou RLS errada aparecem em qualquer teste, não só nos de segurança.

## Consequências

- **Critérios de aceite da Etapa 3 que fecham aqui:**
  - "Org A recebe 404 em qualquer recurso da Org B": `EndpointSecurityTests.Resources_of_another_organization_or_user_answer_404`;
  - "SQL cru com o tenant da Org A não vê linhas da Org B": `RowLevelSecurityTests.Raw_sql_as_the_application_only_sees_the_organization_of_the_session`.
- **Custo de desempenho:** cada abertura de conexão faz um `set_config` a mais. Se pesar, dá para manter uma conexão por requisição ou usar o multiplexing do Npgsql.
- **Produção:** exige provisionar o papel dono com `CREATEROLE` e `BYPASSRLS`, e guardar a senha do login da aplicação no cofre.
