# ADR 0012 — Contrato da API versionado e portão de cobertura

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 1

## Contexto

A definição de pronto (seção 1.4) exige OpenAPI atualizado, cliente TypeScript regenerado, nenhuma *breaking change* não planejada (`oasdiff`, seção 8.2) e cobertura de pelo menos 80% em Domain e Application.

## Decisão

- **`docs/openapi/v1.json` versionado.** O `OpenApiContractTests` compara o documento servido pela API, sem `servers`, com o arquivo do repositório. Para atualizar: `LEXFLOW_UPDATE_OPENAPI=1 dotnet test --project tests/LexFlow.Api.IntegrationTests`. Isso evita gerar o documento no build, o que exigiria subir a API sem as dependências.
- **`oasdiff` 1.33.0 (Apache-2.0)** no job `openapi` do CI: compara o contrato do PR com o da base e falha em `ERR`. Na primeira versão do contrato, a comparação é pulada.
- **Cliente Angular gerado** com `@hey-api/openapi-ts` 0.99.0 (MIT), plugin `@hey-api/client-angular`, em `src/app/api`. É gerado do arquivo versionado e nunca editado à mão. O CI regenera e falha se houver diferença.
- **Cobertura:**
  - coletada com `coverlet.MTP` 10.1.0 (MIT) em todos os projetos de teste. O coletor da Microsoft foi descartado por ter licença proprietária;
  - `scripts/coverage_gate.py` une os relatórios: uma linha conta como coberta se algum teste, unitário ou de integração, a executou;
  - o portão exige 80% nos pacotes `*.Domain`, `*.Application`, `SharedKernel` e `Application.Abstractions`;
  - código gerado (`*.g.cs`) fica de fora.

## Consequências

- Toda mudança de endpoint precisa atualizar o contrato e o cliente no mesmo PR, e o CI aponta o que falta.
- Cobertura inicial: Application.Abstractions 98,3%, SharedKernel 96,9%, Platform.Domain 97,5% e Platform.Application 90,3%.
