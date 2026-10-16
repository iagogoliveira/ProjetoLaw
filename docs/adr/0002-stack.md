# ADR 0002 — Stack e licenças dos pacotes

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 0 (atualizada a cada pacote novo)

## Contexto

A stack está fixada em `docs/spec/implementacao.md`, seção 2: .NET 10, EF Core 10 + Npgsql, PostgreSQL 17+, Angular, Wolverine, Hangfire e OpenIddict. Em 2026-10-16 o responsável confirmou **PostgreSQL**, que é gratuito. O `CLAUDE.md` dizia SQL Server 2025 e foi corrigido. Pelo `CLAUDE.md`, nenhum pacote entra sem a licença conferida e registrada aqui. Ficam proibidos MediatR, AutoMapper e MassTransit (licenças comerciais).

## Decisão

Versões centralizadas em `Directory.Packages.props` (CPM), com o SDK fixado em `global.json` (10.0.302). As licenças abaixo foram conferidas no `.nuspec` ou no `package.json` publicado de cada versão.

### Backend (NuGet)

| Pacote | Versão | Licença | Uso |
| --- | --- | --- | --- |
| Aspire.AppHost.Sdk / Aspire.Hosting.* (PostgreSQL, Redis, RabbitMQ, JavaScript, Testing) | 13.6.1 | MIT | Orquestração local e testes do AppHost |
| Microsoft.Extensions.Http.Resilience / ServiceDiscovery | 10.11.0 | MIT | ServiceDefaults |
| OpenTelemetry.* (Extensions.Hosting, Exporter.OTLP, Instrumentation.AspNetCore/Http/Runtime) | 1.20.0 | Apache-2.0 | Telemetria |
| Serilog.AspNetCore | 10.0.0 | Apache-2.0 | Logs estruturados |
| Microsoft.AspNetCore.OpenApi | 10.0.13 | MIT | Documento OpenAPI |
| Scalar.AspNetCore | 2.18.0 | MIT | UI da API (só em Development) |
| AspNetCore.HealthChecks.NpgSql / Redis | 9.0.0 | Apache-2.0 | `/health/ready` |
| RabbitMQ.Client | 7.2.2 | Apache-2.0 OR MPL-2.0 | Health check AMQP (Wolverine assume na Etapa 1) |
| AWSSDK.S3 | 4.0.104.4 | Apache-2.0 | Health check do armazenamento; base do `IObjectStorage` |
| Microsoft.Extensions.Configuration.Binder | 10.0.13 | MIT | Binding de opções |
| MessagePack (pin transitivo) | 2.5.307 | MIT | Corrige GHSA-qhrr-8q5h-9q3h, que vinha via Aspire.Hosting |
| Meziantou.Analyzer | 3.0.303 | MIT | Analisador em todos os projetos |
| xunit.v3 | 4.0.2 | Apache-2.0 | Testes (Microsoft.Testing.Platform) |
| Shouldly | 4.3.0 | BSD-3-Clause | Asserções |
| NetArchTest.eNhancedEdition | 1.4.5 | MIT | Testes de arquitetura (fork mantido; `NetArchTest.Rules` está parado desde 2021) |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | MIT | `WebApplicationFactory` |
| Testcontainers (+ PostgreSql, Redis, RabbitMq) | 4.16.0 | MIT | Integração com containers reais |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | MIT | Regras do CLAUDE.md no build (Etapa 1, ADR 0006) |
| Mono.Cecil | 0.11.6 | MIT | Varredura de IL nos testes de arquitetura (Etapa 1) |
| FluentValidation | 12.2.0 | Apache-2.0 | Validadores dos comandos e consultas (Etapa 1, ADR 0007) |
| Scrutor | 7.0.0 | MIT | Registro por varredura de assembly e decorators (Etapa 1) |
| Microsoft.Extensions.DependencyInjection(.Abstractions) / Logging.Abstractions | 10.0.13 | MIT | Application.Abstractions |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 | MIT | `FakeTimeProvider` (o "FakeClock" da seção 13.3) |
| Microsoft.Extensions.Diagnostics.Testing | 10.11.0 | MIT | `FakeLogCollector` nos testes |
| Microsoft.AspNetCore.TestHost | 10.0.13 | MIT | Testes do pipeline HTTP do Web.Common |
| Microsoft.EntityFrameworkCore(.Relational/.Design) | 10.0.13 | MIT | Persistência (Etapa 1) |
| Npgsql / Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | Provedor PostgreSQL |
| EFCore.NamingConventions | 10.0.1 | Apache-2.0 | snake_case (seção 6.1) |
| WolverineFx (+ EntityFrameworkCore, Postgresql, RabbitMQ, RuntimeCompilation) | 6.51.0 | MIT | Outbox/inbox e transporte (ADR 0009); transitivas JasperFx, Weasel, Newtonsoft.Json: MIT |
| Hangfire.Core / Hangfire.NetCore | 1.8.25 | LGPL-3.0 | Jobs (seção 2.1) |
| Hangfire.PostgreSql | 1.21.1 | LGPL-3.0 | Armazenamento dos jobs; transitivas Dapper e Dapper.AOT: Apache-2.0 |
| MailKit (MimeKit) | 4.18.1 | MIT | SMTP |
| System.Security.Cryptography.Pkcs (pin transitivo) | 10.0.13 | MIT | Corrige GHSA-846f-f764-76mx (alta) e GHSA-2fmv-rv2c-59x7, transitivas do MimeKit |
| coverlet.MTP | 10.1.0 | MIT | Cobertura (ADR 0012); o coletor da Microsoft tem licença proprietária |
| dotnet-ef (ferramenta local) | 10.0.13 | MIT | Geração de migrations |
| Microsoft.Extensions.Identity.Core / Stores, Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 | MIT | ASP.NET Core Identity (Etapa 2, ADR 0013) |
| Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.13 | MIT | Anel de chaves compartilhado por API e Worker (ADR 0013) |
| Scriban | 7.5.1 | BSD-2-Clause | Templates dos e-mails transacionais (ADR 0013) |

