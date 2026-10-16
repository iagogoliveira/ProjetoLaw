# ADR 0003 — Endpoints de saúde em todos os ambientes

- **Status:** Proposta
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

O template padrão do Aspire mapeia os endpoints de saúde só em Development, para não expor informação. Porém o Azure Container Apps (§8.3) precisa de probes de liveness e readiness em produção. A spec (Etapa 0) pede `/health/live` e `/health/ready`, mas não diz em quais ambientes.

## Decisão

- **Os dois endpoints são mapeados em todos os ambientes**, ambos anônimos (`AllowAnonymous`).
- **`/health/live`:** avalia só o check `self` (tag `live`), que indica se o processo está vivo. Não depende de nada externo, para evitar reinícios em cascata.
- **`/health/ready`:** avalia todas as dependências (SQL, Redis, RabbitMQ e Blob). Responde `503` se alguma estiver fora.
- **Resposta do `/health/ready`:** JSON com `status`, duração e, por check, `name`, `status`, duração e tags. Não inclui mensagens de exceção, connection strings nem hosts.
- **Traces:** requisições para `/health/*` ficam fora dos traces do OpenTelemetry.

## Consequências

- O que fica exposto publicamente são só nomes e estados das dependências.
- Na borda (Front Door/WAF, Etapa 16 ou deploy), pode valer restringir `/health/*` à rede interna do Container Apps. Fica como pendência de infraestrutura.
