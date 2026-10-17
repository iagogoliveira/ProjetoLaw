# LexFlow — Progresso da implementação

Ordem das etapas: `docs/spec/implementacao.md`, seções 9 a 12.

| Etapa | Status | Branch |
| --- | --- | --- |
| 0 — Bootstrap do repositório | **Concluída** (PR #3 mergeado) | `etapa-0-bootstrap` |
| 1 — Building blocks | **Concluída** (PRs #4, #5 e #6 mergeados; CI do `main` verde) | `etapa-1a-shared-kernel`, `etapa-1b-application-web`, `etapa-1c-infra-platform` |
| 2 — Identidade e autenticação | **Concluída no branch** (PRs A #7, B1 #8 e B2 #9 mergeados; C pronto, aguardando PR) | `etapa-2a-identity-accounts`, `etapa-2b-oidc-login`, `etapa-2b2-mfa-me`, `etapa-2c-frontend-e2e` |

---

## Etapa 2 — Identidade e autenticação (todos os critérios de aceite com teste; PR C aguardando merge)

**RFs:** RF-01 (cadastro, login, logout, recuperação de senha, MFA, bloqueio por tentativas, sessões). Ficam para depois: MFA obrigatório por organização, convites, troca de organização e restrição por IP/horário (Etapa 3); SSO, SCIM e passkeys (Etapa 16).

Plano aprovado em 2026-10-16, em 3 PRs: **A** (contas, senhas, e-mails), **B** (servidor OIDC, login, sessões, TOTP), **C** (frontend e e2e).

**Decisões de negócio confirmadas:**
- O cadastro é aberto (autoatendimento).
- MFA por TOTP e também por código enviado por e-mail.
- O refresh token é renovado a cada uso, vale 14 dias sem uso e no máximo 30 dias; depois disso é preciso entrar de novo.
- A verificação de senhas vazadas (Have I Been Pwned, k-anonymity) fica ligada por padrão, com chave para desligar.
- MFA é opcional por usuário nesta etapa: quem ativa escolhe TOTP ou código por e-mail como segundo fator (decisão de 2026-10-16). O MFA obrigatório por organização fica para a Etapa 3.
- Sem "lembrar este dispositivo" por enquanto: todo login de quem tem MFA pede o segundo fator.
- O SPA guarda os tokens em `sessionStorage` (decisão de 2026-10-16): sobrevive ao F5 na mesma aba e some ao fechar a aba; nada em `localStorage` (seção 5.3).

**Plano do PR C (proposto em 2026-10-16):** CORS só para a origem do SPA; Mailpit em porta fixa e rate limit configurável no AppHost; `core/auth` com `angular-auth-oidc-client` (PKCE, refresh rotativo, guard, interceptor); configuração em tempo de execução (`config.json`); página pública `/entrar`; feature `account` (perfil, sessões, ativação de TOTP com QR code e códigos de recuperação, ou código por e-mail) com SignalStore (`@ngrx/signals`); Vitest; e2e contra o AppHost do fluxo cadastro → confirmação (Mailpit) → login → TOTP → logout → login com TOTP.

**Plano do PR B (proposto em 2026-10-16), dividido em dois:**
- **B1:** OpenIddict (Authorization Code + PKCE, refresh rotativo com revogação em reuso), validação de token no lugar do esquema provisório (ADR 0008), páginas Razor de conta em pt-BR, sessões ligadas aos tokens (`sid`), bloqueio na 6ª senha errada, rate limit por IP e limpeza de tokens expirados.
- **B2:** MFA (TOTP com códigos de recuperação e código por e-mail), `GET /api/v1/me` e `GET|DELETE /api/v1/me/sessions`.

### PR C — frontend e e2e (pronto no branch `etapa-2c-frontend-e2e`, sem PR)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.21 | CORS só para as origens do SPA; Mailpit em porta fixa e rate limit configurável no AppHost | `CorsTests`, `AppHostTests` |
| 2.22 | `core/auth`: `angular-auth-oidc-client` (PKCE, refresh rotativo com trava entre abas, `sessionStorage`), guards `canMatch`, `/entrar`, `/auth/callback`; configuração em tempo de execução (`config.json`) | `auth.spec.ts`, `load-app-config.spec.ts`, e2e `shell.spec.ts` |
| 2.23 | Shell com usuário, navegação e "Sair" (logout no servidor) | `shell.spec.ts` (Vitest) |
| 2.24 | "Minha conta": perfil, TOTP (QR code, chave, confirmação, códigos de recuperação), código por e-mail, sessões com "Encerrar"; SignalStore e erros traduzidos | `account-store.spec.ts`, `account-components.spec.ts` |
| 2.25 | E2E do critério de aceite contra o AppHost | `e2e/account-flow.spec.ts` |

**ADR:** [0016 — Autenticação no SPA e "minha conta"](adr/0016-autenticacao-no-spa.md).

**Critérios de aceite da Etapa 2:**

| Critério | Status | Evidência |
| --- | --- | --- |
| Fluxo e2e: cadastro → confirmação de e-mail (Mailpit) → login → ativação de TOTP → logout → login com TOTP | ✅ | `e2e/account-flow.spec.ts` (Playwright contra o AppHost; no CI com `E2E_MAILPIT_URL`) |
| Refresh token rotativo: reutilizar um antigo revoga a família inteira | ✅ | `OidcFlowTests.Reusing_an_old_refresh_token_revokes_the_whole_family` |
| 6ª tentativa de senha errada bloqueia a conta por 15 min | ✅ | `OidcFlowTests.Sixth_attempt_after_five_wrong_passwords_is_blocked_for_15_minutes` |

**Última verificação local (PR C):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 415 testes .NET em 10 projetos e 14 do AppHost, 23 Vitest, `npm run lint`, `npm run build`, `npm audit` e pacotes .NET sem vulnerabilidades. Playwright: 5 de 5 contra o AppHost (duas rodadas, incluindo o fluxo completo) e 4 contra o `ng serve` (o fluxo completo é pulado sem Mailpit).

**Pendências do PR C:**
1. CSP restritiva e demais cabeçalhos da seção 7.3 (HSTS, `Referrer-Policy`), que são a defesa principal dos tokens no `sessionStorage`.
2. Diretiva `lfCan` e permissões no `GET /api/v1/me` entram com o `PermissionCatalog` (Etapa 3).

### PR B2 — MFA e minha conta (concluído: PR #9 mergeado em 2026-10-16)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.16 | TOTP em duas etapas (`POST /api/v1/me/mfa/totp` e `/confirm`), com 10 códigos de recuperação | `MfaAndProfileTests.Totp_is_enabled_in_two_steps_and_then_required_at_login`, `Confirming_without_starting_the_setup_is_refused` |
| 2.17 | Código por e-mail como segundo fator (`POST /api/v1/me/mfa/email`), enviado pelo Worker a cada login | `Email_code_is_sent_by_the_worker_at_each_login`, `AccountEmailConsumerTests.Sign_in_code_generated_by_the_worker_is_accepted_as_the_email_second_factor` |
| 2.18 | Página `/conta/segundo-fator` (TOTP, e-mail, recuperação); `amr` com `mfa` no token | `Wrong_second_factor_is_refused_and_a_recovery_code_works_only_once`, `SessionAndUserTests.Enabling_mfa_*` |
| 2.19 | `GET /api/v1/me`, `GET /api/v1/me/sessions` (com `current`, sem as sessões de refresh reutilizado) e `DELETE /api/v1/me/sessions/{id}` | `Profile_is_returned_for_the_signed_in_user_only`, `Open_sessions_are_listed_with_the_current_one_and_a_reused_refresh_hides_its_session`, `A_user_ends_their_own_session_but_not_someone_elses` |
| 2.20 | Contrato OpenAPI e cliente TypeScript atualizados (só adições) | `OpenApiContractTests` |

**Migration:** `MfaMethod` (coluna `identity.users.mfa_method`, padrão `None`).
**ADR:** [0015 — MFA e "minha conta"](adr/0015-mfa-e-minha-conta.md).

**Última verificação local (PR B2):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 412 testes .NET em 10 projetos (Testcontainers), 5 Vitest, 4 Playwright, `npm run lint`, sem pacotes vulneráveis. Cobertura: Identity.Application 94,3% e Identity.Domain 100%. A migration `MfaMethod` não tem mudança de modelo pendente.

**Pendências do PR B2:**
1. Desligar o MFA ou trocar o método exige reautenticação (*step-up*), que não está na especificação.
2. Etapa 3: conceder `identity.profile.read` e `identity.profile.manage` a todo usuário autenticado no `PermissionCatalog`.

### PR B1 — servidor OIDC, login e sessões (concluído: PR #8 mergeado em 2026-10-16)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.8 | OpenIddict 7.7.2 (passthrough): `/connect/authorize`, `/connect/token`, `/connect/logout`; cliente `lexflow-web` público com PKCE, cadastrado pelo MigrationService (`IDatabaseSeeder`) | `OidcFlowTests.Login_with_pkce_issues_tokens_bound_to_a_new_session`, `OidcStoreTests.Web_client_is_public_requires_pkce_and_seeding_twice_keeps_a_single_client` |
| 2.9 | Validação local dos access tokens como esquema padrão (substitui a ADR 0008) | `OidcFlowTests.Access_token_authenticates_api_requests` (sem esquema de teste) |
| 2.10 | Refresh rotativo, tolerância zero de reuso; access token de 10 min | `OidcFlowTests.Reusing_an_old_refresh_token_revokes_the_whole_family` |
| 2.11 | Sessão ligada à autorização (`sid`): renovada a cada troca, recusada se revogada ou expirada; logout e redefinição de senha encerram a sessão e revogam os tokens | `Refresh_renews_the_session_and_is_refused_once_the_session_ends`, `Logout_ends_the_session_and_revokes_its_tokens`, `Password_reset_ends_every_open_session` |
| 2.12 | Páginas Razor pt-BR (`/conta/entrar`, `cadastro`, `confirmar-email`, `esqueci-a-senha`, `redefinir-senha`); cookie de login apagado ao emitir o código | `Account_pages_are_served_in_portuguese`, `Login_cookie_is_dropped_after_the_code_so_a_new_authorization_asks_for_the_password`, `AccountMessagesTests` |
| 2.13 | Bloqueio por tentativas; e-mail não confirmado não entra | `Sixth_attempt_after_five_wrong_passwords_is_blocked_for_15_minutes`, `Unconfirmed_email_cannot_sign_in` |
| 2.14 | Rate limit por IP (10/min) nas páginas de conta e em `/api/v1/auth/*` | `Public_account_endpoints_are_rate_limited_per_ip` |
| 2.15 | Limpeza diária de tokens no Worker | `OidcStoreTests.Cleanup_job_runs_against_the_real_tables` |

**Tabelas novas:** `identity.openiddict_applications`, `openiddict_authorizations`, `openiddict_scopes`, `openiddict_tokens` (migration `OpenIddict`).
**ADR:** [0014 — Servidor OIDC, login e sessões](adr/0014-servidor-oidc-login-e-sessoes.md).

**Critérios de aceite da etapa:**

| Critério | Status | Evidência |
| --- | --- | --- |
| Refresh token rotativo: reutilizar um antigo revoga a família inteira | ✅ | `OidcFlowTests.Reusing_an_old_refresh_token_revokes_the_whole_family` |
| 6ª tentativa de senha errada bloqueia a conta por 15 min | ✅ | `OidcFlowTests.Sixth_attempt_after_five_wrong_passwords_is_blocked_for_15_minutes` |
| Fluxo e2e cadastro → confirmação → login → TOTP → logout → login com TOTP | Pendente | depende do PR B2 (TOTP) e do PR C (telas e Playwright) |

**Última verificação local (PR B1):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 397 testes .NET em 10 projetos (Testcontainers), 5 Vitest, 4 Playwright, `npm run lint`, sem pacotes vulneráveis, contrato OpenAPI sem mudança. Cobertura: Identity.Application 96,5% e Identity.Domain 100%. A migration `OpenIddict` não tem mudança de modelo pendente.

**Pendências do PR B1:**
1. `ForwardedHeaders` no deploy (IP real do rate limit e HTTPS atrás de proxy).
2. ~~PR B2: sessão de refresh reutilizado; MFA; `/api/v1/me`~~: feitos no PR B2 (a sessão some da lista e não renova).
3. Client Credentials na Etapa 14.

### PR A — contas, senhas e e-mails (concluído: PR #7 mergeado em 2026-10-16, CI verde)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.1 | Convenções de módulo em `ModuleModel`/`IModuleDbContext`, usadas também por contexto que não herda de `ModuleDbContextBase`; marcações de auditoria por configuração (`IsSensitive`, `IsNotAudited`) | `PersistenceTests`, `AuditTrailTests` (existentes); `AccountEmailConsumerTests.Audit_trail_records_the_user_without_secrets_and_never_the_keys` |
| 2.2 | `PlatformIntegrationEvent`: evento sem organização, consumido sem tenant | `OutboxTests.Platform_event_is_published_without_tenant_and_consumed_without_tenant`, `Platform_event_with_organization_is_rejected` |
| 2.3 | Módulo Identity: `ApplicationUser`, `Session` (14 dias sem uso, máximo 30), Identity + EF Core no schema `identity`, migration `Initial`, passkeys fora do modelo (Etapa 16) | `SessionAndUserTests`, `MigrationTests` (Identity) |
| 2.4 | Política de senha: mínimo 12, máximo 128, senhas vazadas (Pwned Passwords, falha aberta, desligável); bloqueio 5 tentativas / 15 min configurado (prova no PR B, com o login) | `PwnedPasswordValidatorTests`, `AccountRulesTests`, `AuthEndpointsTests.Short_password_is_rejected_with_its_code_even_for_an_existing_email` |
| 2.5 | `POST /api/v1/auth/register`, `confirm-email`, `forgot-password`, `reset-password`, públicos com motivo (`AllowPublicAccess`) e sem revelar se o e-mail tem conta | `AuthEndpointsTests` (11 casos), `EndpointSecurityConventionTests`, `SecurityAndCorrelationTests.Public_endpoint_records_its_reason_and_skips_authentication` |
| 2.6 | E-mails de conta em pt-BR (Scriban), enviados pelo Worker a partir do outbox; token gerado no Worker; anel de chaves do Data Protection no banco | `AccountEmailComposerTests`, `AccountEmailConsumerTests`, `AuthEndpointsTests.Link_sent_by_the_worker_confirms_the_email`, `Forgot_password_resets_with_a_single_use_link` |
| 2.7 | Contrato OpenAPI e cliente TypeScript atualizados (só adições) | `OpenApiContractTests` |

**Tabelas:** `identity.users`, `user_claims`, `user_logins`, `user_tokens`, `sessions`, `data_protection_keys`, `processed_integration_events`.
**Telas:** nenhuma (páginas Razor no PR B, SPA no PR C). Os links dos e-mails apontam para `/conta/confirmar-email`, `/conta/redefinir-senha` e `/conta/esqueci-a-senha` no servidor de identidade (`Identity:PublicUrl`), que o PR B cria.
**ADR:** [0013 — Contas, senhas e e-mails de conta](adr/0013-contas-senhas-e-emails.md) (Aceita em 2026-10-16: cadastro que não revela contas e eventos da plataforma aprovados; seção 4.5 da spec atualizada).

**Critérios de aceite da etapa:** nenhum fecha no PR A. O fluxo e2e e o refresh rotativo dependem do OpenIddict (PR B) e das telas (PR C). O bloqueio na 6ª tentativa está configurado, mas só é provado com o login.

**Última verificação local (PR A):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 382 testes .NET em 10 projetos (Docker local, Testcontainers), 5 Vitest e 4 Playwright, `npm run lint`, sem pacotes vulneráveis. Cobertura: Identity.Application 98,2% e Identity.Domain 95,3%; as demais camadas seguem acima de 90%. A migration `Initial` do Identity não tem mudança de modelo pendente.

**Pendências do PR A:**
1. Anel de chaves do Data Protection sem criptografia em repouso: proteger com `IKeyProvider`/cofre antes de produção (ADR 0013).
2. PR B: rate limit por IP nos endpoints públicos de conta junto com o de login; revogar sessões ao redefinir a senha; páginas Razor nos caminhos `/conta/*` dos links.
3. Spec, seção 7.1: a tabela é `identity.users` (snake_case, seção 6.1), e não `identity.Users`.

---

## Etapa 1 — Building blocks (2026-10-16)

**RFs:** base para todos. Feriados e tribunais preparam o RF-03 e o RF-09; a auditoria prepara o RF-45.

Plano aprovado em 2026-10-16, em 3 PRs empilhados e mergeados: **A** #4 (SharedKernel e value objects), **B** #5 (Application.Abstractions e Web.Common) e **C** #6 (infraestrutura, Platform e mensageria).

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
