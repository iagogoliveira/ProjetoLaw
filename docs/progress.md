# LexFlow — Progresso da implementação

Ordem das etapas: `docs/spec/implementacao.md`, seções 9 a 12.

| Etapa | Status | Branch |
| --- | --- | --- |
| 0 — Bootstrap do repositório | **Concluída** (PR #3 mergeado) | `etapa-0-bootstrap` |
| 1 — Building blocks | **Concluída localmente**; PRs #4, #5 e C aguardando revisão e merge | `etapa-1a-shared-kernel`, `etapa-1b-application-web`, `etapa-1c-infra-platform` |

---

## Etapa 1 — Building blocks (2026-10-16)

**RFs:** base para todos. Feriados e tribunais preparam o RF-03 e o RF-09; a auditoria prepara o RF-45.

Plano aprovado em 2026-10-16, em 3 PRs empilhados: **A** #4 (SharedKernel e value objects), **B** #5 (Application.Abstractions e Web.Common) e **C** (infraestrutura, Platform e mensageria).

**Decisões de negócio confirmadas:**
- O seed de feriados tem só os feriados nacionais de lei federal. Forenses, estaduais, municipais, Carnaval, Corpus Christi e recesso ficam para a Etapa 7.
- Os números CNJ de teste são gerados com dígito verificador correto, sem processos reais.

### Entregue

| # | Subtarefa | Testes |
| --- | --- | --- |
| 1.1 | SharedKernel: `Entity`, `AggregateRoot`, `ValueObject`, `Result`/`Error`, `ITenantOwned`, `ICaseScoped`, `ISoftDeletable`, `IAuditable`, `[NotAudited]`, `[Sensitive]` | `ResultTests`, `EntityTests` |
| 1.2 | Value objects: `Cpf`, `Cnpj` (inclui alfanumérico), `CnjNumber`, `OabNumber`, `Money`, `Email`, `Phone`, `Cep` | `CnjNumberTests`, `DocumentTests`, `ContactTests`, `MoneyTests` |
| 1.3 | Regras do CLAUDE.md no build e no IL (ADR 0006) | `BannedApiTests.*` |
| 1.4 | Pipeline: `ICommand`/`IQuery`, decorators Logging → Validação → Transação, `IUnitOfWork` por módulo, `IClock`, `IIdGenerator`, Scrutor (ADR 0007) | `PipelineTests`, `ClockTests`, `HandlerConventionTests` |
| 1.5 | Web.Common: ProblemDetails, `X-Correlation-Id`, `RequirePermission` e autenticação provisórios (ADR 0008), cursor, `IModule` | `ProblemDetailsTests`, `SecurityAndCorrelationTests`, `CursorPaginationTests` |
| 1.6 | EF Core base: `ModuleDbContextBase` (filtros nomeados de tenant e soft delete, `xmin`), interceptors de eventos de domínio, tenant, colunas de controle e auditoria | `PersistenceTests`, `AuditTrailTests` |
| 1.7 | Módulo Platform: `Job`, `IdempotencyKey`, `Holiday` (regras), `Court` (107); migrations; trilha encadeada e append-only (ADR 0010); `MigrationService` (ADR 0011) | `HolidayCalendarTests`, `JobAndCourtTests`, `MigrationTests`, `AuditLogTests` |
| 1.8 | Outbox/inbox Wolverine (PostgreSQL + RabbitMQ), consumo idempotente por `EventId` (ADR 0009) | `OutboxTests` |
| 1.9 | Adaptadores: S3, SMTP, ClamAV (cliente próprio), Hangfire com tenant do agendador | `AdaptersTests` |
| 1.10 | `GET /api/v1/jobs/{id}`, `Idempotency-Key` com armazenamento e limpeza diária | `JobsEndpointTests`, `IdempotencyFilterTests`, `IdempotencyStoreTests` |
| 1.11 | Contrato versionado (`docs/openapi/v1.json`), `oasdiff` no CI, cliente TypeScript gerado, cobertura ≥ 80% (ADR 0012) | `OpenApiContractTests`, `scripts/coverage_gate.py` |

**Tabelas:** `audit.audit_logs`, `platform.jobs`, `platform.idempotency_keys`, `platform.holidays`, `platform.courts`, `platform.processed_integration_events`, além dos schemas `wolverine` e `hangfire`, mantidos pelas próprias bibliotecas.
**Endpoint:** `GET /api/v1/jobs/{id}` (permissão `platform.jobs.read`).
**Telas:** nenhuma; existe só o cliente gerado em `src/app/api`.

### Critérios de aceite e evidências

| Critério | Status | Evidência |
| --- | --- | --- |
| Testes unitários dos value objects com casos válidos e inválidos (≥ 20 CNJ válidos e ≥ 20 inválidos) | ✅ | `CnjNumberTests.Valid_numbers_are_accepted` (22 números, segmentos J = 1 a 9), `Valid_numbers_pass_independent_mod97_check` (`BigInteger`), `Invalid_numbers_are_rejected` (20) e `Null_is_rejected`; `DocumentTests`, `ContactTests` e `MoneyTests` para os demais value objects |
| Evento de integração gravado no outbox é publicado e consumido uma única vez, mesmo com reentrega | ✅ | `OutboxTests.Event_saved_in_outbox_is_published_and_consumed_exactly_once`, `Redelivered_event_is_not_processed_twice`, `Rolled_back_transaction_publishes_nothing` e `Consumer_runs_with_the_tenant_of_the_event` (Wolverine, PostgreSQL e RabbitMQ reais) |
| `SaveChanges` gera linha em `audit.audit_logs` com antes e depois | ✅ | `AuditTrailTests.Save_changes_writes_audit_rows_with_before_and_after` e `Sensitive_and_not_audited_fields_never_enter_changes`; com a migration real, `AuditLogTests.Save_changes_writes_chained_audit_row_with_before_and_after`, além de cadeia sob concorrência, append-only e adulteração detectada |

**Última verificação local:** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 333 testes .NET (8 projetos), 5 Vitest e 4 Playwright passando, cobertura entre 90,3% e 98,3% nas camadas do portão, `npm run lint`, `npm test`, `npm run build` e cliente da API determinístico.

### ADRs

| ADR | Status |
| --- | --- |
| [0006 — Regras do CLAUDE.md no compilador e no IL](adr/0006-regras-no-compilador.md) | Aceita |
| [0007 — Pipeline de casos de uso próprio](adr/0007-pipeline-de-casos-de-uso.md) | Aceita |
| [0008 — Autenticação, permissão e tenant provisórios](adr/0008-autenticacao-e-tenant-provisorios.md) | Aceita |
| [0009 — Mensageria, jobs e adaptadores](adr/0009-mensageria-jobs-e-adaptadores.md) | Aceita |
| [0010 — Trilha de auditoria](adr/0010-trilha-de-auditoria.md) | Aceita |
| [0011 — Dados de referência e MigrationService](adr/0011-dados-de-referencia-e-migrations.md) | Aceita |
| [0012 — Contrato da API e cobertura](adr/0012-contrato-da-api-e-cobertura.md) | Aceita |

### Pendências

1. **Etapa 2:** substituir o esquema de autenticação provisório pelo OpenIddict.
2. **Etapa 3:**
   - validar `org_id` contra `OrganizationMember` e aceitar o cabeçalho `X-Organization-Id`;
   - RLS e papéis do banco, incluindo o `REVOKE UPDATE, DELETE` em `audit.audit_logs`;
   - permissão `platform.jobs.read` no `PermissionCatalog`;
   - decorator de autorização de recurso;
   - painel do Hangfire com autorização;
   - `SecurityTests` cobrindo `GET /api/v1/jobs/{id}`.
3. **Etapa 7:** feriados forenses, estaduais e municipais, recesso e feriados do escritório.
4. **Desempenho:** pré-gerar o código do Wolverine (`TypeLoadMode.Static`) para a inicialização em produção.
5. **Etapa 9:** `StreamMaxLength` do ClamAV alinhado ao limite de upload.
6. **Testes:** a inbox do Wolverine (descarte por envelope) não tem teste próprio. O efeito único é garantido e testado pela camada de `processed_integration_events`.
7. Continuam da Etapa 0: Worker sem endpoint de saúde; política do npm `allow-scripts`; licença do Redis 8.

---

## Etapa 0 — Bootstrap do repositório (2026-10-16)

**RFs:** nenhum (infraestrutura).

### Entregue (um commit por subtarefa)

| # | Subtarefa | Principais artefatos |
| --- | --- | --- |
| 0.1 | Base do repositório | `global.json` (SDK 10.0.302 + runner MTP), `Directory.Build.props` (net10.0, Nullable, TreatWarningsAsErrors, `latest-recommended`), `Directory.Packages.props` (CPM), `.editorconfig`, Meziantou.Analyzer; `docs/specs` → `docs/spec` |
| 0.2 | Solução e esqueleto | `LexFlow.sln`, BuildingBlocks (`SharedKernel`, `Application.Abstractions`, `Infrastructure.Common`, `Web.Common`), `LexFlow.Api`, `LexFlow.Worker`, `infra/`, `deploy/` |
| 0.3 | ServiceDefaults + API | Serilog (mantém o provedor OTel), OpenTelemetry, `GET /health/live`, `GET /health/ready` (PostgreSQL, Redis, RabbitMQ, armazenamento S3), OpenAPI + Scalar em Development; `tests/LexFlow.Api.IntegrationTests` (Testcontainers) |
| 0.4 | Testes de arquitetura | `tests/LexFlow.ArchitectureTests`: regras da seção 4.2 por namespace, BuildingBlocks, auto-testes com fixture de violações |
| 0.5 | Frontend | `src/Web/lexflow-web`: Angular 22 zoneless, standalone/OnPush (exigido no lint), Material 3 com tokens CSS e tema claro/escuro, Transloco pt-BR (en preparado, sem HTTP), ESLint + Prettier, Vitest, Playwright + axe; shell vazio em pt-BR |
| 0.6 | AppHost | PostgreSQL (pgvector pg17), Redis, RabbitMQ, RustFS (S3), Gotenberg, ClamAV, Mailpit, API, Worker, Web; segredo do armazenamento gerado pelo Aspire; `tests/LexFlow.AppHost.Tests` |
| 0.7 | CI | `.github/workflows/ci.yml`: build, format, pacotes vulneráveis, arquitetura, integração, frontend (audit, lint, Vitest, build), AppHost + Playwright contra o AppHost, CodeQL, Gitleaks, Trivy (fs) |
| 0.8 | Documentação | ADRs 0001–0005 e este arquivo |

Não há entidades, tabelas, migrations nem endpoints de negócio nesta etapa.

### Critérios de aceite e evidências

| Critério | Status | Evidência |
| --- | --- | --- |
| `dotnet run --project src/Aspire/LexFlow.AppHost` sobe API, Worker, Web e todos os containers, todos saudáveis no dashboard | ✅ | `AppHostTests.Resource_becomes_healthy` (postgres, lexflow, redis, rabbit, storage, gotenberg, clamav via `zPING`/`PONG`, mailpit, api, web) e `AppHostTests.Resource_is_running` (worker, que não tem endpoint HTTP de saúde). Verificação manual: `dotnet run --launch-profile http` subiu tudo e o dashboard ficou em `http://localhost:15211` |
| `GET /health/ready` retorna 200 com PostgreSQL, Redis, RabbitMQ e MinIO verificados | ✅ (MinIO → RustFS, ADR 0004) | `HealthEndpointsTests.Ready_returns_200_when_all_dependencies_are_up`; `HealthEndpointsTests.Ready_returns_503_when_a_dependency_is_down` (4 casos, um por dependência) prova que cada uma é verificada; `AppHostTests.Api_ready_endpoint_returns_200` |
| Frontend abre com layout vazio em pt-BR | ✅ | e2e `shell.spec.ts › opens the empty layout in pt-BR`, `skip link moves focus to the main content`, `has no WCAG 2.1 AA accessibility violations` (claro e escuro); Vitest `Shell`, `DashboardPage`, `translations`; `AppHostTests.Web_serves_the_angular_app_in_pt_BR` |
| CI verde no primeiro PR; um teste de arquitetura propositalmente quebrado faz o CI falhar | ✅ | PR #3: CI verde no commit `e1cc7c7` (run 38948667829; Backend, Frontend, AppHost + E2E, Gitleaks e Trivy passaram, CodeQL pulado, ver pendência 10). Commit temporário `8a2c0f5` (`LexFlow.Demo.Domain` usando `LexFlow.Demo.Infrastructure`): run 38949537138 falhou em `ModuleDependencyTests.Module_layers_respect_dependency_rules` com "Demo.Domain viola a seção 4.2: LexFlow.Demo.Domain.DemoEntity". Revertido em seguida. As regras também são provadas por `LayerRulesSelfTests.*` |

**Última verificação local completa:** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test --solution LexFlow.sln` (33/33), `npm run lint`, `npm test` (5/5), `npx playwright test` (4/4, contra o `ng serve` e contra o AppHost), `dotnet list package --vulnerable` e `npm audit` sem achados.

### ADRs criadas

| ADR | Status |
| --- | --- |
| [0001 — Monólito modular](adr/0001-monolito-modular.md) | Aceita |
| [0002 — Stack e licenças dos pacotes](adr/0002-stack.md) | Aceita |
| [0003 — Módulos criados por etapa](adr/0003-modulos-criados-por-etapa.md) | Aceita |
| [0004 — RustFS no lugar do MinIO](adr/0004-armazenamento-s3-local-rustfs.md) | Aceita (spec atualizada: seções 2.3, 4.1, 8.1, 8.2, 8.3, 9 e 13.1) |
| [0005 — SonarAnalyzer fora até revisão da licença](adr/0005-sonaranalyzer-fora.md) | Aceita |

### Pendências

1. **Revisão e merge do PR #3** (https://github.com/iagogoliveira/ProjetoLaw/pull/3).
2. ~~Revisão humana das ADRs 0003, 0004 e 0005~~: aceitas em 2026-10-16; spec atualizada (MinIO → RustFS).
3. **Checagem de breaking changes do OpenAPI (`oasdiff`)** e **cliente TypeScript gerado** (`@hey-api/openapi-ts`): entram quando houver o primeiro endpoint de negócio, porque ainda não há baseline.
4. **Trivy em imagens:** hoje varre só o filesystem. A varredura de imagens entra com os Dockerfiles do `deploy-staging.yml`.
5. **Cobertura ≥ 80% em Domain/Application:** ainda não há código nessas camadas. Ligar a coleta de cobertura (extensão do Microsoft.Testing.Platform) na Etapa 1.
6. **Worker sem endpoint de saúde:** registra os health checks das dependências, mas não os expõe. A observabilidade do Worker (Wolverine e Hangfire) entra na Etapa 1.
7. **`/health/*` sem autenticação em todos os ambientes:** responde só o status agregado, sem detalhes. Revisar junto com as proteções da Etapa 3 e da seção 7.3.
8. **npm 11 `allow-scripts`:** aparecem avisos sobre scripts de instalação não aprovados (`esbuild`, `lmdb`, `@parcel/watcher`, `msgpackr-extract`). O build funciona sem eles; decidir a política de aprovação (`npm approve-scripts`).
9. **Licença do Redis 8** (RSALv2/SSPLv1/AGPLv3): aceitável para uso interno. Avaliar Valkey antes de qualquer distribuição (ADR 0002).
10. ~~CodeQL desligado~~: o repositório ficou público em 2026-10-16 e o CodeQL voltou a rodar sem condição. Se o repositório voltar a ser privado, será preciso o GitHub Code Security.
11. **CI no GitHub:** o evento `pull_request` não disparou para o commit `e1cc7c7`, e o CI rodou por `workflow_dispatch`. Nos commits seguintes o evento voltou a disparar normalmente.
12. **Ajustes de CI descobertos no primeiro PR:** o runner Linux não confia no certificado de dev, então o readiness usa o endpoint `http`; o `dotnet run` do AppHost com `AspireUseCliBundle` só funciona em Debug; CodeQL precisa de `actions: read` e Gitleaks de `pull-requests: read`.
13. **PRs #1 e #2 (fechados sem merge, revisados em 2026-10-16):** eram uma tentativa anterior da Etapa 0 com SQL Server 2025, Azurite e OpenSearch, descartada quando a stack passou a ser PostgreSQL. Foi aproveitado o health check do ClamAV (`ClamAvHealthCheck`, protocolo clamd). Ficam como referência para a Etapa 1: a geração do `docs/openapi/v1.json` no build com o job `oasdiff` (pendência 3), o `proxy.conf.js` do Angular para `/api` e o writer JSON do `/health/ready` (só status e duração por dependência).
