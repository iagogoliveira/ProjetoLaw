# ADR 0043 — Sincronização de calendários por usuário

- **Status:** Proposta (para revisão no PR #42)
- **Data:** 2026-10-22
- **Etapa:** 7G (RF-24)

## Decisões de negócio

O plano 7A–7G aprovado prevê `ICalendarSyncProvider` com simulador; os adaptadores reais dependem do registro das aplicações OAuth. Em 2026-10-22 o usuário confirmou:

1. Alterações nos dois lados desde a última sincronização ficam pendentes para decisão do usuário.
2. Exclusão externa exige confirmação antes de cancelar o compromisso local.
3. A saída contém somente tipo, data/horário e link autenticado. Não exportar títulos, descrições, locais, partes ou dados do processo, inclusive em itens sigilosos.

## Decisão técnica

- Conexões pertencem ao escritório e ao usuário autenticado, com um vínculo por provedor (Google/Microsoft 365), período explícito semiaberto de até 366 dias e estado ativo/desativado. As permissões pessoais controlam a configuração; leitura e mutação dos recursos continuam exigindo suas permissões e escopos. O job revalida o membro e o acesso atual. Somente itens atribuídos ao titular são considerados.
- Compromissos usam sincronização bidirecional; prazos confirmados são apenas de saída. Tarefas e audiências conservam seus módulos e não são transformadas em compromissos. Cancelar/desconectar não apaga registros locais nem recursos externos. Cancelamento externo de compromisso vira pendência; alterações externas em prazo nunca alteram datas, conferência ou evidência no LexFlow.
- O contrato de saída não possui campos de conteúdo privado. Atualizações preservam os textos que já existem no calendário externo. No retorno, aplicar somente os campos remotos que mudaram em relação ao último snapshot, preservando o título local quando a representação genérica externa não mudou. Conflitos usam snapshots e fingerprints, com nova validação das versões antes da decisão.
- Criação externa usa chave idempotente; atualização usa versão condicional. Retry após gravação externa e antes do commit local não deve duplicar eventos. A ausência numa página nunca significa exclusão; links existentes têm leitura individual, inclusive quando o evento saiu do período. Pendências com mudanças posteriores precisam ser atualizadas antes de aceitar a decisão.
- O simulador é isolado por escritório/usuário/provedor, fica em Infrastructure e é desabilitado por padrão. Quando habilitado para desenvolvimento/testes, a API/interface identificam explicitamente a simulação. Não há OAuth, tokens nem tráfego Google/Graph nesta entrega; as fixtures representam o contrato normalizado do simulador, sem afirmar validação dos protocolos reais. Nenhum pacote novo.
- O processamento simulado usa o lock transacional de Scheduling, RLS e `xmin`. O estado do simulador de desenvolvimento é persistido em uma terceira tabela do escritório, compartilhada pela API e pelo Worker e participante da transação local. O fake em memória dos testes permite simular efeitos externos independentes e verificar idempotência após falha antes do commit; isso não equivale à atomicidade de uma integração real. Antes dos adaptadores reais, revisar a execução de rede fora de transações longas, segredos protegidos por usuário/tenant, expiração/revogação, paginação/delta e testes de contrato com payloads oficiais.
- Compromissos externos que não podem ser representados pelo modelo atual (por exemplo, recorrência ou dia inteiro) ficam identificados como não suportados; não converter silenciosamente horários nem criar recorrência no domínio. Páginas e lotes têm limites explícitos; uma resposta parcial não é tratada como sincronização completa.

## Verificação

Domínio, simulador com fixtures normalizadas, idempotência, concorrência, autorização por usuário/tenant/processo, RLS, API, contrato aditivo, testes de interface e acessibilidade. Verificação integral aprovada em 2026-10-23: 1.215 testes .NET, gate mínimo de 80%, modelos e contrato aditivo, 196 Vitest e duas rodadas de 22 Playwright com acessibilidade; resultados e limites no progress. O simulador limita páginas/lotes e vínculos históricos a 1.000 itens por conexão e o armazenamento a 10.000 por usuário/provedor; revisar antes da integração real. A revisão jurídica do 7A e o fornecedor real de publicações continuam pendentes; o 7G simulado não encerra essas pendências nem os adaptadores OAuth.