### Frontend (npm)

| Pacote | Versão | Licença |
| --- | --- | --- |
| @angular/* (core, router, forms, cdk, material, build, cli) | 22.2.x | MIT |
| @jsverse/transloco | 8.4.0 | MIT |
| angular-eslint / typescript-eslint / eslint | 22.6.0 / 8.71.0 / 10.x | MIT |
| prettier | 3.x | MIT |
| vitest / jsdom | 5.0.x / 30.x | MIT |
| @playwright/test | 1.64.1 | Apache-2.0 |
| @axe-core/playwright | 4.14.0 | MPL-2.0 (só em dev, sem redistribuição) |
| @hey-api/openapi-ts | 0.99.0 | MIT (dev; gera `src/app/api`, ADR 0012) |
| js-yaml (override npm) | 4.3.2 | MIT | Corrige GHSA-52cp-r559-cp3m, GHSA-5p4m-2wfm-xmqj e GHSA-2883-xcg3-v3hh (altas), transitivas do `@hey-api/openapi-ts` |

### Ferramentas de CI

`oasdiff` 1.33.0 (Apache-2.0), baixado da release oficial no job `openapi`.

### Imagens de container (ambiente local e testes)

`pgvector/pgvector:pg17-trixie` (PostgreSQL License), `redis:8.8` (RSALv2/SSPLv1/AGPLv3; uso interno permitido, ver pendência), `rabbitmq:4.3-management` (MPL-2.0), `rustfs/rustfs:1.0.2` (Apache-2.0, ver ADR 0004), `gotenberg/gotenberg:8.38.1` (MIT), `clamav/clamav:1.5.4-debian` (GPL-2.0, serviço separado) e `axllent/mailpit:v1.31.6` (MIT).

### Decisões técnicas menores

- **Execução de testes:** o xUnit v3 4.x roda sobre o Microsoft.Testing.Platform, e no SDK 10 o modo VSTest deixou de funcionar. O `global.json` define `"test": { "runner": "Microsoft.Testing.Platform" }`, e os testes rodam com `dotnet test --project <proj>` ou `dotnet test --solution LexFlow.sln`.
- **Analisadores:** `AnalysisLevel=latest-recommended`, `TreatWarningsAsErrors=true` e Meziantou. O SonarAnalyzer ficou fora (ver ADR 0005).
- **Gitleaks Action:** é gratuita para contas pessoais. Se o repositório passar para uma organização, o secret `GITLEAKS_LICENSE` passa a ser obrigatório.

## Consequências

- Todo pacote novo exige uma linha nesta tabela no mesmo PR.
- A licença do Redis 8 (tripla RSALv2/SSPLv1/AGPLv3) permite uso interno. Se o modelo de distribuição mudar, avaliar Valkey (BSD), que a seção 2.1 já prevê como alternativa.
