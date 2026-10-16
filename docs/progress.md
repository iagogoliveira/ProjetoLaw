# LexFlow — Progresso da implementação

Ordem das etapas: `docs/spec/implementacao.md` §9–12. Uma etapa só começa quando a anterior está verde no CI.

| Etapa | Nome | Status | Branch / PR |
| --- | --- | --- | --- |
| 0 | Bootstrap do repositório | Implementada; aguardando CI verde no PR | `etapa-0-bootstrap` |
| 1 | Building blocks | Não iniciada | — |

---

## Etapa 0 — Bootstrap do repositório

**RFs:** nenhum (infraestrutura). **Data:** 2026-10-16.

### Entregue

| Área | Itens |
| --- | --- |
| Solução | `LexFlow.sln`, `global.json` (SDK 10.0.302, runner MTP), `Directory.Build.props` (net10.0, Nullable, `TreatWarningsAsErrors`, Meziantou + Sonar), `Directory.Packages.props` (CPM + pins de segurança), `.editorconfig`, `.gitattributes` |
| Building blocks | `SharedKernel`, `Application.Abstractions`, `Infrastructure.Common`, `Web.Common`. Vazios: só o marcador de assembly; o conteúdo chega na Etapa 1 |
| Hosts | `LexFlow.Api`: Minimal API, `/health/live`, `/health/ready`, OpenAPI (`/openapi/v1.json`) e Scalar (`/scalar`) em Development, Serilog e OpenTelemetry. `LexFlow.Worker`: host com as mesmas conexões |
| Aspire | `LexFlow.ServiceDefaults` (OTel, health, resiliência, discovery, Serilog). `LexFlow.AppHost`: SQL Server 2025, Redis, RabbitMQ, Azurite, OpenSearch, Gotenberg, ClamAV, Mailpit, API, Worker e Web |
| Frontend | `src/Web/lexflow-web`: Angular 22 standalone e zoneless, Material, Transloco (pt-BR padrão), ESLint, Prettier, Vitest, Playwright + axe. Shell vazio com skip-link e tema claro/escuro por tokens |
| Testes | `LexFlow.ArchitectureTests`, `LexFlow.Api.IntegrationTests`, `LexFlow.AppHost.Tests` (Docker), Vitest e Playwright |
| CI | `.github/workflows/ci.yml`. Jobs: backend, integração, frontend, e2e, OpenAPI e segurança |
| Docs | ADRs 0001–0004 e este arquivo. As specs passaram de `docs/specs/` para `docs/spec/`, o caminho citado no `CLAUDE.md` |

### Critérios de aceite → evidência

| Critério | Evidência (teste) | Onde roda | Resultado |
| --- | --- | --- | --- |
| `dotnet run --project src/Aspire/LexFlow.AppHost` sobe API, Worker, Web e todos os containers, todos saudáveis | `LexFlow.AppHost.Tests.AppHostTests.AllResources_BecomeHealthy` | CI, job `integration` (precisa de Docker) | Pendente: aguardando o CI |
| `GET /health/ready` retorna 200 com SQL, Redis, RabbitMQ e Blob verificados | `AppHostTests.ApiHealthReady_Returns200_WithSqlRedisRabbitBlob` (containers reais) + `HealthEndpointsTests.Ready_ListsSqlRedisRabbitBlobChecks` e `Ready_Returns503_WhenDependencyIsUnhealthy` (em memória) | CI `integration`; local + CI `backend` | Em memória: ✅ local. Containers: aguardando o CI |
| Frontend abre com layout vazio em pt-BR | `e2e/shell.spec.ts › shell › abre layout vazio em pt-BR sem violações de acessibilidade` + `Shell › renderiza título traduzido em pt-BR` (Vitest) | Local (`ng serve`) + CI `e2e` (contra o AppHost) | ✅ local. AppHost: aguardando o CI |
| CI verde no primeiro PR; um teste de arquitetura quebrado faz o CI falhar | PR da Etapa 0 + PR de demonstração com `BuildingBlockTests.WebCommon_DoesNotDependOnInfrastructure` quebrado | GitHub Actions | Prova local ✅: com a violação, `WebCommon_DoesNotDependOnInfrastructure` falhou e apontou `LexFlow.Web.Common.Violation`. CI: pendente |

Resultados locais em 2026-10-16:
- `dotnet build`: 0 avisos.
- `dotnet format --verify-no-changes`: OK.
- `dotnet test` sem os testes que exigem Docker: 17/17.
- `npm run lint`: OK.
- `npm test`: 2/2.
- `npx playwright test`: 1/1.

### ADRs criadas

- [0001 — Monólito modular](adr/0001-monolito-modular.md) — Aceita
- [0002 — Stack e pacotes (licenças)](adr/0002-stack.md) — Aceita; **SonarAnalyzer.CSharp é Proposta** (licença SSALv1, precisa de revisão humana)
- [0003 — Endpoints de saúde em todos os ambientes](adr/0003-health-endpoints-todos-ambientes.md) — Proposta
- [0004 — Detalhes do AppHost Aspire 13](adr/0004-aspire-apphost.md) — Proposta

### Pendências

1. **Validar no CI** os critérios que dependem de Docker (AppHost, `/health/ready` com containers, e2e contra o AppHost). Localmente o Docker não estava ativo.
2. **Revisão humana da licença do SonarAnalyzer.CSharp** (ADR 0002).
3. **CodeQL** em repositório privado roda sem upload (sem GHAS); os resultados ficam como artefato.
4. **Cobertura ≥ 80%** em Domain/Application ainda não se aplica: não há módulos. A coleta já está ativa no CI (`--coverage`, formato cobertura).
5. **Fontes Roboto e Material Symbols vêm do Google Fonts** (adicionadas pelo `ng add @angular/material`). Self-hospedar quando a CSP restritiva entrar (§7.3).
6. **Cliente TypeScript gerado do OpenAPI** (`@hey-api/openapi-ts`) fica para quando houver o primeiro endpoint de negócio. A pasta `src/app/api/` já existe.
7. **Projetos ainda não criados da árvore §4.1:** `LexFlow.SecurityTests`, `LexFlow.LoadTests`, `LexFlow.AiEvals`, `infra/` (Bicep), `deploy-staging.yml` e `deploy-prod.yml`. Nascem nas etapas que os usam (SecurityTests na Etapa 1/3, quando houver endpoints com dados de tenant).
8. **`npm` 11.16 bloqueia scripts de instalação não aprovados** (`esbuild`, `lmdb`, `@parcel/watcher`, `msgpackr-extract`). Os binários pré-compilados funcionam. Decidir `npm approve-scripts` quando o time padronizar a versão do npm.

### Como rodar

```bash
dotnet run --project src/Aspire/LexFlow.AppHost      # tudo (requer Docker)
dotnet build && dotnet test                           # backend (AppHost.Tests requer Docker)
dotnet test --solution LexFlow.sln --filter-not-trait "Category=RequiresDocker" --ignore-exit-code 8
cd src/Web/lexflow-web && npm ci && npm run lint && npm test && npx playwright test
```
