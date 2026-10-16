# ADR 0004 — Detalhes do AppHost Aspire 13

- **Status:** Proposta
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

O exemplo da §8.1 da spec foi escrito para uma versão anterior do Aspire. Três pontos diferem no Aspire 13.6.1:

1. **`AddNpmApp` deixou de existir.** Apps JavaScript agora usam o pacote `Aspire.Hosting.JavaScript` (`AddJavaScriptApp`, `AddViteApp` etc.).
2. **O AppHost pode depender do "Aspire CLI bundle"** (`AspireUseCliBundle`), que exige o Aspire CLI instalado na máquina.
3. **As imagens de container não tinham versão definida.**

## Decisão

- **Web:**
  - `AddJavaScriptApp("web", "../../Web/lexflow-web", "start")` com `WithNpm(installCommand: "ci")`;
  - o Aspire cria um recurso instalador que roda `npm ci` antes de subir;
  - porta fixa 4200 (`isProxied: false`), usada pelo e2e.
- **`AspireUseCliBundle=false` explícito**, com o aviso `ASPIRE010` suprimido. O DCP e o dashboard vêm dos pacotes NuGet, então `dotnet run --project src/Aspire/LexFlow.AppHost` funciona sem instalar o Aspire CLI, no dev e no CI.
- **Imagens fixadas por versão menor:**

  | Imagem | Tag |
  | --- | --- |
  | `mssql/server` | `2025-latest` |
  | `opensearchproject/opensearch` | `3.9.0` |
  | `gotenberg/gotenberg` | `8.38` |
  | `clamav/clamav` | `1.5` |
  | `axllent/mailpit` | `v1.31` |

  Redis, RabbitMQ e Azurite usam as tags padrão das integrações Aspire 13.6.1.
- **Health checks dos containers:**

  | Container | Check |
  | --- | --- |
  | OpenSearch | `/_cluster/health`, com o plugin de segurança desligado só no ambiente local |
  | Gotenberg | `/health` |
  | Mailpit | `/livez` |
  | ClamAV | `zPING` → `PONG` pelo protocolo clamd |

- **API:** só é considerada saudável quando `/health/ready` responde 200. A Web espera a API (`WaitFor`).
- **`launchSettings.json` do AppHost:** perfis `https` (padrão) e `http`. O CI usa o `http`, com `ASPIRE_ALLOW_UNSECURED_TRANSPORT`.

## Consequências

- Subir pela primeira vez é lento: download das imagens e carga das assinaturas do ClamAV, que pode levar alguns minutos.
- As portas fixas (9200, 3000, 3310, 8025, 1025, 4200) podem colidir com serviços locais.
- Atualizar o Aspire exige mudar `AspireVersion` em `Directory.Packages.props` e o SDK em `LexFlow.AppHost.csproj`.
