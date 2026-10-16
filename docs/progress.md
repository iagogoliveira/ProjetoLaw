# LexFlow — Progresso da implementação

Ordem das etapas: `docs/spec/implementacao.md`, seções 9 a 12.

| Etapa | Status | Branch |
| --- | --- | --- |
| 0 — Bootstrap do repositório | **Concluída**; aguardando revisão e merge do PR #3 | `etapa-0-bootstrap` |
| 1 — Building blocks | Não iniciada | — |

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
| `dotnet run --project src/Aspire/LexFlow.AppHost` sobe API, Worker, Web e todos os containers, todos saudáveis no dashboard | ✅ | `AppHostTests.Resource_becomes_healthy` (postgres, lexflow, redis, rabbit, storage, gotenberg, mailpit, api, web) e `AppHostTests.Resource_is_running` (clamav, worker, que não têm endpoint HTTP de saúde). Verificação manual: `dotnet run --launch-profile http` subiu tudo e o dashboard ficou em `http://localhost:15211` |
| `GET /health/ready` retorna 200 com PostgreSQL, Redis, RabbitMQ e MinIO verificados | ✅ (MinIO → RustFS, ADR 0004) | `HealthEndpointsTests.Ready_returns_200_when_all_dependencies_are_up`; `HealthEndpointsTests.Ready_returns_503_when_a_dependency_is_down` (4 casos, um por dependência) prova que cada uma é verificada; `AppHostTests.Api_ready_endpoint_returns_200` |
| Frontend abre com layout vazio em pt-BR | ✅ | e2e `shell.spec.ts › opens the empty layout in pt-BR`, `skip link moves focus to the main content`, `has no WCAG 2.1 AA accessibility violations` (claro e escuro); Vitest `Shell`, `DashboardPage`, `translations`; `AppHostTests.Web_serves_the_angular_app_in_pt_BR` |
| CI verde no primeiro PR; um teste de arquitetura propositalmente quebrado faz o CI falhar | ✅ | PR #3: CI verde no commit `e1cc7c7` (run 38948667829; Backend, Frontend, AppHost + E2E, Gitleaks e Trivy passaram, CodeQL pulado, ver pendência 10). Commit temporário `8a2c0f5` (`LexFlow.Demo.Domain` usando `LexFlow.Demo.Infrastructure`): run 38949537138 falhou em `ModuleDependencyTests.Module_layers_respect_dependency_rules` com "Demo.Domain viola a seção 4.2: LexFlow.Demo.Domain.DemoEntity". Revertido em seguida. As regras também são provadas por `LayerRulesSelfTests.*` |

**Última verificação local completa:** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test --solution LexFlow.sln` (33/33), `npm run lint`, `npm test` (5/5), `npx playwright test` (4/4, contra o `ng serve` e contra o AppHost), `dotnet list package --vulnerable` e `npm audit` sem achados.

### ADRs criadas

| ADR | Status |
| --- | --- |
| [0001 — Monólito modular](adr/0001-monolito-modular.md) | Aceita |
| [0002 — Stack e licenças dos pacotes](adr/0002-stack.md) | Aceita |
| [0003 — Módulos criados por etapa](adr/0003-modulos-criados-por-etapa.md) | **Proposta** |
| [0004 — RustFS no lugar do MinIO](adr/0004-armazenamento-s3-local-rustfs.md) | **Proposta** |
| [0005 — SonarAnalyzer fora até revisão da licença](adr/0005-sonaranalyzer-fora.md) | **Proposta** |

### Pendências

1. **Revisão e merge do PR #3** (https://github.com/iagogoliveira/ProjetoLaw/pull/3).
2. **Revisão humana das ADRs 0003, 0004 e 0005.** Se a 0004 for aceita, atualizar as seções 2.3, 8.1 e 8.3 da especificação (MinIO → armazenamento S3/RustFS).
3. **Checagem de breaking changes do OpenAPI (`oasdiff`)** e **cliente TypeScript gerado** (`@hey-api/openapi-ts`): entram quando houver o primeiro endpoint de negócio, porque ainda não há baseline.
4. **Trivy em imagens:** hoje varre só o filesystem. A varredura de imagens entra com os Dockerfiles do `deploy-staging.yml`.
5. **Cobertura ≥ 80% em Domain/Application:** ainda não há código nessas camadas. Ligar a coleta de cobertura (extensão do Microsoft.Testing.Platform) na Etapa 1.
6. **Worker sem endpoint de saúde:** registra os health checks das dependências, mas não os expõe. A observabilidade do Worker (Wolverine e Hangfire) entra na Etapa 1.
7. **`/health/*` sem autenticação em todos os ambientes:** responde só o status agregado, sem detalhes. Revisar junto com as proteções da Etapa 3 e da seção 7.3.
8. **npm 11 `allow-scripts`:** aparecem avisos sobre scripts de instalação não aprovados (`esbuild`, `lmdb`, `@parcel/watcher`, `msgpackr-extract`). O build funciona sem eles; decidir a política de aprovação (`npm approve-scripts`).
9. **Licença do Redis 8** (RSALv2/SSPLv1/AGPLv3): aceitável para uso interno. Avaliar Valkey antes de qualquer distribuição (ADR 0002).
10. **CodeQL desligado:** code scanning em repositório privado exige GitHub Code Security (pago). O job só roda com o repositório público. Decisão pendente: manter assim, contratar o Code Security ou tornar o repositório público.
11. **CI no GitHub:** o evento `pull_request` não disparou para o commit `e1cc7c7`, e o CI rodou por `workflow_dispatch`. Nos commits seguintes o evento voltou a disparar normalmente.
12. **Ajustes de CI descobertos no primeiro PR:** o runner Linux não confia no certificado de dev, então o readiness usa o endpoint `http`; o `dotnet run` do AppHost com `AspireUseCliBundle` só funciona em Debug; CodeQL precisa de `actions: read` e Gitleaks de `pull-requests: read`.
