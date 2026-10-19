# ADR 0034 — DataJud, histórico de andamentos e checkpoints de captura

**Status:** Aceita (PR #33 mergeado em 2026-10-19)

**Data:** 2026-10-19

## Contexto

O plano da Etapa 6 foi aprovado com fake e DataJud enquanto não há fornecedor contratado de publicações/intimações. DataJud disponibiliza metadados e movimentos públicos e não substitui intimações. Em 2026-10-19 o usuário aprovou importar todo o histórico disponível na primeira consulta, preservando as datas dos atos; consultas seguintes acrescentam somente novidades, sem duplicar. Autorizou também reiniciar o AppHost local quando necessário, preservando os cadastros.

## Decisão

- Consultar processos judiciais ativos por contrato de Litigation, sem acesso ao seu DbContext. Distribuir as consultas em janelas de seis horas por um deslocamento estável do processo.
- Usar somente o host oficial do DataJud e aliases de tribunais conhecidos. Enviar a chave por requisição, sem headers compartilhados entre escritórios; limitar concorrência e tempo de resposta. Não adicionar pacote ou SDK.
- Configurar o provedor por escritório com `settings.integrations.manage` em escopo de escritório. Chaves são protegidas por ASP.NET Data Protection, com finalidade que inclui o tenant, e excluídas da auditoria e das respostas. API/Worker reutilizam o key ring persistido existente; sua proteção operacional continua necessária.
- Paginar documentos por `search_after`; manter checkpoint por processo/tribunal e revisão da configuração. Cada página e seus andamentos são confirmados na mesma transação. Falha mantém o checkpoint para retomada. Nova consulta completa relê documentos para encontrar movimentos históricos incluídos tardiamente.
- Usar `@timestamp` com desempate por `id.keyword`, vinculando o cursor ao CNJ e ao alias. Em 2026-10-19 uma consulta técnica `match_none`/`size: 0` ao endpoint oficial TJSP confirmou HTTP 200, sem timeout/falha de shards nem registros retornados. A chave pública foi obtida da wiki somente em memória e não foi gravada no repositório. O CI usa exclusivamente fixtures. Cursor inválido/que não avança reinicia a varredura na próxima tentativa; os movimentos já salvos continuam deduplicados.
- Identificar o movimento por hash determinístico de tribunal, instância, data, código do ato, órgão e códigos/valores dos complementos. A ordem dos movimentos/complementos e correções dos rótulos da TPU não alteram a identidade nem geram outro andamento; o texto originalmente coletado fica preservado. Índice único e lock transacional por processo protegem reexecuções concorrentes. A data do ato é preservada; coleta usa `IClock`.
- Retornar códigos estáveis para indisponibilidade, credencial recusada, resposta inválida, tribunal não suportado e cursor inválido. Não registrar corpo externo ou chave em logs.
- Manter o fake de publicações separado do provedor de movimentações. O fornecedor real permanece pendente de escolha, contrato, documentação e acesso. Triagem e telas continuam nos PRs 6D e 6E.

## Consequências e verificação

Há migration aditiva com isolamento RLS e novos contratos aditivos da API. Os testes usam respostas anonimizadas e HTTP controlado, sem chamadas reais ao CNJ no CI. Testes direcionados durante a implementação; todos os gates do projeto no fechamento. O manual e o guia local serão atualizados com a configuração efetivamente entregue.

## Referências

- [Acesso oficial](https://datajud-wiki.cnj.jus.br/api-publica/acesso/)
- [Endpoints por tribunal](https://datajud-wiki.cnj.jus.br/api-publica/endpoints/)
- [Glossário](https://datajud-wiki.cnj.jus.br/api-publica/glossario/)
- [Paginação com search_after](https://datajud-wiki.cnj.jus.br/api-publica/exemplos/exemplo3/)
