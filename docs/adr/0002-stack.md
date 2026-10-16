# ADR 0002 — Stack e pacotes da Etapa 0 (com licenças)

- **Status:** Aceita, exceto o item marcado como **Proposta** (SonarAnalyzer)
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

A stack está fixada em `docs/spec/implementacao.md` §2. O `CLAUDE.md` exige conferir a licença de cada pacote adicionado e registrá-la em ADR. Esta ADR é o registro vivo: toda etapa que adicionar pacotes acrescenta linhas às tabelas abaixo.

## Decisão

### Plataforma

| Item | Versão | Observação |
| --- | --- | --- |
| .NET SDK | 10.0.302 (`global.json`, `rollForward: latestFeature`) | Runner de testes Microsoft.Testing.Platform |
| Aspire | 13.6.1 | AppHost via `Aspire.AppHost.Sdk` (ADR 0004) |
| Node.js | 24 (LTS) | CI usa a mesma versão |
| Angular | 22.2 | Standalone, zoneless, Vitest |

### Pacotes NuGet (versões em `Directory.Packages.props`)

| Pacote | Versão | Licença | Uso |
| --- | --- | --- | --- |
| Meziantou.Analyzer | 3.0.303 | MIT | Analisador (build) |
| SonarAnalyzer.CSharp | 10.35.0.4138 | **Sonar Source-Available License v1.0** | Analisador (build) — ver abaixo |
| Aspire.Hosting.* (SqlServer, Redis, RabbitMQ, Azure.Storage, JavaScript, Testing) | 13.6.1 | MIT | AppHost e testes |
| Aspire.Microsoft.Data.SqlClient, Aspire.StackExchange.Redis, Aspire.RabbitMQ.Client, Aspire.Azure.Storage.Blobs | 13.6.1 | MIT | Conexões e health checks na API e no Worker |
| Microsoft.Extensions.Http.Resilience / ServiceDiscovery | 10.11.0 | MIT | ServiceDefaults |
| OpenTelemetry.* | 1.20.0 | Apache-2.0 | Telemetria |
| Serilog.AspNetCore | 10.0.0 | Apache-2.0 | Logs estruturados |
| Microsoft.AspNetCore.OpenApi / ApiDescription.Server | 10.0.13 | MIT | OpenAPI (runtime e build) |
| Scalar.AspNetCore | 2.18.0 | MIT | UI da API (só em Development) |
| Microsoft.Extensions.Hosting | 10.0.13 | MIT | Worker |
| xunit.v3.mtp-v2 | 4.0.2 | Apache-2.0 | Testes |
| Shouldly | 4.3.0 | BSD-3-Clause | Asserções |
| NetArchTest.Rules | 1.3.2 | MIT | Testes de arquitetura |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | MIT | Testes de integração |
| Microsoft.Testing.Extensions.CodeCoverage | 18.12.0 | Microsoft .NET Library License (gratuita) | Cobertura no CI |

**Pins transitivos de segurança.** Ficam em `Directory.Packages.props` com `CentralPackageTransitivePinningEnabled`:

| Pacote | Versão | Motivo |
| --- | --- | --- |
| MessagePack | 2.5.307 | GHSA-qhrr-8q5h-9q3h; vem via Aspire.Hosting |
| System.Security.Cryptography.Pkcs | 10.0.13 | CVE-2026-70162 e CVE-2026-84008; vem via Azure.Identity/SqlClient |

### Pacotes npm (`src/Web/lexflow-web/package.json`)

| Pacote | Licença |
| --- | --- |
| @angular/* (core, router, forms, cdk, material, build, cli) | MIT |
| @jsverse/transloco | MIT |
| angular-eslint, eslint, typescript-eslint | MIT |
| prettier, vitest, jsdom | MIT |
| @playwright/test | Apache-2.0 |
| @axe-core/playwright, axe-core | MPL-2.0 (só ferramenta de teste; não é distribuído no bundle) |

### Ferramentas de CI

| Ferramenta | Licença | Observação |
| --- | --- | --- |
| Gitleaks (CLI v8.30.1, container) | MIT | Usamos a CLI, não a `gitleaks-action`, que exige licença paga em organizações |
| Trivy (`aquasecurity/trivy-action`) | Apache-2.0 | |
| CodeQL | Gratuito para repositórios públicos ou com GitHub Advanced Security | Em repositório privado sem GHAS, a análise roda sem upload (`upload: never`) e o SARIF vira artefato do job |
| oasdiff (`oasdiff/oasdiff-action`) | Apache-2.0 | `review: false`, para não enviar as specs a um serviço externo |

## SonarAnalyzer.CSharp — Proposta (revisão humana)

A spec (§9, Etapa 0, tarefa 2) pede o `SonarAnalyzer.CSharp`. Desde a v10 o pacote não é mais LGPL: usa a **Sonar Source-Available License v1.0 (SSALv1)**.

- **O que a licença permite:** uso interno, inclusive comercial, contanto que não seja num produto que compita com o SonarQube.
- **Como usamos aqui:** só como analisador de build (`PrivateAssets=all`). O pacote não é distribuído com o LexFlow.
- **Decisão pendente:** um humano confirma o uso. Se não for aceito, basta remover as duas linhas de `Directory.Build.props` e `Directory.Packages.props`; Meziantou e os analisadores .NET (`AnalysisLevel=latest-recommended`) continuam ativos.

## Consequências

- Todo pacote novo exige uma linha nas tabelas acima.
- `TreatWarningsAsErrors` inclui os avisos de vulnerabilidade do NuGet (NU1902/NU1903). Um CVE novo quebra o build até ser resolvido com atualização ou pin.
