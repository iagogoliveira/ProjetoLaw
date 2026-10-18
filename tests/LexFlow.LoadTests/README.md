# Testes de carga (k6)

Ferramenta: [k6](https://k6.io) pela imagem oficial `grafana/k6` (licença AGPL-3.0, só no desenvolvimento, sem ir
para o produto; ADR 0026). Os scripts rodam contra o AppHost local.

## Lista de processos (Etapa 5, critério 1)

> Lista com 100 mil processos no tenant responde em < 1,5 s (p95) com filtro por área e responsável.

```bash
dotnet run --project src/Aspire/LexFlow.AppHost   # em outro terminal, até a API e o SPA responderem
bash tests/LexFlow.LoadTests/run-cases-list.sh
```

- `prepare-load.spec.ts` (Playwright) cria um escritório só para a carga, pelo fluxo real de cadastro e login.
- `seed-cases.sql` insere os processos (metade em cada área, um em cada cinco com o usuário da carga como responsável,
  1% sigilosos) e roda `ANALYZE`.
- `cases-list.js` mede `GET /api/v1/cases` filtrado por área e por área e responsável; os limites (`thresholds`)
  falham o k6 se o p95 passar de 1,5 s ou se mais de 1% das requisições falhar.

O seed fica no banco do AppHost (volume persistente), num escritório próprio: não aparece para as outras contas.
