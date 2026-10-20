# ADR 0036 — Telas de publicações, triagem e tempo real

- **Status:** Aceita (PR #35 mergeado em 2026-10-19)
- **Data:** 2026-10-19
- **Etapa:** 6 (PR 6E)
- **Revisão:** PR [#35](https://github.com/iagogoliveira/ProjetoLaw/pull/35), merge `af904e3`, confirmado com todos os checks aprovados.

## Contexto

O plano 6A–6E já aprovado entrega no 6E a operação visual dos contratos implementados nos PRs #31–#34. As decisões de triagem, permissões de destino, descarte justificado e histórico de fechamento da ADR 0035 permanecem no backend. O fornecedor real continua pendente; não declarar a Etapa 6 integralmente concluída.

## Decisão

1. Feature `publications`, carregada sob demanda: fila em `/publicacoes`, fechamento em `/publicacoes/fechamento` e fontes/integrações em `/publicacoes/fontes`. Guards e `*lfCan` seguem leitura, triagem, distribuição, fechamento e gestão de integrações. Quem só gerencia integrações pode acessar fontes sem depender de `publications.read` ou da administração de usuários.
2. Componentes standalone, OnPush, formulários reativos e signalStores. HTTP exclusivamente em `data/`, usando o cliente OpenAPI. Filtros da fila na URL, cursor no servidor e proteção contra respostas antigas ao trocar filtros/seleção. Texto de publicação renderizado como texto; destaque de CNJ sem HTML externo. Atalhos J/K, T/P ignoram inputs, seleção, modificadores e campos editáveis; podem ser desativados, conforme WCAG 2.1 AA.
3. Acrescentar consultas mínimas de contador de pendências visíveis e opções de membros/processos para a operação, sem permissões de configuração. Alcance, sigilo e tenant são reavaliados no servidor; contagens não derivam do snapshot limitado de notificações. Opções não substituem autorização/validação ao gravar.
4. Consumir o hub existente com **`@microsoft/signalr` 10.0.11**, licença **MIT**, verificada no registro npm e na [licença oficial](https://github.com/dotnet/aspnetcore/blob/v10.0.11/LICENSE.txt). Cliente do protocolo, sem SDK de nuvem. Transporte LongPolling com `withCredentials: false` (API usa Bearer e CORS sem cookies), Bearer em cabeçalho e escritório no cabeçalho existente; nunca token na URL. Reconexão/reinscrição, recuperação REST e descarte ao sair/trocar escritório. Não registrar conteúdo ou credenciais nos logs do cliente. [Documentação oficial](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0).
5. Notificações persistidas são referências autorizadas; leitura, paginação e reconexão respeitam o backend do 6B. Novidades invalidam contador/fila sem apagar formulário em edição; informar que há novidades para atualização explícita. Revalidar também ao retomar o foco e periodicamente para refletir triagem por outro usuário.
6. Fontes mostram ativação/desativação, execuções/alertas e configuração DataJud sem retornar a chave existente. Campo de chave limpo após envio; modos de provedor identificam simulação/integração desabilitada. Andamentos usam as consultas e linha do tempo existentes, com entrada manual protegida por `cases.edit`.
7. Fechamento mostra data de recebimento em São Paulo, pendências, captura incompleta e histórico paginado. Confirmação humana explícita antes de criar destinos/fechar; tela apresenta falhas estáveis e mantém o formulário para correção. Calculadora e agenda completas ficam na Etapa 7.

## Decisão operacional aprovada em 2026-10-19

O usuário aprovou “Tentar captura agora” para recuperar captura após as retentativas automáticas: `settings.integrations.manage` em escopo de escritório, fonte ativa, auditoria e deduplicação existentes, execução no Worker. O agendamento automático das 05h–07h permanece como está. A operação também permite verificar o fluxo de captura em qualquer horário.

O dia usa São Paulo e deve estar entre a última ativação aplicável da fonte e hoje, conforme o `CaptureSchedule` existente. A consulta de execuções recebe dia opcional, mantendo hoje como padrão. Pedido e auditoria `CaptureRetryRequested` entram com o evento no outbox da mesma transação; o consumidor com inbox agenda o job existente com `force=true`. A API responde 202 com identificador da solicitação, sem afirmar que a captura já terminou. Fonte desativada antes da execução continua impedindo o job. A solicitação usa a trilha auditada e o outbox existentes, sem tabela própria; não altera publicações ou assinaturas anteriores.

## Correção do vínculo de capturas com períodos

O Playwright com relógio real detectou duas leituras distintas de `IClock.UtcNow` na ativação: a fonte e seu período divergiam em microssegundos e o fechamento recusava uma captura concluída. Usar um único instante por transição. Acrescentar `CaptureRun.SourcePeriodId` nullable, com FK e índice, para identificar o período capturado inclusive em reativações com o mesmo timestamp; fonte e período são lidos em uma única consulta antes de chamar o provedor. A comparação por período prevalece sobre o fallback legado por data.

A migration aditiva `CaptureRunSourcePeriod` associa somente históricos inequívocos: timestamp exato do período ou da última ativação ainda aberta. Não reescreve timestamps, períodos, publicações ou fechamentos assinados. Históricos sem associação segura continuam com referência nula e a validação legada estrita; eventual divergência antiga em período já encerrado exige avaliação técnica, sem atribuição presumida. Regressões cobrem relógio progressivo, reativação no mesmo instante e upgrade/reversão preservando o histórico. Nenhuma migration aplicada foi editada.

## Licenças das dependências adicionadas

Conferidos o pacote oficial e as entradas novas do lockfile. Dependências transitivas MIT: `abort-controller` 3.0.0, `event-target-shim` 5.0.1, `eventsource` 2.0.2, `node-fetch` 2.7.0, `tr46` 0.0.3, `whatwg-url` 5.0.0, `psl` 1.15.0, `querystringify` 2.2.0, `requires-port` 1.0.0, `set-cookie-parser` 2.7.2, `universalify` 0.2.0, `url-parse` 1.5.10 e `ws` 7.5.13. `fetch-cookie` 2.2.0 usa Unlicense; `tough-cookie` 4.1.4, BSD-3-Clause; `webidl-conversions` 3.0.1, BSD-2-Clause. São licenças permissivas, sem copyleft ou SDK de nuvem; manter os avisos das distribuições e conferir vulnerabilidades no gate final. Não há pacote .NET novo.

## Verificação

Vitest de cada store e transporte/descartes relevantes; testes de integração, autorização, sigilo e SecurityTests das consultas adicionadas; OpenAPI aditivo e cliente regenerado. Playwright contra AppHost com acessibilidade, fluxo de captura simulada → fila → triagem → destino/fechamento, fontes/DataJud, andamentos e acesso negado; duas rodadas quando possível. Gates completos, resultados, limitações e pendências no progress. Atualizar manual, guia local, AGENTS/CLAUDE e handoff na entrega; commit local, push/PR a pedido.
