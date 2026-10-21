# Passagem de bastão entre agentes (Claude Code ⇄ Codex)

Este arquivo é a conversa entre os agentes que trabalham no LexFlow. Quem começa uma sessão **lê a última entrada antes de tudo**. Quem termina uma sessão, ou para no meio de um PR, **acrescenta uma entrada no topo**.

Como funciona:
- O estado oficial de cada etapa continua no `docs/progress.md`. Aqui fica o que não cabe lá: o que estava em andamento, o que ficou combinado com o usuário na conversa e o que o próximo agente deve fazer primeiro.
- As regras gerais ficam no `CLAUDE.md` (lido pelo Claude Code) e no `AGENTS.md` (lido pelo Codex). Mudou uma regra ou o "Onde paramos"? Atualize os dois.
- Cada entrada tem: data, agente, branch, o que foi feito, o estado (commitado, com push, PR aberto ou mergeado), o que falta e as decisões novas do usuário.
- Entradas antigas não são apagadas, para o histórico ficar completo.

---

## 2026-10-21 — Codex (7D implementado, verificado e commitado localmente; sem push/PR)

**Pedido e decisões:** usuário aprovou as duas propostas restantes: “Pode seguir com essas propostas”. Indicadores e pendências seguem as definições no progress/ADR 0040; alertas obrigatórios in-app para perda, calendário e fatal no dia; resumo opt-in às 07:30 de São Paulo, com agenda atribuída, prazos abertos em sete dias e publicações pendentes visíveis. Continua valendo a regra já aprovada para processos ativos sem andamento, padrão 30 dias.

**Estado:** branch codex/etapa-7d-controladoria-notificacoes; implementação e fechamento em commit local, sem push/PR da implementação. Remoto ainda na arrumação pós-merge 22f1479. .codex/ local não rastreado preservado e excluído do commit. ADR 0040 proposta, sem número de PR inventado.

**Feito:** painel por API com itens/totais/indicadores autorizados via contratos dos módulos e Dapper parametrizado; preferências pessoais e regras obrigatórias auditadas com RLS; piso de alertas que o usuário não consegue suprimir; lembrete fatal no sino com traduções e navegação ao processo; jobs por escritório e usuário, conta confirmada, escolhas de seções, escopo/sigilo, HTML escapado, lock/ledger e retry do resumo. OpenAPI e cliente regenerados oficialmente; ambos os manuais atualizados. Telas de prazos ficam no 7E, painel/preferências/agenda no 7F; demais canais futuros.

**Verificação integral aprovada em 2026-10-21:** builds Release da solução e Debug AppHost/API/Worker com zero avisos/erros; format; 1.161 testes .NET em 20 projetos, zero falhas/ignorados. API 281; Platform.Unit 63/Integration 55; BuildingBlocks.Integration 51; Scheduling.Unit 169; Security 12; AppHost 14. Gate 80% aprovado (Platform.Domain 100,0%, Application 97,7%; Scheduling.Domain 98,8%, Application 99,4%); dependências .NET sem vulneráveis e seis modelos EF sem pendências. OpenAPI 162 caminhos/287 schemas; oasdiff v1.33.0 sem quebra nem nova exceção; audit zero, lint, 134 Vitest e build Angular aprovados. Duas rodadas completas consecutivas de 17 Playwright contra AppHost, com WCAG 2.1 AA; uma preparação opcional de carga ignorada por rodada sem LOAD_OUTPUT. SMTP/Mailpit real, concorrência, retry, opt-out e revogação por sigilo verificados. AppHost.Tests terminou antes da instância manual. Logs finais TestResults/7d-final-validation; tentativas direcionadas preservadas em TestResults/7d-directed. Orquestrador conferiu diff, contagens/logs; segunda revisão Sol não apontou novo bug concreto.

**Incidentes/cuidado técnico:** Npgsql entrega date como DateOnly, que o materializador Dapper não converte diretamente à propriedade DateTime; projeção SQL usa cast date::timestamp antes de mapear datas civis. Testes expuseram envio de resumos vazios; agora seções sem itens são removidas, e sem nenhuma seção visível não envia nem marca sucesso. Fixture do teste existente de visibilidade recebeu IClock. Testes de tenant do helper não habilitam RLS na amostra; o upgrade específico e SecurityTests verificam RLS real nas novas tabelas usando lexflow_app. Não conte tentativas sem testes ou falhas de IPC/Docker como sucesso. Execução serial -m:1 e acesso autorizado foram usados. Instalação npm do AppHost mudou só 18 flags peer, sem versões: diff conferido e normalização descartada. SMTP não garante exactly-once após queda entre aceitação e commit; limitação documentada, sem marcar sucesso antes de enviar.

**Ambiente fechado:** AppHost/API/Worker/SPA encerrados, sete containers desta instância parados; portas 4200/5238/8025 fechadas e zero containers ativos; volumes/cadastros preservados. Migrations 20261021140049_ControlReportPermission e 20261021141634_NotificationPoliciesAndDailyDigest confirmadas no histórico do banco local. Não editar essas migrations nem as anteriores aplicadas.

**Próximo:** push e PR do 7D quando o usuário solicitar; conferir CI e revisão e aguardar merge pelo usuário. Depois, arrumação pós-merge e 7E. Revisão jurídica do 7A e fornecedor real de publicações seguem pendentes; não declarar as etapas 7/6 integralmente concluídas. Sol orquestrou domínio, segurança, jobs, transações, migrations, contrato e fechamento; Luna fez fontes/testes delimitados e gates, com revisão; Sol também fez os testes mais exigentes de jobs/SMTP.

---

## 2026-10-21 — Codex (7D parcial; duas definições de negócio pendentes)

**Pedido:** “Continue com o desenvolvimento”. Branch codex/etapa-7d-controladoria-notificacoes, HEAD ainda 22f1479 (arrumação pós-merge do 7C). Alterações do 7D no working tree, sem commit, push ou PR da implementação. .codex/ local preservado e excluído.

**Decisão nova confirmada:** processos sem andamento = somente Active, último CaseMovement.OccurredAt, fallback LegalCase.CreatedAt; padrão 30 dias ajustável na consulta. Não contar edição cadastral como andamento nem usar CollectedAt.

**Feito:** ADR 0040 proposta, licença oficial Apache-2.0 do Dapper conferida antes de adicionar versão 2.1.89; TenantSql com sessão do tenant reaplicada, parâmetros e transação preservados; contratos mínimos e fonte de processos parados; GET /api/v1/schedule/control; permissão schedule.control.read e migration aditiva 20261021140049_ControlReportPermission para Administrador/Sócio/Controladoria. Casos ativos vêm de Litigation por contrato com cases.read + control.read + sigilo; a consulta de andamentos em Publications lê só suas tabelas e não exige publications.read, como o endpoint existente de andamentos. Count calculado antes de limitar itens. Platform compõe as fontes; nenhum SQL entre schemas de módulos. Fonte não fornece as demais pendências/métricas ainda.

**Perguntas em aberto (não presumir resposta):** usuário respondeu apenas a primeira das três perguntas assíncronas. Restam (1) definições de indicadores, prazos sem responsável e sem evidência; (2) política obrigatória de notificações, destinatários e resumo diário opt-in às 07:30. Propostas completas estão na conversa. Publicações não têm reabertura/retriagem hoje; retrabalho deve refletir isso, sem criar novo fluxo por conta própria. Demais indicadores, notificações/preferências/regras, jobs/resumo diário não implementados.

**Verificação parcial:** build da infraestrutura, módulos afetados e API.IntegrationTests Release, 0 avisos/erros. API ControlReportTests 3/3 (TestResults/7d-directed/control-report.log); Platform ControlReportTests 8/8 (platform-control.log); TenantSqlTests 4/4, saída do subagente conferida (não há log persistido, código revisado). Testes cobrem parâmetros, tenant ausente/sessão em conexão aberta, transação/rollback; janela civil São Paulo; timestamp real vs coleta, contagens, tenant, sigilo, escopo, status e permissões na API. A amostra TenantSql não habilita RLS, portanto esses quatro não provam RLS. Tentativas anteriores com filtro incorreto/IPC não contam; o filtro MTP aprovado é --filter-class '*ControlReportTests', Release --no-build após build serial -m:1 com acesso autorizado.

**Continuidade:** concluir após as duas respostas pendentes. Verificar upgrade/novos gates de segurança e RLS; OpenAPI e cliente ainda não regenerados; suíte completa obrigatória ainda não executada. Não declarar 7D pronto nem publicar esse estado parcial. Ambos os manuais/progress/AGENTS/CLAUDE atualizados para distinguir esse checkout da main e manter telas no 7F. Nenhum AppHost iniciado ou migration nova aplicada ao banco persistente; cadastros/volumes preservados. Revisão jurídica do 7A e fornecedor real de publicações continuam pendentes. Sol fez base/contratos/permissão/migration/revisão; Luna fez fonte e testes delimitados.

---

## 2026-10-21 — Codex (7C mergeado; preparação do 7D)

**Pedido:** usuário confirmou “Mergeado”. PR #38 confirmado como MERGED em 2026-10-21 às 10:48 (America/Sao_Paulo), merge 966a3a1ca1c5fcf3fb004e43f990620ea74b6841. Implementação 407ca4a; envio final f63f6e8. Todos os checks aprovados, incluindo backend, frontend, OpenAPI, CodeQL, Gitleaks, Trivy e AppHost/E2E.

**Feito:** main atualizada por fast-forward e branch codex/etapa-7d-controladoria-notificacoes criado da base mergeada. ADR 0039 aceita; progress, AGENTS/CLAUDE e ambos os manuais atualizados. Commit e push da arrumação documental neste branch seguem o ciclo pós-merge autorizado; nenhum PR novo aberto.

**Verificação:** somente documentação; diff, UTF-8 e igualdade dos agentes conferidos. Preservados 1.124 testes .NET, gate de 80%, 134 Vitest e duas rodadas de 17 Playwright do 7C, complementados pelo CI final integral aprovado. Sem repetir runtime. Aplicação permanece fechada e volumes/cadastros preservados; .codex/ local não rastreado preservado e excluído do commit. Migration AgendaAndTasks já aplicada no banco local, não editar.

**Próximo:** detalhar o 7D (controladoria, indicadores, preferências/regras de notificação e resumo diário às 07:30 em America/Sao_Paulo) conforme o plano aprovado. Implementação do 7D ainda não iniciada. Sol orquestra partes críticas e revisão; Luna executa fatias delimitadas e verificação. Telas no 7F; revisão jurídica do 7A e fornecedor real de publicações seguem pendentes. Merge continua sendo feito pelo usuário.

---

## 2026-10-21 — Codex (7C publicado no PR #38; merge pelo usuário)

**Pedido e confirmação:** usuário pediu “Crie o merge” e esclareceu: “Abra o PR; eu faço o merge”. Implementação 407ca4a enviada no branch codex/etapa-7c-agenda-tarefas; [PR #38](https://github.com/iagogoliveira/ProjetoLaw/pull/38) aberto e anexado ao chat. ADR 0039 permanece proposta.

**Feito:** registros de publicação atualizados no progress, AGENTS/CLAUDE, ADR e ambos os manuais. Sem mudança de runtime ou novos passos de uso/inicialização; preservada a verificação integral da entrada anterior (1.124 .NET, 134 Vitest e duas rodadas de 17 Playwright). Diff, UTF-8 e sincronização dos agentes conferidos. Aplicação permanece fechada, volumes/cadastros preservados; .codex/ local excluído do commit.

**Próximo:** conferir CI e revisão do PR #38 e aguardar merge pelo usuário. Depois, arrumação pós-merge e 7D. Revisão jurídica do 7A e fornecedor real de publicações continuam pendentes.

---

## 2026-10-21 — Codex (7C implementado, verificado e commitado localmente; sem push/PR)

**Pedido do usuário:** "Prossiga", após a arrumação pós-merge do 7B. Implementação no branch `codex/etapa-7c-agenda-tarefas`, que já tinha a arrumação `75d38c4` enviada.

**Decisões do usuário em 2026-10-20:** processo opcional para tarefas/compromissos; concluir exige checklist e subtarefas resolvidos; tarefas com quatro estados e prioridades, permitindo reabrir; subtarefas completas de um nível, com canceladas também liberando o pai. Registradas no progress e na ADR 0039, proposta.

**Feito:** domínio, persistência e API de tarefas com prioridade, checklist, comentários, subtarefas e reabertura; compromissos; agenda unificada por usuário/equipe/escritório, intervalo e cursor; leitura mínima de audiências por `IHearingCalendar` em Litigation, sem acesso cruzado a DbContext; redistribuição parcial em lote de tarefas, compromissos e prazos. Datas e conferência dos prazos preservadas, com atividade traduzida na timeline. OpenAPI e cliente regenerados; ambos os manuais atualizados. Telas no 7F; anexos no GED da Etapa 9, notificações adicionais no 7D e sincronização no 7G. Sem pacote novo.

**Divisão e revisão:** Sol fez domínio, autorização, transações, migrations, contrato e fechamento; Luna fez endpoints, testes delimitados e os gates backend. Segunda revisão Sol encontrou dois problemas corrigidos: destinatário administrativo precisa ter leitura cujo escopo alcance sua atribuição; recurso oculto e inexistente precisam do mesmo código do tipo, preservando a auditoria de CaseAccess. Testes cobrem ambos. Data inválida de query passou de 500 para 400 por seletor de status em Web.Common, sem expor detalhes; erro inesperado continua 500.

**Verificação integral aprovada em 2026-10-21:** builds Release/Debug sem warnings/erros; format; **1.124 testes .NET em 20 projetos**, todos sem falhas/skips; gate de 80% (Scheduling.Domain 98,8%, Application 99,2%); dependências sem vulneráveis e seis modelos EF sem pendências. API **262**, Scheduling.Unit **169**, Scheduling.Integration **4**, Security **12** e AppHost **14**. OpenAPI **159 caminhos/279 schemas**, oasdiff v1.33.0 sem quebra, cliente regenerado; audit zero, lint, **134 Vitest** e build Angular aprovados. **Duas rodadas completas consecutivas de 17 Playwright contra AppHost**, com acessibilidade; somente preparação opcional de carga ignorada em cada rodada. AppHost.Tests encerrou antes do manual. Logs finais em `TestResults/7c-final-validation`, tentativas anteriores em `TestResults/7c-directed`. Orquestrador conferiu diffs/evidências.

**Cuidados de continuidade:** tarefas usam `TaskState`, distinto de `WorkItemStatus` dos prazos; `LegalCaseId`/`PublicationId` da tarefa agora são opcionais. Na lista legada por processo, `status` permanece `pending` e o estado real vem em `lifecycleStatus`. Subtarefas herdam o vínculo de processo, têm só um nível e só são alteradas/reabertas quando o pai está aberto. `AgendaAccess` aplica permissão de destino, leitura do processo, sigilo e, para itens administrativos, responsável/equipe/escritório. Filtros não concedem acesso. O lock transacional de Scheduling ordena as mudanças. Testes de RLS direto precisam de `SET ROLE lexflow_app` na conexão aberta (migration login contorna RLS); SQL permitido é `ExecuteSqlAsync`/interpolado. Testes de timestamps preservados devem comparar GET persistido antes/depois (precisão PostgreSQL), sem afrouxar a comparação.

**Ambiente fechado:** AppHost/API/Worker/SPA encerrados; os sete containers registrados da instância parados, zero containers ativos e portas 4200/5238/8025 fechadas. Volumes/cadastros preservados. Migration **`20261021025032_AgendaAndTasks` aplicada no banco local**; não editar essa migration nem as anteriores. Docker Desktop foi iniciado a partir da instalação per-user `AppData/Local/Programs/DockerDesktop`; o daemon estava parado nas primeiras tentativas. E2E usou Mailpit IPv4, dois workers e limite público 10000 só na instância de verificação, sem mudar produção.

**Estado:** implementação verificada em commit local, **sem push/PR da implementação**. Remoto da branch ainda em `75d38c4`. `.codex/` local não rastreado preservado e excluído do commit. Próximo passo: push e PR do 7C quando o usuário pedir, conferir CI e aguardar merge pelo usuário. Depois, arrumação pós-merge e 7D. Revisão jurídica do 7A e fornecedor real de publicações continuam pendentes; as etapas 7 e 6 não estão integralmente concluídas.

---

## 2026-10-20 — Codex (pós-merge do 7B e preparação do 7C)

**Pedido do usuário:** "Mergeado". Confirmado no GitHub o PR #37 como MERGED em 2026-10-20 às 23:35 (America/Sao_Paulo), merge `aaec47faee69ef8eecd284dd34dbf4bf20a3b4bb`. Todos os checks finais de `43356fb` aprovados, incluindo backend, frontend, OpenAPI, CodeQL, Gitleaks, Trivy e AppHost/E2E; a correção do Vite está coberta pelo CI final.

**Feito:** main atualizada por fast-forward e branch `codex/etapa-7c-agenda-tarefas` criado dessa base. ADR 0038 aceita; progress, AGENTS/CLAUDE e ambos os manuais atualizados quanto ao merge, disponibilidade e checkout. Nenhuma alteração de runtime; mantidas as verificações locais consolidadas (1.075 .NET, gate de 80%, 134 Vitest) e o CI final aprovado. Diff, UTF-8 e sincronização documental conferidos. Aplicação não iniciada; volumes/cadastros preservados. Diretório não rastreado `.codex/` preservado e não incluído no commit.

**Estado:** arrumação pós-merge para commit/push na branch do 7C conforme o ciclo autorizado. 7C em preparação, sem implementação ou PR neste registro. Nenhuma decisão nova de negócio.

**Próximo trabalho:** 7C (agenda e tarefas) conforme o plano aprovado: compromissos, tarefas com prioridade/checklist/subtarefas/comentários, agenda unificada com audiências por contrato, filtros por usuário/equipe/escritório e redistribuição em lote; autorização, escopo/sigilo, paginação e SecurityTests. Sol orquestra partes críticas e Luna executa fatias delimitadas e verificações. Push/PR da implementação só a pedido; merge pelo usuário. Revisão jurídica do 7A e fornecedor real de publicações continuam pendentes.

---
## 2026-10-20 — Codex (7B enviado e PR #37 aberto)

**Pedido do usuário:** "Ok, prossiga", após a entrega local do 7B. Implementação `7c7b4bf` enviada na branch `codex/etapa-7b-ciclo-prazo`; [PR #37](https://github.com/iagogoliveira/ProjetoLaw/pull/37) aberto contra `main` e anexado à conversa. Merge permanece com o usuário.

**Feito:** progress, ADR 0038 e AGENTS/CLAUDE atualizados com a publicação. Ambos os manuais revisados, com ajuste da referência ao PR; nenhum passo novo de uso ou inicialização. O CI inicial apontou ampliação de enum fechado no OpenAPI e vulnerabilidade transitiva no Vite. Corrigidos com `lifecycleStatus` aditivo na lista de prazos (status legado preservado) e override de Vite 8.3.4, MIT; sem nova exceção de contrato ou redução do audit. Verificação das correções: build/format, API 240/240 (consolidado 1.075 .NET), gate 80%, oasdiff v1.33.0 sem quebra, audit zero, lint, 134 Vitest e build Angular aprovados. Detalhes no progress; logs em `TestResults/7b-ci-fixes`. Conferir AppHost/E2E no CI após o ajuste do Vite antes do merge. Aplicação permanece fechada, volumes/cadastros preservados.

**Estado e continuidade:** PR #37 aberto, ADR 0038 proposta; registro documental desta publicação segue em commit e push na mesma branch. Acompanhar CI e revisão sem presumir aprovação. Após merge pelo usuário, atualizar main, aceitar a ADR e preparar o 7C conforme o plano aprovado. Revisão jurídica do 7A e fornecedor real de publicações continuam pendentes. Nenhuma decisão nova de negócio.

---

## 2026-10-20 — Codex (7B implementado, verificado e commitado localmente; sem push/PR)

**Pedido do usuário:** "Ok, continue." Implementação do 7B no branch `codex/etapa-7b-ciclo-prazo`, após a arrumação pós-merge `a674bd1`.

**Decisão nova do usuário:** "Bloquear apenas o cumprimento; permitir iniciar o trabalho" quando a conferência for obrigatória. Registrada no progress e na ADR 0038, proposta. Nenhuma outra decisão anterior foi alterada.

**Feito:** domínio e endpoints de cadastro manual, consulta e transições do prazo, política de conferência por organização/contrato, snapshots de cálculo e local, atividades/timeline, alertas do calendário sem reescrever datas, notificações com visibilidade e consumidor idempotente, métrica, RLS e migrations aditivas `DeadlineLifecycle` e `RequireDeadlineReview`. OpenAPI e cliente regenerados. Ajustados sino/timeline existentes e traduções; telas completas de prazo seguem no 7E. Ambos os manuais atualizados.

**Divisão:** Sol fez domínio, autorização, transações, migrations e contrato; Luna fez configuração, fatias de notificação, testes, segurança e i18n/UI sob escopo definido. O orquestrador revisou os diffs e corrigiu integração/fixtures; o verificador Luna executa os gates. Logs de tentativas direcionadas preservados em `TestResults/7b-directed`; fechamento em `TestResults/7b-final-validation`.

**Verificação integral aprovada:** builds Release/Debug sem warnings/erros; format final; **1.074 testes .NET em 20 projetos**, sem falhas/skips; cobertura mínima de 80% (Scheduling.Domain 98,7%, Application 98,3%); nenhuma vulnerabilidade reportada; seis modelos EF sem pendências; OpenAPI e cliente regenerados; lint, **134 Vitest**, build Angular e **duas rodadas consecutivas de 17 Playwright contra AppHost**, com acessibilidade. Somente a preparação opcional da carga ignorada em cada rodada. AppHost.Tests 14/14 antes do AppHost manual. Verificador Luna executou os gates backend; Sol conferiu evidências e executou frontend/E2E. Resultados finais e comandos no progress; logs em `TestResults/7b-final-validation`.

**Cuidados para continuidade:** o consumer de Scheduling no Platform usa um `Handle` público que envolve `base.Handle`, pois o Wolverine descobre métodos públicos; a métrica só é contada depois da persistência e não na reentrega. Teste específico protege esse comportamento. Testes antigos de upgrade de Organization usam SQL interpolado para semear o schema histórico; o modelo EF atual não serve para inserir antes de adicionar `require_deadline_review`. Foram acrescentados testes de upgrade 7A→7B em Organization e Scheduling, sem reescrever migrations aplicadas.

**Ambiente fechado:** AppHost/API/SPA/Worker encerrados e sete containers desta instância parados, sem remover volumes/cadastros. O banco local agora tem as migrations do 7A e do 7B aplicadas; não editar essas migrations. Mailpit verificado pelo IPv4 (`127.0.0.1:8025`) no E2E, com dois workers e limite público de teste 10000 apenas na instância de verificação.

**Estado:** implementação em commit local na branch `codex/etapa-7b-ciclo-prazo`, **sem push/PR da implementação**. A branch remota contém apenas a arrumação pós-merge `a674bd1`. ADR 0038 proposta; AGENTS/CLAUDE sincronizados e ambos os manuais revisados/atualizados. Próximo passo: push e PR do 7B quando o usuário pedir; conferir CI e aguardar merge pelo usuário. Depois, arrumação pós-merge e 7C. A revisão jurídica do 7A e o fornecedor real de publicações continuam pendentes.

---

## 2026-10-20 — Codex (pós-merge do 7A e preparação do 7B)

**Pedido do usuário:** "Merge aprovado". Confirmado no GitHub o PR #36 como MERGED em 2026-10-20 às 18:11 (America/Sao_Paulo), merge `2184b6b`, com todos os checks aprovados, inclusive AppHost/E2E.

**Feito:** main atualizada por fast-forward; branch `codex/etapa-7b-ciclo-prazo` criado dessa base. ADR 0037 aceita; progress, AGENTS/CLAUDE e os dois manuais atualizados quanto ao merge e à base local. Preservadas a revisão jurídica pendente, os resultados locais do 7A e as decisões aprovadas do 7B. Esta arrumação não altera runtime nem exige repetir suítes aprovadas; diff e sincronização documental conferidos. Nenhuma aplicação iniciada.

**Estado:** arrumação documental na branch do 7B para commit e push pelo ciclo pós-merge. O 7B permanece em preparação, sem implementação ou PR neste registro.

**Mapeamento de leitura do Luna, conferido por amostra pelo orquestrador:** `Deadline` ainda tem só `Pending`, exige publicação e compartilha `WorkItemStatus` com `TaskItem`; o cadastro manual e o ciclo precisam de evolução aditiva. `RequireDeadlineReview` ainda não existe em Organization. Reutilizar `IOrganizationMembers.CountActiveAsync` para a exceção solo (padrão de propostas), `ICaseDirectory` e `WorkAssignees` para escopo/sigilo. Scheduling ainda não contribui à timeline; os padrões estão em `PublicationsTimeline`/`ITimelineContributor`. O CRUD de `CalendarEntry` ainda não emite alertas de alteração; a política de visibilidade das notificações deve restringir destinatários e acesso ao processo. Nenhum código ou teste alterado nesse mapeamento.

**Próximo trabalho:** ciclo do prazo (7B) conforme plano aprovado: cadastro com regra/sugestão, andamento/cumprimento/perda, evidência e justificativa, dupla conferência com exceção de escritório solo, notificações com escopo/sigilo, alertas por mudança do calendário sem alterar prazos confirmados, timeline e métrica. Sol como orquestrador e Luna nas tarefas delimitadas. Push/PR de implementação só a pedido; merge pelo usuário. A revisão jurídica do 7A e o fornecedor real de publicações continuam pendentes.

---

## 2026-10-20 — Codex (7A enviado e PR #36 aberto)

**Pedido do usuário:** criar o PR do 7A já implementado pelo Claude. Aprovada também a divisão no Codex: Sol como orquestrador e Luna para verificações, exploração e implementação delimitada; Sol em subtarefas mais exigentes.

**Feito:** conferidos a branch, o estado limpo, o commit de implementação `49043b2`, a base remota `af904e3` e a ausência de PR anterior para esta branch. Implementação enviada; aberto o PR [#36](https://github.com/iagogoliveira/ProjetoLaw/pull/36), de `etapa-7-prazos-agenda` para `main`, e anexado à conversa.

**Verificação:** subagente verificador Luna conferiu somente os logs existentes de `TestResults/7a-final-validation`, confirmando os 1.026 testes .NET, cobertura de 80%, demais gates e a limitação de Playwright sem AppHost. O orquestrador conferiu evidências por amostra. Nenhuma suíte repetida e nenhuma mudança de runtime. Progress, ADR 0037 e AGENTS/CLAUDE atualizados; os dois manuais revisados, com ajuste apenas da disponibilidade/checkout e sem novos comandos. Aplicação não iniciada.

**Estado:** implementação com push e PR #36 aberto; registro documental desta publicação segue em commit e push na mesma branch. CI deve ser conferido no GitHub; não presumir aprovação. ADR 0037 permanece proposta.

**Próximo agente:** acompanhar revisão/CI; o merge é do usuário. Após o merge, fazer a arrumação pós-merge e seguir o 7B, com o plano e as decisões já aprovados. Continuam pendentes a revisão jurídica dos 72 casos e do seed estadual, a conferência legal indicada no progress e o fornecedor real de publicações. Não marcar o critério jurídico da Etapa 7 como atendido.

---

## 2026-10-20 — Claude (PR 7A implementado e verificado; commit local, sem push)

**Pedido do usuário:** "Ok. gostei das sugestões. siga". O plano da Etapa 7 e as oito propostas foram aprovados como estavam, e implementei o 7A.

**Feito:**
- **Platform:**
  - `Holiday` ganhou abrangência, tipo e duração;
  - `IHolidayDirectory` novo;
  - seed com o recesso do CPC, a Justiça Federal e 31 feriados estaduais de 20 UFs.
- **Scheduling:**
  - `IDeadlineCalculator` puro, com a memória de cálculo;
  - `CalendarEntry` do escritório, com RLS;
  - `POST /api/v1/deadlines/calculate` e o CRUD `/api/v1/schedule/calendar-entries`.
- **Permissões e contratos:**
  - permissão `schedule.calendar.manage`;
  - `ICaseDirectory.LocationAsync`;
  - três migrations: `ForensicCalendar`, `OfficeCalendar` e `ScheduleCalendarPermission`.
- **Documentação e contrato:**
  - ADR 0037, proposta;
  - `docs/revisao/prazos-7a.md` para o advogado;
  - manual e guia local;
  - OpenAPI e cliente TypeScript, só com adições.
- **Divisão de trabalho, na prática:**
  - um subagente de pesquisa (Sonnet) levantou os feriados estaduais com fontes; entraram só os de confiança alta ou média, e os demais estão listados na ADR;
  - o `implementador` (Sonnet) fez o CRUD do calendário e os testes; revisei o diff;
  - o `verificador` (Haiku) rodou a verificação completa; conferi os logs por amostra.

**Verificação:**
- 1.026 testes .NET aprovados;
- gate de 80% aprovado, com Scheduling.Domain em 98,3% e Scheduling.Application em 99,2%;
- build Release com 0 warnings; format, dependências e seis modelos EF sem pendência;
- lint, 133 Vitest e build Angular aprovados;
- Playwright contra o AppHost não executado, porque o 7A não tem tela; fica para o CI.

Detalhes no `progress.md`.

**Estado:** commit local no branch `etapa-7-prazos-agenda`, **sem push e sem PR**. O Docker Desktop está ligado e nenhum AppHost ficou no ar.

**Próximo agente:**
- push e PR somente a pedido do usuário;
- depois do merge, a arrumação pós-merge e o 7B.

O critério 1 da etapa (casos conferidos por advogado) continua aberto até a revisão jurídica ser registrada no documento de revisão.

**Atenção:** os tipos `implementador` e `verificador` só são reconhecidos numa sessão nova do Claude Code. Antes disso, use o `general-purpose` com o modelo deles.

---

## 2026-10-20 — Claude (subagentes, arrumação pós-merge do 6E e proposta da Etapa 7)

**Pedidos do usuário:**
- Montar a divisão entre um orquestrador (Opus) e subagentes mais baratos; o usuário perguntou também se ela vale para o Codex.
- Depois: "Vamos testar essa divisão. Siga o desenvolvimento do projeto."

**Feito:**
- **Subagentes.** Criados `.claude/agents/verificador.md` e `.claude/agents/implementador.md`, e a seção "Divisão de trabalho entre agentes" no `CLAUDE.md` e no `AGENTS.md`, que continuam idênticos:
  - `verificador` (Haiku): roda os gates e devolve o resumo das falhas, sem editar;
  - `implementador` (Sonnet): fatias com especificação fechada.
- **Arrumação pós-merge do #35.**
  - Os registros locais do Codex (entrada abaixo) foram preservados.
  - A `main` local foi atualizada até `af904e3`.
  - O branch `etapa-7-prazos-agenda` foi criado dessa base. O branch remoto do 6E tinha sido apagado; a árvore do `34c07dc` é idêntica à do merge.
- **Proposta da Etapa 7**, em PRs de 7A a 7G, registrada no `progress.md`:
  - 7A: calendário forense e calculadora;
  - 7B: ciclo do prazo;
  - 7C: agenda e tarefas;
  - 7D: controladoria e notificações;
  - 7E: telas de prazos;
  - 7F: telas de agenda e tarefas;
  - 7G: sincronização com Google e Microsoft.
- **Primeiro teste da divisão.** O `Explore` (Haiku) mapeou o Scheduling do 6D. Conferi o relatório no código: o status é só `Pending`, há apenas as consultas por processo e as permissões `schedule.*` já estão no catálogo.

**Estado:** documentação no branch `etapa-7-prazos-agenda`, com commit e push desta arrumação (ciclo pós-merge). **Plano da Etapa 7 não aprovado e implementação não iniciada.**

**Próximo agente:** aguardar a aprovação e as respostas às oito dúvidas da seção da Etapa 7 no `progress.md`:
1. quem confere os 60 casos;
2. a abrangência dos feriados;
3. o início da contagem;
4. a dupla conferência;
5. a evidência;
6. os destinatários do prazo perdido;
7. a sincronização Google/Microsoft;
8. as bibliotecas.

Não presumir as respostas. O fornecedor real de publicações continua pendente.

**Para o Codex:** os arquivos `.claude/agents/` são do Claude Code. O Codex segue a mesma divisão de papéis com os recursos que tiver, ou faz em sequência.

---

## 2026-10-19 — Codex → Claude (PR #35 mergeado; somente registro local a pedido)

**Instrução mais recente do usuário:** “PR mergeado. Somente anote o estado atual, irei ir para outro agente de IA (Claude).” Esta instrução limita esta sessão ao registro documental. Não continuar automaticamente o ciclo pós-merge nem iniciar a próxima etapa.

**Estado confirmado:** PR [#35](https://github.com/iagogoliveira/ProjetoLaw/pull/35) MERGED pelo usuário em 2026-10-19, às 22h36 de Brasília; merge `af904e37953d32ac20f9d38a37e30e18822e7046`. Main remota aponta para esse commit. Todos os checks aprovados: backend, frontend, OpenAPI, CodeQL (C#/JS/TS), Gitleaks, Trivy e AppHost/E2E. Implementação `23c853d`, enviada com documentos até `34c07dc`. O checkout local continua **`codex/etapa-6e-telas-publicacoes`, HEAD `34c07dc`**, com os registros desta passagem de bastão **modificados localmente, sem commit/push**. Main local não foi atualizada; nenhum branch novo foi criado. Preservar essas alterações ao retomar; não descartá-las para trocar de branch.

**Entregue:** fontes/DataJud e captura manual auditada no Worker; fila dividida/filtros/URL/atalhos; vínculo/distribuição/triagem com confirmação e permissões do destino; prazo/tarefa/audiência mínimos; fechamento com histórico imutável; notificações/SignalR/contador; andamentos e linha do tempo. Corrigido vínculo de captura ao período com `SourcePeriodId`, snapshot único e migration aditiva `CaptureRunSourcePeriod`; não reescrever datas ou assinaturas históricas. ADR 0036 agora aceita no registro local.

**Verificação preservada:** 906 testes .NET em 20 projetos (API 210, Publications.Integration 18, Architecture 17, Security 11, AppHost 14); cobertura mínima de 80% aprovada, Publications.Application 99,2%/Domain 98,0%. Release/Debug sem warnings; format verify; dependências sem vulnerabilidades; seis modelos EF sem mudanças pendentes; OpenAPI aditivo e cliente regenerado; lint/Prettier, 133 Vitest e build Angular. Duas rodadas completas consecutivas de 16 Playwright, com WCAG 2.1 AA e relatório; apenas a preparação opcional de carga ignorada (sem `LOAD_OUTPUT`). Evidências locais em `TestResults/6e-*`; CI do PR #35 também todo aprovado. Esta sessão só altera documentação: conferir UTF-8/links/diff e agentes idênticos, sem repetir suítes de runtime.

**Documentação/ambiente:** progress e AGENTS/CLAUDE registram o merge e esta parada; manual e guia local atualizados para as telas mergeadas, sem novos comandos de inicialização. Último estado operacional: aplicação e sete serviços encerrados com volumes/cadastros preservados; nenhum serviço iniciado nesta sessão. Reabrir para exploração somente a pedido. Manter os dois guias após cada melhoria.

**Decisões que o Claude deve preservar:** plano 6A–6E e captura manual aprovados; fontes ativas e gestão de integrações em escopo de escritório para captura manual, com auditoria/outbox/inbox/deduplicação, sem mudar rotina 05h–07h. Primeira consulta DataJud importa todo o histórico público; seguintes acrescentam novidades sem duplicar. Descarte sem processo exige justificativa/autor; demais triagens exigem processo; criar destino exige sua permissão; novidades após fechamento exigem nova revisão preservando o histórico. Testes afetados durante implementação, verificação completa no fechamento. Todas as perguntas do 6E respondidas.

**Pendências/próximo agente:** 6A–6E estão mergeados, mas o fornecedor real de publicações/intimações segue pendente e **a Etapa 6 não está integralmente concluída**; DataJud não substitui captura de intimações. Históricos antigos de períodos encerrados sem associação inequívoca de captura exigem avaliação técnica. Quando o usuário mandar retomar: ler handoff/progress, os dois arquivos `docs/spec/` e ADRs aplicáveis; preservar estes registros, concluir a arrumação pós-merge e então planejar a continuidade. Calculadora/agenda completas ficam na Etapa 7, com plano e dúvidas antes de implementar. Nenhum plano novo aprovado nesta parada. Commit/push destes registros e mudança de branch ficam para a retomada pelo Claude; não foram feitos agora.

---

## 2026-10-19 — Codex (PR 6E #35 aberto a pedido do usuário)

**Estado:** branch `codex/etapa-6e-telas-publicacoes`; implementação `23c853d` e atualização documental desta entrada enviados juntos, em um único push. PR [#35](https://github.com/iagogoliveira/ProjetoLaw/pull/35) aberto contra `main`, CI/revisão pendentes, sem merge. O PR foi preparado em rascunho para registrar seu número antes do push único e disponibilizado para revisão após confirmar o envio. ADR 0036 continua proposta.

**Pedido do usuário:** “Pode seguir o PR” autoriza enviar a implementação e abrir o PR. Não autoriza merge. Mantidas as decisões de negócio já aprovadas, inclusive a captura manual; nenhuma nova dúvida.

**Feito e verificado:** descrição do PR em português com escopo, migration, testes e pendências; progress, ADR e AGENTS/CLAUDE atualizados e sincronizados. Manual e guia local revisados: esta publicação não altera uso ou inicialização, e ambos já descrevem as telas do branch sem merge; não acrescentar passos artificiais. UTF-8, links e diff conferidos antes do commit documental. Sem mudança de runtime: mantidos 906 testes .NET, cobertura mínima de 80%, 133 Vitest, lint/builds/format/dependências/seis modelos aprovados e duas rodadas consecutivas de 16 Playwright com WCAG (1 preparação opcional de carga ignorada por rodada). Não repetir suítes completas sem mudança, falha ou dúvida concreta. Aplicação permanece fechada, volumes/cadastros preservados.

**Próximo:** aguardar CI/revisão do PR #35 e tratar falhas concretas. Usuário faz merge. Após aviso: confirmar merge/checks, atualizar main, aceitar ADR 0036 com número/data e preparar a continuidade conforme spec/progress. Fornecedor real de publicações/intimações segue pendente; não declarar a Etapa 6 integralmente concluída. Calculadora e agenda completas ficam na Etapa 7; planejar e esclarecer dúvidas antes de implementar.

---

## 2026-10-19 — Codex (6E verificado; commit local, aguardando pedido de push/PR)

**Estado:** branch `codex/etapa-6e-telas-publicacoes`, base remota `c5c13c0`. Esta entrada integra o commit local de implementação (consultar `git log -1`), com código, ADR 0036 proposta, OpenAPI/cliente e documentos juntos. Sem push/PR/merge. Fornecedor real pendente; Etapa 6 continua em andamento.

**Feito:** fontes/DataJud, fila dividida/filtros/URL/teclado, não vinculadas, vínculo/distribuição/triagem e criação confirmada de prazo/tarefa/audiência, fechamento/histórico, notificações/SignalR/contador exato e andamentos na linha do tempo. Duas consultas de apoio e tentativa manual adicionadas com permissões, testes e contrato aditivo. Cliente SignalR 10.0.11 MIT e transitivas permissivas registrados na ADR; nenhum pacote .NET novo.

**Decisão nova aprovada:** “Tentar captura agora” para fonte ativa, `settings.integrations.manage` em escopo de escritório, auditoria/deduplicação e Worker; rotina 05h–07h preservada. Dia entre última ativação e hoje, resposta 202 de solicitação; auditoria e evento no outbox, consumidor com inbox. Não pedir aprovação novamente.

**Correção descoberta no e2e:** relógio real fazia fonte/período diferirem em microssegundos e impedia fechar. Reutilizado o instante e introduzido `CaptureRun.SourcePeriodId` com FK/índice; snapshot único, monitor/fechamento por ID, inclusive reativações simultâneas. Migration aditiva `CaptureRunSourcePeriod` só associa históricos inequívocos; não muda datas/períodos/assinaturas. Casos antigos ambíguos de períodos encerrados exigem avaliação técnica. Nenhuma migration aplicada foi alterada. Helpers e2e aguardam opções habilitadas/abertura pelo teclado; teste de processos aguarda detalhe carregado antes de ler a pasta. Não relaxadas asserções.

**Verificado:** Release/Debug 0 warnings; format verify; 906 testes .NET em 20 projetos, incluindo API 210, Publications.Integration 18, Architecture 17, Security 11 e AppHost 14; gate 80% aprovado (Publications.Application 99,2%/Domain 98,0%). Consolidação sem duplicar cobertura e repetições somente afetadas após ajustes. Sem dependências vulneráveis; seis modelos EF sem pendências; OpenAPI oficial e cliente regenerado; lint/Prettier, 133 Vitest/18 arquivos e build Angular. Duas rodadas completas consecutivas de Playwright/AppHost com relatório: 16 aprovados + 1 preparação opcional de carga ignorada em cada, com WCAG 2.1 AA. Evidências em `TestResults/6e-*`, relatórios `6e-playwright-final-{1,2}-report`; tentativas anteriores preservadas. UTF-8, links, chaves pt/en e agentes idênticos conferidos. Manual e guia atualizados para uso entregue no branch, ainda sem merge; imagens ficam para revisão posterior.

**Ambiente:** AppHost e os sete serviços desta instância encerrados, com volumes/cadastros preservados. Só reabrir para exploração a pedido. Não executar testes completos de novo sem alteração ou preocupação concreta.

**Próximo:** aguardar pedido de push/PR e enviar código/documentos no mesmo push. Usuário faz merge. Após aviso: conferir merge/CI, atualizar main, aceitar ADR 0036 e preparar próxima etapa, considerando fornecedor real ainda pendente e calculadora/agenda da Etapa 7. Nenhuma dúvida de negócio pendente para o 6E.

---

## 2026-10-19 — Codex (6E implementado; verificação em andamento)

**Estado:** branch `codex/etapa-6e-telas-publicacoes`, base remota `c5c13c0`. Código do 6E no diretório de trabalho, sem commit/push/PR. ADR 0036 proposta; fontes/DataJud, fila dividida/filtros/teclado, triagem/distribuição/vínculo, fechamento/histórico, notificações/contador/SignalR e andamentos implementados. Duas consultas de apoio e tentativa manual acrescentadas; OpenAPI atualizado pelo teste oficial, cliente regenerado. Nenhuma migration ou pacote .NET novo; cliente SignalR 10.0.11 MIT e transitivas permissivas registrados na ADR.

**Nova decisão do usuário:** aprovou “Tentar captura agora”, restrita a fonte ativa e `settings.integrations.manage` no escritório, com auditoria/deduplicação e Worker, sem mudar a rotina automática. Pedido e auditoria gravados com evento no outbox; consumidor com inbox agenda o job forçado. Dia segue a política existente (última ativação até hoje); consulta de execuções recebe dia opcional. Todas as perguntas de negócio respondidas.

**Verificação parcial:** Release/Debug sem warnings, 39 testes direcionados do fluxo e OpenAPI aprovados; lint, 127 Vitest, build e npm audit aprovados. Rodada .NET completa com cobertura em `TestResults/6e-final-validation`; AppHost interrompido para concluir o cliente após a nova operação. Repetições afetadas em `TestResults/6e-affected-validation` (API atual 208 aprovados), seguidas de AppHost.Tests com frontend atualizado. Não abrir AppHost manual até esse projeto terminar. Resultados não afetados devem ser preservados; consolidar cobertura sem duplicar relatórios antigos dos mesmos projetos. Frontend com testes adicionais de concorrência e dois e2e novos ainda em verificação; logs `TestResults/6e-*`.

**O que falta:** concluir gates e duas rodadas Playwright/AppHost com WCAG, corrigir falhas sem relaxar asserções, revisar manual/guia e atualizar resultados/pendências no progress, AGENTS/CLAUDE e este handoff. Depois encerrar aplicação/serviços próprios preservando volumes, conferir diff/UTF-8/contrato aditivo e commit local. Não enviar código nem abrir PR até pedido. Fornecedor real segue pendente; Etapa 6 em andamento. Os dois guias já foram incrementados para o branch, identificando verificação e ausência de merge.

---

## 2026-10-19 — Codex (6D #34 mergeado; arrumação e branch do 6E)

**Estado confirmado:** usuário informou o merge do PR [#34](https://github.com/iagogoliveira/ProjetoLaw/pull/34); GitHub confirmou MERGED em 2026-10-19, merge `75aaa9c`, com todos os checks aprovados (backend, frontend, OpenAPI, CodeQL, Gitleaks, Trivy e AppHost/E2E). Implementação `77acd5e` + `18cbec4`, enviada com documentos até `07287bd`. Main atualizada por fast-forward; branch `codex/etapa-6e-telas-publicacoes` criado da nova main. ADR 0035 aceita. Esta entrada integra o commit/push documental pós-merge autorizado pelo ciclo; consultar `git log -1` para o commit. Nenhum PR do 6E aberto.

**Feito e verificado:** progress marca o 6D concluído e o 6E em preparação; AGENTS/CLAUDE sincronizados; manual e guia local atualizados para o backend do 6D disponível na main, sem prometer telas ainda não implementadas. Somente documentação alterada nesta arrumação; diff, UTF-8, links e igualdade dos agentes conferidos antes do commit. Mantidos os gates completos do 6D: 895 testes .NET, cobertura mínima de 80%, builds sem warnings, formatação, dependências, seis modelos EF, lint, 96 Vitest e duas rodadas de 14 Playwright com acessibilidade. Não repetir suítes sem alteração de runtime ou preocupação concreta. Ambiente permanece fechado, com volumes/cadastros preservados.

**Próximo trabalho:** 6E do plano já aprovado: telas de fontes/integrações, fila dividida com atalhos e não vinculadas, vínculo/distribuição/triagem, fechamento/histórico, contador/SignalR e andamentos na linha do tempo. Implementação ainda não iniciada; estudar os contratos e padrões existentes, criar ADR 0036 proposta, tabela de subtarefas/testes e atualizar os dois guias conforme evoluir. Frontend standalone/OnPush/signals, HTTP em `data/`, Transloco pt-BR/en, permissões/escopo/sigilo, Vitest por store e Playwright/WCAG 2.1 AA contra AppHost. Não reabrir as três perguntas já respondidas do 6D. Perguntar apenas dúvidas novas de negócio. Calculadora/agenda completas ficam na Etapa 7; fornecedor real depende de escolha/documentação/acesso. A Etapa 6 continua em andamento. Código novo termina em commit local; push/PR a pedido; merge sempre pelo usuário.

---

## 2026-10-19 — Codex (publicação do PR 6D solicitada; revisão e merge pendentes)

**Autorização e estado:** usuário pediu “Crie o PR”. Branch `codex/etapa-6d-triagem-fechamento`; implementação `77acd5e` + `18cbec4`. Esta entrada integra o commit documental preparado para enviar código e documentos juntos, em um único push, e abrir o PR contra `main`. Consultar URL, checks e estado remoto com `gh pr view codex/etapa-6d-triagem-fechamento`. Nenhum merge autorizado ao agente; ADR 0035 permanece proposta até a confirmação do merge pelo usuário.

**Verificação preservada:** 895 testes .NET, cobertura mínima de 80%, builds sem warnings, formatação, dependências, seis modelos EF, lint, 96 Vitest e duas rodadas de 14 Playwright com acessibilidade aprovados, conforme o registro abaixo/progress. Único skip de navegador: preparação opcional de carga sem `LOAD_OUTPUT`. Desde `18cbec4`, apenas documentação de publicação foi alterada; revisar diff/UTF-8/links e igualdade AGENTS/CLAUDE antes do commit, sem repetir suítes de runtime sem mudança. Manual e guia local revisados: nenhum novo passo de uso ou inicialização. Ambiente segue fechado, com volumes preservados.

**Próximos passos:** conferir publicação e checks do PR, aguardar revisão/merge pelo usuário. Depois do merge, seguir o ciclo de arrumação e preparar o 6E do plano aprovado; não antecipar sua implementação neste PR. Nenhuma dúvida complementar de negócio pendente. Fornecedor real ainda depende de escolha/documentação/acesso; calculadora e agenda completas ficam na Etapa 7. A Etapa 6 permanece em andamento.

---

## 2026-10-19 — Codex (6D implementado e verificado; commit local, aguardando pedido de push/PR)

**Estado:** branch `codex/etapa-6d-triagem-fechamento`, base remota `88f1736`, checkpoint local `77acd5e` e commit local de fechamento com código/documentos (consultar `git log -1`). Nenhum push ou PR desta implementação. ADR 0035 continua **Proposta (para revisão no PR 6D)**; não marcar como aceita antes do merge pelo usuário.

**Entregue:** vínculo, distribuição e triagem auditada; encerramento irrelevante/engano sem processo, com justificativa e autor; núcleo mínimo de Scheduling; criação de prazo/tarefa/audiência junto da triagem em uma transação PostgreSQL; fechamento diário e histórico paginado. Permissões, acesso a processos sigilosos, RLS e auditoria mantidos. Novas publicações exigem novo fechamento sem alterar os anteriores; cada destino exige também sua permissão (`schedule.deadlines.manage`, `schedule.tasks.manage`, `cases.edit` para audiência). Todas as três perguntas complementares foram respondidas pelo usuário; não perguntar novamente.

**Verificação completa aprovada:** builds Release/Debug sem warnings, formatação, **895 testes .NET em 20 projetos**, sem falhas/skips, e gate de 80% em todas as camadas. Consolidado: 493 unitários, 360 de integração, 17 de arquitetura, 11 SecurityTests e 14 AppHost.Tests. Ajustes finais de descarte após commit físico e auditoria de negativas repetidos: 47 building blocks, 200 da API e 11 de segurança aprovados. Dependências .NET/JavaScript sem vulnerabilidades reportadas; seis modelos EF sem pendência; lint, **96 Vitest** e build Angular aprovados. OpenAPI oficial e cliente regenerados, somente adições (7 rotas, 18 schemas). Nenhum pacote novo. Logs/cobertura em `TestResults/6d-final-validation`, `6d-post-commit-validation` e `6d-final-*-after-review.log`.

**Navegador e ambiente:** duas rodadas consecutivas Playwright contra o AppHost, com um worker, **14 aprovados por rodada** (1,7 e 1,4 min), acessibilidade ativa e relatórios `TestResults/6d-playwright-report-1` e `-2`. Único skip: preparação opcional de carga sem `LOAD_OUTPUT`, conforme o teste existente. AppHost.Tests executado antes da instância manual. AppHost, SPA/API e os sete serviços Docker encerrados; portas 4200/5238/8025 sem listeners. Volumes `lexflow-storage-data` e `lexflow.apphost-10d2fc16f8-postgres-data` preservados. Deixar fechado, reabrir para exploração somente a pedido.

**Documentação e próximos passos:** progress com tabela/resultados/pendências, ADR 0035 e os dois guias atualizados; AGENTS/CLAUDE idênticos, UTF-8 e diff revisados antes do commit. Manual/guia descrevem operação técnica do backend no branch do 6D, sem anunciar formulários do 6E como disponíveis. Aguardar pedido de push/PR e enviar código e documentos juntos. Após confirmação do merge pelo usuário, atualizar main, aceitar ADR e preparar o 6E conforme o ciclo. Telas permanecem no 6E; calculadora/agenda completas, na Etapa 7; fornecedor real de publicações depende de escolha/documentação/acesso. A Etapa 6 continua em andamento.

---

## 2026-10-19 — Codex (6D: decisões aprovadas, implementação e verificação integral em andamento)

**Respostas do usuário:** “Preserve o historico; Exige permissão de destino.” Aprovados novo fechamento quando houver novidades, preservando todos os registros anteriores, e permissão do destino além de `publications.triage` (`schedule.deadlines.manage`, `schedule.tasks.manage`, `cases.edit` para audiência). A pergunta de encerramento irrelevante sem processo já tinha sido aprovada. Nenhuma dessas três perguntas continua pendente; não perguntar novamente.

**Estado atual:** branch `codex/etapa-6d-triagem-fechamento`; checkpoint transacional local `77acd5e`, base remota `88f1736`. Código completo do 6D no diretório de trabalho, ainda sem commit final, push ou PR novo. ADR 0035 proposta atualizada antes dos fluxos; cinco projetos de Scheduling e dois de testes adicionados; migrations novas de Publications, Scheduling e permissão de Organization. Endpoints de vínculo/distribuição/triagem, leitura de prazos/tarefas e fechamento/histórico implementados, com RLS e casos entre tenants nos SecurityTests. Manual e guia local incrementados, sem anunciar telas do 6E como entregues.

**Verificações já aprovadas:** 20 projetos .NET da rodada integral, 893 testes com cobertura, incluindo 199 da API, 11 SecurityTests e 14 AppHost.Tests; gate de 80% aprovado em todos os módulos. Lint, 96 Vitest, build Angular, dependências .NET/JavaScript sem vulnerabilidades reportadas e seis modelos EF sem pendência. OpenAPI oficial gerado, cliente TypeScript regenerado, comparação com main somente aditiva (7 rotas, 18 schemas). Builds Release/Debug sem warnings e formatação aprovados antes dos ajustes finais abaixo. Logs em `TestResults/6d-*`, rodada integral em `TestResults/6d-final-validation`.

**Ajustes finais e verificações ainda necessárias:** revisão detectou erro mascarado quando interceptor falha após o commit físico; teste novo reproduziu, descarte corrigido e integração dos building blocks repetida com **47 aprovados**, com cobertura em `TestResults/6d-post-commit-validation`. Falha antes do commit continua desfazendo ambos os lados; após commit físico, preservar o erro original e consultar o estado. Reuso do autorizador de leitura também foi adicionado aos fluxos de escrita para preservar a auditoria de negativas de caso sigiloso; teste novo correspondente. Recompilado em Release/Debug sem warnings. Repetir a API (agora 200 testes), SecurityTests e formatação afetados; substituir a cobertura antiga da API antes do gate final para não misturar versões desse arquivo. Demais resultados aprovados continuam válidos. Conferir os números finais pela evidência, não apenas por este registro parcial.

**O que falta para fechar:** concluir repetições afetadas e cobertura final; Playwright contra AppHost, preferencialmente duas rodadas com um worker e relatórios separados; encerrar AppHost e serviços próprios preservando volumes; revisar diff/UTF-8, igualdade AGENTS/CLAUDE e os dois guias; atualizar estado/última verificação no progress e este handoff com resultados finais; commit local com código e documentos. Não fazer push/PR até pedido. AppHost.Tests já terminou; nenhuma instância manual está aberta neste momento. Fornecedor real permanece pendente; Etapa 6 continua em andamento; telas no 6E, calculadora/agenda na Etapa 7.

---

## 2026-10-19 — Codex (6D iniciado: base transacional verificada; duas respostas pendentes)

**Pedido:** continuar o 6D. Usuário respondeu **“Adotar a sugestão” somente à pergunta sobre encerrar publicação irrelevante/capturada por engano sem processo**, com justificativa obrigatória e identificação do autor. Prazo/tarefa/audiência exigem processo. Essa aprovação foi persistida no progress e na ADR 0035; não repetir a pergunta.

**Ainda sem resposta:** (1) novas publicações recebidas depois do fechamento no mesmo dia exigirem novo fechamento, preservando o anterior no histórico, ou ficarem para o próximo; (2) criação do destino exigir sua própria permissão (`schedule.deadlines.manage`, `schedule.tasks.manage`, `cases.edit` para audiência), além de `publications.triage`, ou só a de triagem. As sugestões enviadas foram novo fechamento e permissão adicional. A resposta recebida não aprova automaticamente as outras perguntas. Não implementar essas duas escolhas antes das respostas. O plano geral 6A–6E continua aprovado.

**Feito antes do código:** estudo do pipeline, tenant/auditoria/outbox, contratos e modelos; criada [ADR 0035](adr/0035-triagem-transacao-local-e-fechamento.md), status Proposta (para revisão no PR 6D), detalhando a exceção aprovada à ADR 0007. A base não depende das duas decisões de negócio pendentes.

**Implementado:** `ILocalTransactionCommand` declara os módulos participantes, começando pelo dono do comando; `ILocalTransactionCoordinator` scoped em Infrastructure.Common resolve as unidades de trabalho keyed, valida banco/tenant/estado, compartilha conexão e transação PostgreSQL e confirma uma vez. Comandos normais mantêm a unidade de trabalho por módulo; comandos internos reutilizam a transação. Falha, exceção/cancelamento ou descarte sem commit desfazem dados, auditoria e outbox. Contextos secundários recuperam conexões próprias do mesmo data source; trackers são limpos no rollback. Não restaurar o objeto de conexão anterior: o EF o descarta ao substituir conexão possuída. O coordenador rejeita tenant ausente/divergente, transação independente/aninhada e mudanças pendentes. Nenhum pacote novo.

**Verificação parcial:** build Release de toda a solução sem warnings/erros; formatação integral aprovada. 238 testes unitários de building blocks + 46 de integração com PostgreSQL/RabbitMQ reais (ambos com cobertura) + 17 de arquitetura: **301 aprovados, zero falhas/skips**. Inclui 27 novos: 6 de pipeline, 19 de coordenação e 2 de outbox. Publicação dos dois contextos só é consumida após commit; rollback remove os dois lados e auditoria. Inicialmente o teste novo consultou a tabela de auditoria com nome errado; corrigido para `audit.audit_logs` antes da regressão aprovada. Logs/cobertura em `TestResults/6d-base-*`. Não é a verificação final do 6D; gates completos e testes dos fluxos reais entre módulos continuam obrigatórios no fechamento.

**Branch/estado:** `codex/etapa-6d-triagem-fechamento`; base/arrumação enviada `88f1736`. Checkpoint desta base e documentação em commit local (consultar `git log -1`), **sem push nem PR novo**. Nenhuma migration, endpoint, tela ou módulo Scheduling implementado ainda. Manual e guia local revisados: passos de uso/inicialização não foram afetados, sem novos recursos anunciados como disponíveis. AGENTS/CLAUDE sincronizados; aplicação de exploração não iniciada; serviços temporários dos testes encerrados sem mexer nos volumes da aplicação.

**Próximos passos:** incorporar as duas respostas pendentes e continuar os fluxos do 6D: distribuição/vínculo/triagem auditada, Contracts, núcleo mínimo de prazo/tarefa/audiência atômico, permissão e fechamento, migrations/RLS/SecurityTests e contrato aditivo. Testar a atomicidade nos módulos reais, concorrência/reexecução e alcance/sigilo. Atualizar ADR, manuais e gates completos antes do commit final. Calculadora/agenda completas na Etapa 7, telas no 6E e fornecedor real pendente. Código/documentos no mesmo push quando autorizado; nunca fazer merge pelo usuário. Manter ambiente fechado após verificações.

---

## 2026-10-19 — Codex (PR #33 mergeado; arrumação e preparação do 6D)

**Aviso do usuário:** “Merge efetuado.” Confirmado no GitHub: PR [#33](https://github.com/iagogoliveira/ProjetoLaw/pull/33) `MERGED`, em 2026-10-19, merge `fe1f654f97686b6530152faa06e44e2ae7261146`. Todos os checks do CI aprovados, incluindo backend, frontend, OpenAPI, AppHost/E2E, CodeQL, Gitleaks e Trivy. Merge feito pelo usuário.

**Branch/estado:** main atualizada por fast-forward; criado `codex/etapa-6d-triagem-fechamento` da base `fe1f654`. Diretório limpo antes da arrumação; os commits do 6C e do manual já estão na main. Esta arrumação documental recebe commit/push nesse novo branch conforme o ciclo pós-merge autorizado; nenhum PR do 6D solicitado/criado, nenhuma implementação nova nesta entrega.

**Feito:** 6C/DataJud marcado concluído no progress com #33/data; ADR 0034 aceita; AGENTS/CLAUDE sincronizados. Manual e guia revisados: disponibilidade do DataJud na main e base do 6D atualizadas, sem novos passos de tela ou comandos de inicialização. Preferência de manter ambiente fechado preservada; nenhum AppHost/contêiner iniciado para esta arrumação.

**Verificação:** merge/base/CI, diff somente documental, UTF-8, links locais e igualdade AGENTS/CLAUDE conferidos antes do commit. Mantidos os resultados completos do 6C: 794 testes .NET, cobertura acima de 80%, 96 Vitest e duas rodadas de 14 Playwright. Sem repetir testes de runtime para atualização documental. A Etapa 6 continua em andamento e o fornecedor real permanece pendente.

**Próximo trabalho — 6D já aprovado:** detalhar a ADR 0035 antes da implementação para transação local compartilhada entre Publications e Scheduling/Litigation, mantendo comunicação por Contracts. Implementar distribuição/vínculo/triagem com permissões e alcance do conteúdo, confirmação humana, núcleo mínimo de prazo/tarefa/audiência e fechamento diário auditado com `publications.close`. Data de recebimento em America/Sao_Paulo, incluindo pendências anteriores e bloqueando conclusão quando faltar captura esperada, conforme decisões registradas. Testar rollback conjunto, concorrência/reexecução, responsável/permissão/escopo, RLS/SecurityTests e contrato aditivo. Calculadora completa e agenda da Etapa 7, IA da Etapa 11 e telas do 6E não entram automaticamente no 6D.

**Regras para continuidade:** não pedir novamente aprovação do plano 6A–6E ou das decisões já respondidas; dúvida de negócio nova exige pergunta antes de decidir. Testes direcionados na implementação e gates completos no fechamento; revisar ambos os guias após melhorias. Código novo termina em commit local, push/PR somente a pedido; nunca fazer merge pelo usuário. Ambiente de exploração permanece fechado até novo pedido.

---

## 2026-10-19 — Codex (envio/PR do 6C autorizados; ambiente local encerrado)

**Pedido explícito do usuário:** “Não é preciso manter a aplicação rodando [...] quando eu pedir novamente você abre. pode abrir o PR”. Autoriza push do código/documentação e criação do PR 6C para main; não autoriza merge. A preferência substitui as entradas anteriores que pediam manter a instância manual aberta.

**Ambiente:** AppHost da sessão `47794` encerrado. Os sete contêineres desta instância foram identificados pelos labels do Aspire e parados explicitamente; frontend, API e Mailpit sem portas de escuta. Nenhum volume removido; cadastros preservados. Manter o ambiente fechado após verificações e reabrir para exploração manual somente a pedido. Regra registrada no AGENTS/CLAUDE e no guia local; manual de uso revisado, sem alteração dos fluxos de tela.

**Branch/estado de publicação:** `codex/etapa-6c-datajud-integracoes`; implementação `dd3bc20`, política de testes `05076bb` e arrumação pós-merge `928f0d3`. Main remota conferida em `a137919`; nenhum PR anterior encontrado para este branch. Este registro documental acompanha o código no mesmo push. Após o envio, criar o PR e confirmar seu número/endereço no GitHub pelo head do branch; anexá-lo à conversa. Não fazer outro push apenas para registrar o número e cancelar o CI recém-iniciado.

**Verificação:** nenhuma mudança de runtime nesta publicação; mantidos os 794 testes .NET, cobertura acima de 80%, 96 Vitest, builds/formatação/dependências/modelos e duas rodadas de 14 Playwright registrados abaixo. Conferir diff documental, UTF-8 e igualdade AGENTS/CLAUDE antes do commit. Não repetir a suíte sem nova mudança/falha/preocupação concreta.

**Próximos passos:** concluir push/PR autorizado e acompanhar CI/revisão; merge somente pelo usuário. ADR 0034 permanece proposta. Após aviso de merge, fazer a arrumação e seguir o 6D já aprovado. Fornecedor real continua pendente; a Etapa 6 não está concluída.

---

## 2026-10-19 — Codex (6C/DataJud implementado e verificado; commit local)

**Branch/estado:** `codex/etapa-6c-datajud-integracoes`; base `a137919`, arrumação pós-merge enviada em `928f0d3`, política de testes preservada no commit local `05076bb`. Código e documentos do 6C consolidados em commit local desta entrega, sem push/PR novo solicitado. Consultar `git log -1` para o hash. ADR 0034 proposta; não marcar a Etapa 6 concluída.

**Decisões novas do usuário:** “Adotar a sugestão” para importar todo o histórico DataJud na primeira consulta e acrescentar somente novos atos nas seguintes, preservando datas e sem duplicar; “Pode reiniciar quando necessário” para o AppHost local. Reiniciar preservando volumes/cadastros; não rodar AppHost.Tests junto com a instância manual. Nenhuma dessas perguntas continua pendente.

**Feito:** adaptador HTTP DataJud com aliases oficiais, consulta CNJ, `search_after`, desempate validado no endpoint oficial com `match_none`/zero registros, timeout, limite de concorrência e resposta. Configuração por escritório com `settings.integrations.manage`, chave protegida por tenant no Data Protection existente e exclusão de auditoria/resposta. Checkpoints por processo/tribunal, páginas transacionais, hash/índice único e lock por processo, retomada/varredura após cursor inválido e jobs distribuídos em seis horas. Contratos de Litigation para casos judiciais ativos. Migration aditiva com RLS, três endpoints e OpenAPI/cliente regenerados. Manual e guia local revisados e incrementados.

**Verificação completa aprovada:** Release/Debug sem warnings/erros; format aprovado após o último ajuste; **794 testes .NET em 18 projetos**, com cobertura e gate de 80% em todos os módulos (Publications Application 99,1%, Domain 96,3%). Inclui 169 testes da API, 16 de integração de Publications, 11 SecurityTests e 14 AppHost.Tests. Dependências .NET/JavaScript sem vulnerabilidades reportadas; cinco modelos EF sem alterações pendentes; lint, 96 Vitest e build Angular aprovados. OpenAPI gerado pelo teste oficial, somente adições de rotas/schemas, cliente regenerado. Duas rodadas consecutivas de Playwright com `--workers=1`, 14 aprovados cada (1,8/1,4 min), acessibilidade preservada; apenas a preparação opcional de carga ficou fora conforme `LOAD_OUTPUT` ausente. Nenhuma asserção relaxada.

**Logs/ocorrências:** `TestResults/6c-final-validation`, `6c-frontend-*.log`, `6c-playwright-report-1` e `-2`. O erro inicial de escopo no teste do dispatcher foi corrigido. O ajuste do hash para ignorar correções dos rótulos foi compilado antes da integração de Publications; builds/formatação finais repetidos, sem repetir checks não afetados. Perfil HTTP do Aspire exigiu `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, como no CI; tentativa inicial preservada em `TestResults/6c-apphost-http-initial.log`. Sem alteração de produção. Manual e guia incrementados, diff/UTF-8 e igualdade dos dois agentes conferidos antes do commit.

**Ambiente mantido:** AppHost em execução para exploração, sessão **`47794`**, log `TestResults/6c-apphost.log`; SPA `:4200`, API `:5238/health/ready` e Mailpit `:8025` responderam HTTP 200. Volumes/cadastros preservados. Perfil HTTP e `LexFlow__PublicPermitsPerMinute=10000` usados nesta verificação local; o limite de produção não mudou. Não encerrar como limpeza nem rodar AppHost.Tests em paralelo. Depois de alterar backend, recompilar Debug antes do próximo `--no-build`.

**Próximos passos:** aguardar pedido de push/PR do 6C e enviar código/documentos no mesmo push; acompanhar CI/revisão, sem fazer merge. Depois do merge pelo usuário, atualizar main, aceitar ADR 0034 com número/data, criar o branch do 6D e fazer a arrumação documental conforme o ciclo. O plano 6A a 6E já está aprovado; não pedir aprovação de novo para escolhas já registradas. Triagem/Scheduling/fechamento são do 6D, telas do 6E. **Fornecedor real de publicações continua pendente de escolha/documentação/acesso**; DataJud não substitui intimações. Não iniciar o próximo PR de implementação antes do merge do 6C.

---

## 2026-10-19 — Codex (testes direcionados durante implementação; gates completos no fechamento)

**Decisão explícita do usuário:** aprovou executar os testes relacionados à mudança durante a implementação e manter a verificação completa no fechamento do PR; pediu registrar no handover e nos dois arquivos de agentes. Alterações feitas no AGENTS.md, CLAUDE.md e neste `docs/handoff.md`, com registro também no progress.

**Regra para Claude e Codex:** durante o desenvolvimento, executar os unitários afetados e, conforme o impacto, integração, segurança e/ou e2e. Ampliar a suíte quando houver falha, alteração transversal ou dúvida de regressão. Não rodar toda a verificação a cada pequeno ajuste. No fechamento do PR de implementação, cumprir a seção Verificação completa sem reduzir nenhum gate: build Release sem warnings, format, todos os testes .NET/cobertura/gate de 80%, SecurityTests, vulnerabilidades/modelos, lint/Vitest/build e Playwright/AppHost quando aplicável. Testes direcionados não autorizam declarar a verificação completa.

**Repetição:** depois de passar, não repetir sem nova mudança, falha ou preocupação concreta. Alterações posteriores exigem repetir os checks afetados e consolidar o estado final. Registrar o que foi executado e seus resultados/limitações no progress; manter os mesmos critérios de acessibilidade e segurança.

**Estado/verificação:** branch `codex/etapa-6c-datajud-integracoes`; arrumação pós-merge do #32 enviada em `928f0d3`, com manual/guia preservados. Este registro é novo commit documental local, sem push solicitado e sem implementação do 6C. AGENTS/CLAUDE sincronizados; UTF-8, conteúdo e diff conferidos antes do commit. Nenhuma mudança de runtime e nenhuma nova suíte completa executada para esta atualização das instruções. Manual e guia revisados, sem impacto nos fluxos do usuário ou na inicialização; não foi necessário alterar seus passos.

**Próximo passo:** seguir a preparação do 6C já registrada, usando esta política de testes. Preservar a instância do AppHost deixada para exploração do usuário e não iniciar AppHost.Tests em paralelo. Implementação nova termina em commit local; push/PR só a pedido. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (PR #32 mergeado; arrumação no branch do 6C)

**Aviso do usuário:** PR #32 aceito e mergeado. Confirmado no GitHub: `MERGED`, em 2026-10-19, merge `a137919915b0cc0f3eea555500389c7ec4acc08f`; todos os checks aprovados, incluindo AppHost/E2E. Merge feito pelo usuário, não pelo agente.

**Branch/estado:** main atualizada por fast-forward; criado `codex/etapa-6c-datajud-integracoes` dessa base. Os dois commits locais do manual foram preservados por cherry-pick sem conflitos: `2849304` virou `28e8764`; `fd31ce5` virou `9306958`. Não apagar esses documentos nem voltar às instruções anteriores. Esta arrumação documental recebe commit/push no branch do 6C, conforme autorização permanente do ciclo pós-merge; nenhum PR novo solicitado/criado.

**Feito:** 6B marcado como concluído no progress, ADR 0033 aceita (PR #32, 2026-10-19), Onde paramos atualizado e AGENTS/CLAUDE sincronizados. Manual e guia revisados: situação do 6B e base do 6C atualizadas, sem novo fluxo de tela nem comando de inicialização. A regra de revisar ambos após toda melhoria permanece obrigatória para Claude e Codex. Preparação do 6C registrada no progress, seguindo o plano geral já aprovado.

**Verificado:** merge e base conferidos; diff relativo à main somente em documentação; preservação dos arquivos, links/UTF-8 e igualdade dos arquivos de agentes verificadas antes do commit. CI do #32 todo aprovado; mantida a suíte completa registrada do 6B, sem repetir testes de runtime para esta arrumação. SPA `:4200`, prontidão da API `:5238/health/ready` e Mailpit `:8025` responderam HTTP 200 depois da troca de branch. Nenhuma alteração de implementação do 6C nesta entrega.

**Ambiente do usuário:** AppHost mantido em execução na sessão `32406`, log `TestResults/manual-apphost.log`. Não o encerrar como limpeza nem rodar AppHost.Tests em paralelo. Quando nova implementação exigir recompilar Debug/reiniciar o ambiente, considerar que o usuário está explorando as telas e coordenar a interrupção necessária.

**Próximo trabalho — 6C:** estudar contratos/provedores existentes e documentação oficial do DataJud; detalhar o adaptador, consultas de movimentações a cada seis horas distribuídas por processo ativo, cursor/paginação, idempotência, retomada e configurações seguras. O plano 6A a 6E e o início com fake + DataJud já foram aprovados; não pedir novamente aprovação das mesmas decisões. Fornecedor real permanece pendente de escolha, documentação e acesso; DataJud não substitui captura de publicações/intimações. Perguntar apenas dúvidas novas de negócio não cobertas pelas decisões. Triagem/Scheduling/fechamento no 6D; telas no 6E. Implementação nova termina em commit local; push/PR só a pedido, exceto a arrumação pós-merge já autorizada. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (manutenção obrigatória do manual e guia confirmada)

**Pedido explícito do usuário:** salvar o manual e guia no ambiente local e incrementá-los após cada melhoria, mantendo AGENTS, CLAUDE e o handover atualizados para que o Claude continue corretamente. O handover do projeto é este `docs/handoff.md`; não criar outro arquivo concorrente.

**Estado:** os dois arquivos já foram salvos no repositório no commit local `2849304`: `docs/manual/README.md` e `docs/manual/ambiente-local.md`. Este reforço fica em novo commit documental local, sem push/PR adicional. PR #32 permanece aguardando revisão/merge do usuário; não há autorização de merge.

**Regra para Codex e Claude:** em toda melhoria, revisar **ambos** os documentos; atualizar o manual quanto a telas, ações, permissões e disponibilidade, e o guia quanto a inicialização, dependências, endereços e operação local. Se um deles não for afetado, registrar isso na verificação da entrega em vez de criar instruções artificiais. Atualizar progress e acrescentar entrada neste handoff ao encerrar; AGENTS/CLAUDE permanecem sincronizados. Somente documentar recursos já entregues e marcar claramente os futuros. O manual inicial não tem imagens; podem ser acrescentadas quando os fluxos forem aprofundados.

**Dúvida de arquitetura respondida com código conferido:** o frontend Angular consome contratos HTTP por classes em `data/` e não acessa banco/entidades .NET. As regras definitivas de domínio, permissões e isolamento são aplicadas no backend. Existem validações de formulário e lógica de apresentação no navegador, incluindo a geração da prévia de parcelas; trocar o frontend exige reproduzir essas interações, autenticação e acessibilidade, além de manter o contrato da API. Mudança visual no Angular é localizada; reescrever em outro framework é possível, mas é trabalho maior. Nenhuma reescrita ou mudança de stack foi solicitada/aprovada; a stack continua fixa.

**Verificação:** alterações apenas documentais; conferidos arquivos salvos, links relativos/UTF-8, igualdade AGENTS/CLAUDE e diff antes do commit. Sem alteração de runtime e sem repetir a suíte completa do 6B. O guia de ambiente não teve seus comandos alterados nesta melhoria, pois não houve mudança operacional.

**Ambiente:** AppHost foi deixado para exploração do usuário na sessão `32406` (log `TestResults/manual-apphost.log`), conforme a entrada anterior. Não o encerrar como limpeza nem iniciar AppHost.Tests em paralelo enquanto ele estiver em uso.

**Próximo passo:** continuar documentando cada melhoria com essa regra; aguardar revisão/merge do #32 e pedido para enviar os complementos documentais locais. Depois do merge, seguir o ciclo de arrumação e preparação do 6C, preservando todos os documentos. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (manual inicial e AppHost disponível para exploração)

**Pedido do usuário:** quer visualizar a aplicação e começar um manual de utilização que evolua com as entregas. Implementado como documentação no repositório; o conector de documentos não está disponível nesta sessão. Não houve pedido de novo push/PR ou merge.

**Branch/estado:** `codex/etapa-6b-captura-alertas`; PR [#32](https://github.com/iagogoliveira/ProjetoLaw/pull/32) aberto, código e documentos do 6B enviados até `1693fd5`. Manual/guia e atualização de continuidade ficam em commit documental local desta entrada, sem push, para não cancelar o CI em execução. ADR 0033 continua proposta. Nenhum código de runtime alterado.

**Feito:** `docs/manual/README.md` cobre primeiro acesso, Mailpit, conta, permissões, configurações, pessoas/conflitos, processos, valores, risco, acordos, audiências, importação e propostas. Inclui roteiro de experimentação e lista de funcionalidades ainda sem tela. `docs/manual/ambiente-local.md` explica iniciar/parar/reabrir pelo PowerShell, endereços, persistência e problemas comuns. Primeira edição sem imagens; próximas entregas devem atualizar os fluxos correspondentes. Regra adicionada ao AGENTS/CLAUDE sincronizados.

**Verificado:** Docker 29.6.2; `dotnet run --project src/Aspire/LexFlow.AppHost --no-build` iniciado com Debug já compilado e sem alterações de backend desde a verificação do 6B. SPA `http://localhost:4200`, API `http://localhost:5238/health/ready` e Mailpit `http://localhost:8025` responderam HTTP 200. Rotas/componentes/traduções/e2e foram consultados para o manual; links, texto e diff documental conferidos antes do commit. Suíte completa não repetida para documentação; resultados do 6B preservados.

**Ambiente em uso:** AppHost permanece executando para o usuário, sessão de execução `32406`, log local `TestResults/manual-apphost.log`. Não encerrar como rotina de limpeza enquanto o usuário explora as telas; não executar AppHost.Tests em paralelo. O usuário cria sua conta pela interface e confirma e-mails pelo Mailpit; nenhuma credencial de demonstração foi criada para ele.

**Próximo passo:** permitir a exploração manual, receber observações e ampliar o manual conforme as telas evoluírem. Enviar o complemento documental somente quando solicitado. Aguardar CI/revisão/merge do #32; depois seguir o ciclo de arrumação e o 6C. Não fazer merge pelo usuário.

---

## 2026-10-19 — Codex (envio e abertura do PR 6B autorizados)

**Branch:** `codex/etapa-6b-captura-alertas`. **Decisão do usuário:** “Crie o PR”. Isso autoriza o push do código/documentação e a abertura do PR para `main`; não autoriza merge. Implementação pronta no commit `6de6b4f`, após `d46b774` (perguntas) e `4bcc2cd` (arrumação pós-merge do #31).

**Estado e envio:** esta entrada registra a autorização antes da publicação, em commit documental para acompanhar o código no mesmo push. Base remota `main` conferida em `f2e01a9`; nenhum PR anterior encontrado para este branch. Após o push, abrir o PR 6B, anexá-lo à conversa e confirmar o endereço/estado no GitHub. A ADR 0033 permanece proposta até o merge pelo usuário.

**Verificado:** diretório limpo antes desta atualização, branch e commits conferidos, documentação de envio sincronizada em progress/AGENTS/CLAUDE e diff documental a verificar antes do commit. Nenhuma mudança de runtime; preservada a verificação completa do `6de6b4f`: 771 testes .NET, cobertura acima de 80%, builds/formatação, pacotes/modelos, lint, 96 Vitest, build Angular e duas rodadas Playwright de 14 testes cada. Ocorrências e relatórios estão registrados na entrada anterior e no progress; não repetir sem nova mudança ou falha.

**Próximos passos:** concluir a publicação autorizada e aguardar CI/revisão/merge pelo usuário. Se houver falha no CI, investigar e corrigir no branch do 6B. Depois do merge, executar o ciclo documental e preparar o 6C; fornecedor real continua pendente, triagem/fechamento são do 6D e telas do 6E. Não iniciar outro PR de implementação antes do merge do 6B.

---

## 2026-10-19 — Codex (6B implementado e verificado; commit local, sem push/PR)

**Branch:** `codex/etapa-6b-captura-alertas`, da main/merge `f2e01a9`. **Estado:** arrumação pós-merge do #31 enviada em `4bcc2cd`; dúvidas registradas no commit local `d46b774`, depois respondidas pelo usuário. Código e documentos do 6B consolidados no commit local desta entrega, sem push nem PR da implementação. ADR 0033 proposta; 6A #31 permanece concluído e ADR 0032 aceita.

**Decisões novas aprovadas:** o usuário respondeu “Adotar a sugestão” às duas perguntas: alertas de captura por e-mail/in-app para membros ativos com `settings.integrations.manage` ou `publications.triage` em escopo de escritório; novas publicações somente in-app/SignalR para `publications.read` com alcance do conteúdo; fontes criadas/reativadas depois de 05h capturam imediatamente, depois de 07h ganham até uma hora; execução concluída com zero publicações conta como sucesso. Nenhuma dessas perguntas continua pendente.

**Feito:** contratos de provedores e fake explícito no AppHost (padrão Disabled fora dele); jobs de captura/monitor separados, janela e retentativas, ativação tardia, timeout, locks PostgreSQL por fonte/dia e hash, deduplicação concorrente, alerta persistido/resolução e métrica. Platform ganhou notificações por referência e recibo por usuário, consumers com inbox e e-mail apenas de alerta. REST e stream SignalR LongPolling renovam pertença/permissões/MFA/escopo/sigilo, restauram avisos persistidos na reconexão e não transmitem texto por broadcast. Migrations aditivas com RLS, quatro rotas OpenAPI novas e cliente gerado.

**Verificado:** Release/Debug sem warnings; format; 771 testes .NET em 18 projetos com cobertura/gate de 80% (Publications.Application 99,2%, Domain 95,9%); pacotes sem vulnerabilidades e cinco contextos sem modelo pendente; lint, 96 Vitest e build Angular. Contrato anterior preservado, só adições. Nove testes API de captura/notificações incluem concorrência e stream SignalR real com reconexão/revogação de alcance. Playwright contra o AppHost aprovado em duas rodadas finais consecutivas, com 14 testes em cada (2,0 min e 1,6 min), relatório HTML e um worker; apenas o preparo opcional de carga foi pulado. AppHost de verificação encerrado ao fim.

**Ocorrências preservadas:** a primeira suíte API pegou o snapshot OpenAPI anterior; após gerar oficialmente o contrato e o cliente, os 164 testes passaram. A primeira regressão Playwright com oito workers, durante verificações .NET, teve cinco falhas em fluxos existentes (controles/esperas, acessibilidade e convite); logs/traces/HTML em `TestResults/6b-playwright-initial-failure*`. A execução inicial de AppHost.Tests foi interrompida para não disputar portas com o AppHost manual; a repetição isolada passou (14 testes). As rodadas finais de navegador usam uma única instância do AppHost e `--workers=1`; não foram relaxadas asserções nem critérios de acessibilidade.

**Próximo passo:** aguardar pedido de push/PR; enviar código e documentos juntos no mesmo push quando autorizado. Progress/AGENTS/CLAUDE atualizados e sincronizados; resultados completos e caminhos dos relatórios no progress. Não repetir testes sem nova mudança, falha ou dúvida que justifique. Push e PR da implementação somente quando o usuário pedir. Depois do merge, seguir o ciclo para o 6C; fornecedor real continua pendente, DataJud não substitui publicações. Triagem/Scheduling/fechamento são do 6D, telas e cliente visual SignalR do 6E. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (arrumação do #31 enviada; duas regras complementares antes do 6B)

**Branch:** `codex/etapa-6b-captura-alertas`. **Estado:** arrumação pós-merge enviada no commit `4bcc2cd`, a partir de `main`/merge `f2e01a9`. 6A #31 concluído, ADR 0032 aceita. Nenhum código ou PR do 6B criado. Este registro das perguntas fica em commit documental local, para acompanhar o próximo envio autorizado.

**Feito/verificado:** confirmado merge do #31 e main atualizada; branch do 6B criado; progresso/AGENTS/CLAUDE/handoff atualizados, diff documental conferido e AGENTS/CLAUDE idênticos. Lidos novamente RF-05/RF-10, seção da captura e contratos locais de jobs/estrutura. Não repetida a suíte de runtime porque só houve documentação; mantidos os resultados completos do 6A.

**Aguardar respostas do usuário antes de implementar as regras:**
1. Alertas de captura ausente por e-mail/in-app para membros ativos com `settings.integrations.manage` ou `publications.triage` em escopo de escritório? Novas publicações só por in-app/SignalR, para quem tem `publications.read` e alcance do conteúdo, sem e-mail individual? Essa é a proposta, ainda não aprovada especificamente.
2. Fontes criadas/reativadas após 05h capturam imediatamente? Depois de 07h, tolerância de até uma hora desde a ativação antes do alerta? Execução concluída com zero publicações conta como sucesso, ao contrário de falha ou ausência de execução? Essa é a proposta, ainda não aprovada especificamente.

**Motivo:** o plano 6B e os canais foram aprovados, mas os destinatários e essas situações de captura não estavam definidos no plano, especificação ou ADRs. Segue a regra do projeto de perguntar antes de decidir dúvida de negócio; não pedir nova aprovação do plano inteiro. Não presumir resposta pelo silêncio. Com as respostas, registrar decisões, formalizar ADR 0033 proposta e implementar/verificar o 6B; commit local, sem push/PR até pedido. Nunca fazer merge.

---
## 2026-10-19 — Codex (6A #31 mergeado; arrumação e início do 6B aprovado)

**Branch:** `codex/etapa-6b-captura-alertas`, criado da `main` atualizada no merge `f2e01a9` do PR #31. **Estado:** o usuário informou merge e aprovação; confirmado `MERGED` no GitHub em 2026-10-19. Arrumação documental em commit/push neste branch, conforme o ciclo já autorizado. Nenhum PR do 6B aberto.

**Feito:** main atualizada por fast-forward; 6A concluído no progresso; ADR 0032 aceita; AGENTS/CLAUDE sincronizados. Conferidos branch/base, diff documental e ausência de alterações locais anteriores. Mantida a verificação completa do 6A (751 testes .NET, cobertura, frontend e duas rodadas Playwright); esta arrumação não muda runtime.

**Próximo:** seguir a implementação do **6B já aprovado**, um PR por vez: provedores fake, captura idempotente inclusive concorrente, jobs às 05:00 e retentativas até 07:00, monitor independente de captura ausente, alerta persistido sem duplicação/métrica e notificações mínimas in-app/e-mail/SignalR com isolamento e autorização. Detalhar o desenho na ADR 0033 proposta, adicionar testes e executar verificação completa. Não antecipar DataJud/fornecedor real, triagem/fechamento ou telas de fila. A implementação do 6B termina em commit local; push/PR só quando solicitado. Nunca fazer merge.

**Decisões:** nenhuma decisão de negócio nova. Todas as escolhas da aprovação da Etapa 6 permanecem válidas, incluindo fake + DataJud inicialmente e fornecedor real como pendência, núcleo mínimo de Scheduling no 6D, `publications.close` e fechamento por recebimento no fuso legal.

---
## 2026-10-19 — Codex (envio e PR do 6A autorizados)

**Branch:** `codex/etapa-6-publicacoes-andamentos`, base `main`. **Implementação:** commit `a4c2d75` — “Etapa 6A: base de publicações, fontes e andamentos”. O usuário pediu “Faça push e PR”; este registro documental acompanha o código no mesmo push. A entrega segue para revisão como PR 6A, com a ADR 0032 ainda proposta.

**Feito nesta entrega:** conferência do branch, diretório limpo, commit e remoto; verificado que não havia PR aberto para o branch; registro da autorização e preparação do envio e do PR. O resultado da publicação e o link do PR são confirmados na resposta desta conversa. Não há alteração de código em relação à verificação completa anterior: 751 testes .NET, gate de cobertura, frontend e duas rodadas Playwright aprovados. Somente documentação de estado foi atualizada; diff e sincronização AGENTS/CLAUDE conferidos.

**Próximo:** acompanhar o CI e a revisão do PR 6A; aguardar o usuário fazer e avisar do merge. Depois, atualizar a main, aceitar a ADR 0032 com número/data do PR, atualizar progresso/AGENTS/CLAUDE/handoff, criar o branch do 6B e fazer a arrumação pós-merge. Nunca fazer merge pelo usuário. As decisões da Etapa 6 continuam aprovadas; fornecedor real permanece pendente e não bloqueia o 6A.

---
## 2026-10-19 — Codex (plano da Etapa 6 aprovado; PR 6A implementado e verificado localmente)

**Branch:** `codex/etapa-6-publicacoes-andamentos`. **Estado:** último commit enviado `beee338` (arrumação pós-merge do #30); 6A implementado e verificado, em commit local desta entrega, sem novo push e sem PR aberto. O usuário aprovou o plano e as recomendações com “Ok, gostei das sugestões, pode seguir.” Não pedir novamente aprovação das mesmas escolhas.

**Decisões aprovadas:** iniciar com fake + DataJud, mantendo fornecedor real de publicações como pendência explícita; antecipar o núcleo mínimo de Scheduling com confirmação humana e transação local PostgreSQL compartilhada (ADR específica no 6D); fontes/credenciais com `settings.integrations.manage`, consulta com `publications.read`, triagem com `publications.triage`, distribuição com `publications.distribute` e fechamento com nova `publications.close`; escritório individual pode acumular permissões sem segunda aprovação. Fechamento pela data de recebimento em America/Sao_Paulo, incluindo pendências anteriores, e captura esperada ausente impede conclusão integral. A data da publicação continua separada.

**Escopo do 6A:** cinco projetos Publications, fontes OAB/nome/CNPJ, estrutura de captura/publicação/triagem/andamento, RLS, consultas paginadas, entrada interna de publicação com extração/validação do CNJ, vínculo por Contracts, andamento manual com autorização do processo e contribuição na timeline. ADR 0032 proposta. Nenhum provedor externo, job de captura, triagem, fechamento ou tela de fila antecipado.

**Segurança:** o novo `ICaseDirectory` usa o `CaseAccess` do Litigation com a permissão do caso de uso; não há acesso a outro DbContext. A fila sem vínculo exige escopo de escritório e oculta também publicações que mencionam CNJs inacessíveis, inclusive ambíguas, para preservar o sigilo antes de haver vínculo.

**Verificação concluída:** Docker Desktop rodando; builds Release/Debug sem warnings, format sem alterações; **751 testes .NET em 18 projetos**, incluindo os 9 novos de API e os 14 do AppHost; cobertura >=80% em todas as camadas (Publications.Application 100%, Domain 95,4%); NuGet sem vulneráveis; cinco contextos sem mudança de modelo pendente. OpenAPI gerado pelo teste oficial, diff apenas aditivo, cliente TypeScript regenerado. Lint, 96 Vitest e build Angular aprovados. Duas rodadas finais consecutivas de Playwright: **14 aprovados em cada (37,6 s e 29,2 s)**, com relatório HTML e WCAG; apenas preparo opcional de carga pulado. Artefatos ignorados em `TestResults/6a-*` e `src/Web/lexflow-web/playwright-report`.

**Armadilha confirmada:** a repetição imediata das suítes com limite local padrão (120/min por IP) gerou 429 na confirmação de e-mail; traces preservados em `TestResults/6a-playwright-rate-limit-failure`, rodada interrompida. Reiniciar o AppHost com `LexFlow__PublicPermitsPerMinute=10000` (configuração existente, só E2E) permitiu as duas rodadas finais. Não foi mudado limite de produção nem mascarado erro no teste; orientação incluída em AGENTS/CLAUDE. O AppHost da verificação final ficou em execução.

**Próximo:** aguardar o pedido do usuário para enviar o commit local do 6A e abrir o PR. Não fazer push nem abrir PR sem pedido. Depois do merge do 6A pelo usuário, seguir o ciclo pós-merge e o 6B; não implementar 6B no mesmo PR.

---

## 2026-10-19 — Codex (PR #30 mergeado; arrumação e plano proposto da Etapa 6)

**Branch:** `codex/etapa-6-publicacoes-andamentos`, criado da `main` atualizada no merge `33ec6bb` do PR #30. **Estado:** esta arrumação documental segue em commit/push neste branch, como já autorizado pelo ciclo pós-merge. Nenhum código da Etapa 6 foi implementado e nenhum PR da Etapa 6 foi aberto.

**Feito:** o usuário informou que os testes passaram e que fez o merge; o estado `MERGED` e a data de 2026-10-19 foram confirmados no GitHub. `main` atualizada por fast-forward; 5E2 e Etapa 5 concluídos no progresso; ADR 0031 aceita; `AGENTS.md` e `CLAUDE.md` atualizados e sincronizados. O registro local da leitura integral da especificação/ADRs foi preservado e incluído aqui, sem perda do histórico.

**Verificação desta arrumação:** conferência do merge e da base do branch, diff somente documental, `git diff --check` e igualdade de `AGENTS.md`/`CLAUDE.md`. Não houve alteração de runtime, pacote, migration, endpoint ou contrato; a suíte completa aprovada no 5E2 e o CI informado pelo usuário continuam sendo a última verificação de código.

### Plano da Etapa 6 — proposta para aprovação, sem implementação

**Objetivo:** RF-10 e RF-11, seção 10 da `implementacao.md`: captura rastreável de publicações, vinculação ao processo, triagem da controladoria, andamentos e fechamento diário. Um PR por vez, com a verificação completa e uma ADR por mudança de arquitetura. O plano abaixo ainda não foi aprovado pelo usuário.

| PR | Entrega proposta | Testes previstos |
| --- | --- | --- |
| 6A | Criar os cinco projetos de Publications, entidades `CaptureSource`, `CaptureRun`, `Publication`, `PublicationTriage` e `CaseMovement`, schema/RLS, consultas e gravação manual; fontes de OAB/nome/CNPJ; contratos para localizar processos e validar alcance sem acessar outro DbContext; vínculo por CNJ validado e contribuição na linha do tempo | Domínio, migrations do zero/incrementais, API, descoberta automática e casos entre organizações nos SecurityTests, escopo e muralha ética, OpenAPI/cliente gerado |
| 6B | `IPublicationProvider`/`IMovementProvider` com fake determinístico; captura das 05:00, retentativas até 07:00 e retomada; hash de diário/data/texto normalizado; estados vinculada/não vinculada; monitor independente da captura, alerta persistido sem duplicação e métrica `lexflow_capture_missing_total`; infraestrutura mínima de notificações in-app/e-mail e SignalR com autorização por usuário/escritório/processo | Reexecutar captura não duplica; nenhuma publicação é descartada; provedor fake indisponível gera alerta; sucesso posterior resolve a situação de captura; reconexão e isolamento das notificações |
| 6C | Adaptador DataJud de metadados/movimentações públicas e adaptador do fornecedor contratado (dependente da escolha e do contrato); configurações/credenciais fora do código; andamentos a cada 6 h por processo ativo, distribuídos ao longo do dia; cursor por tribunal, paginação `search_after`, idempotência e reprocessamento | Contratos de provedores com respostas gravadas/anônimas, indisponibilidade, paginação, deduplicação, relógio falso e limites de concorrência; testes reais opcionais sem dependência externa no CI |
| 6D | Triagem com as seis classificações da especificação, distribuição, vínculo manual auditado e criação de prazo/tarefa/audiência; fechamento diário com responsável e bloqueio por pendências. Antecipação mínima de Scheduling e transação entre módulos dependem de aprovação (abaixo) | Rollback conjunto com falha induzida; repetição idempotente; permissão/escopo de cada recurso; fechamento recusado enquanto houver pendência; fechamento auditado |
| 6E | `features/publications`: configuração das fontes, fila com filtros e paginação, não vinculadas, painel texto/formulário, CNJ destacado, atalhos de teclado, distribuição e fechamento; contador no menu, notificações/SignalR e andamentos na linha do tempo | Vitest de cada store, chaves pt-BR/en iguais, Playwright contra o AppHost em duas rodadas, `expectAccessible` nas telas novas e casos de acesso negado |

**Permissões propostas, para validação no plano:** fontes e credenciais com `settings.integrations.manage`; consulta com `publications.read`; triagem com `publications.triage`; distribuição com `publications.distribute`; andamentos manuais com `cases.edit` e alcance do processo. O fechamento pode usar `publications.triage` ou uma permissão própria, conforme a decisão do usuário. Novas permissões exigem catálogo, perfis e testes, inclusive nos escritórios existentes quando aplicável.

**Vínculo e sigilo:** os dados ligados a processo devem implementar `ICaseScoped` e respeitar o alcance completo e o sigilo do processo; `ICaseVisibility` hoje cobre os ids ocultos por sigilo e precisará ser complementado para as consultas necessárias. CNJs ambíguos não devem ser vinculados arbitrariamente; permanecem para conferência manual. Não expor texto nem notificações de processo fora do alcance do usuário.

**Dependência e atomicidade a aprovar:** a especificação exige que a triagem crie prazo/tarefa/audiência na mesma transação, mas Scheduling só está previsto para a Etapa 7. A ADR 0007 e o `IUnitOfWork` atual abrem transação por módulo, portanto apenas gravar um evento no outbox não atende ao critério. Recomendo antecipar o núcleo mínimo de prazos/tarefas (processo, responsável, data fatal explícita e estado inicial, com validação e confirmação humana) e criar um coordenador de transação local compartilhada entre os contextos participantes, com comunicação por Contracts e rollback conjunto. A calculadora de prazos, agenda completa, feriados forenses, revisão e cumprimento com evidência seguem na Etapa 7. O desenho da transação e a exceção à decisão por módulo devem ser formalizados em ADR antes de implementar; não usar gravações sequenciais independentes como se fossem atômicas.

**Critérios de aceite propostos (nomes de testes ainda não implementados):**
- `Capturing_twice_does_not_duplicate_publications`;
- `Every_captured_publication_is_linked_or_waits_for_linking`;
- `An_unavailable_provider_raises_a_missing_capture_alert`;
- `A_failed_triage_rolls_back_the_created_deadline`;
- adicional: `Closing_a_day_with_pending_publications_is_refused` e testes de isolamento/escopo em todas as rotas e notificações.

**Dúvidas para o usuário antes de implementar:**
1. Qual fornecedor de publicações/intimações está contratado e há documentação/sandbox? Se ainda não houver, autoriza começar com fake + DataJud e manter o adaptador contratado como pendência explícita (a etapa não será declarada integralmente concluída sem ele)? DataJud atende movimentações públicas; não substitui a captura contratada de diários/intimações.
2. Aprova antecipar o núcleo mínimo de Scheduling para gerar os registros reais na triagem, com responsável/data informados e confirmados pelo usuário, mantendo a calculadora e a agenda completa na Etapa 7? Aprova a proposta de transação local compartilhada formalizada em ADR?
3. Fontes/integrações ficam com o Administrador (`settings.integrations.manage`) e o fechamento com quem pode triar, ou o fechamento deve ter permissão própria?
4. Para o fechamento, "do dia" significa data de captura/recebimento ou data de publicação no diário? Recomendo capturadas/recebidas até a data escolhida, no fuso legal, incluindo pendências anteriores para não ocultar atraso; confirmar antes de gravar essa regra.

**Fontes técnicas consultadas para o plano:** [API pública do DataJud](https://datajud-wiki.cnj.jus.br/api-publica/), [acesso](https://datajud-wiki.cnj.jus.br/api-publica/acesso/) e [paginação](https://datajud-wiki.cnj.jus.br/api-publica/exemplos/exemplo3/), além da especificação, das ADRs e do código local. A chave pública vigente deve ser configurada, nunca copiada para o código/documentação do projeto.

**Próximo agente:** aguardar respostas e aprovação do plano. Não implementar nem abrir o PR 6A ainda. Após aprovação, registrar as decisões no progresso, detalhar apenas o primeiro PR e executar um PR por vez.

---

## 2026-10-19 — Codex (leitura integral da especificação e das ADRs)

**Branch:** `etapa-5e2-telas-valores-importacao`. **Estado:** PR #30 aberto na sessão anterior, com os commits `e993a8b` (implementação) e `775484d` (autorização do envio). Esta entrada é somente um registro local da leitura, sem alteração de código nem novo push.

**Pedido do usuário:** ler integralmente os dois documentos de `docs/spec` e todos os documentos de `docs/adr`. Antes deste pedido, a especificação havia sido consultada por seções pertinentes ao 5E2; isso foi esclarecido ao usuário.

**Leitura concluída:** `docs/spec/implementacao.md` e `docs/spec/requisitos-v3.md`, do início ao fim, e todas as 31 ADRs existentes, de 0001 a 0031, incluindo contexto, decisões, consequências, pendências e atualizações. As decisões posteriores registradas nas ADRs e no progresso prevalecem sobre os exemplos genéricos da especificação. Esta leitura não é uma nova auditoria do código nem uma nova execução dos testes.

**Próximo:** aguardar o usuário avisar do merge do PR #30 antes da arrumação e do planejamento da Etapa 6. Nenhuma nova decisão de negócio ou implementação nesta solicitação.

---

## 2026-10-19 — Codex (envio do 5E2 aprovado pelo usuário)

**Branch:** `etapa-5e2-telas-valores-importacao`. **Commit de implementação:** `e993a8b` (`Etapa 5E2: telas de valores, acompanhamento e importação`).

**Decisão do usuário:** aprovou o resultado e autorizou enviar o commit e criar o PR para a `main`. Este registro é feito antes do envio para que código e documentação sigam juntos em um único push. Nenhuma mudança de código após a verificação completa registrada abaixo; este commit só documenta a autorização.

**Estado neste registro:** implementação e documentação commitadas localmente; envio e criação do PR autorizados. Consulte o PR pelo branch no GitHub para obter o número, a URL e o estado atual do CI, antes de tentar reenviar ou criar outro PR.

**Próximo:** conferir o CI e aguardar o merge pelo usuário. Nunca fazer merge. Quando o usuário avisar do merge, seguir a arrumação e o planejamento da Etapa 6 descritos na entrada anterior. A Etapa 5 e a ADR 0031 ainda aguardam o merge para serem marcadas como concluída/aceita.

---

## 2026-10-19 — Codex (5E2 implementado e verificado)

**Branch:** `etapa-5e2-telas-valores-importacao`. **Estado:** código e documentos neste commit local; sem push, sem PR e sem merge. O plano aprovado foi implementado diretamente, sem nova decisão de negócio.

**Feito:**
- Abas Pedidos e valores, Risco, Acordos e Audiências, em componentes próprios com carregamento ao abrir, forms reativos, OnPush, SignalStores e permissões. Chamadas concentradas no `CasesApi`.
- Pedidos com correção por data, totais e histórico; risco por processo/pedido e histórico; fluxo completo dos acordos com gerador de parcelas (centavos na última, vencimentos ancorados no primeiro dia escolhido), aprovação, homologação e baixa manual; audiências por formato, pessoas e membros, redesignação com histórico e realização/cancelamento.
- `/processos/importar`: modelo, envio, consulta periódica, prévia, confirmação, andamento, resultado e relatório XLSX como Blob. A URL permite retomar após recarregar e a consulta para ao sair da tela.
- O e2e revelou uma falha anterior no `CaseImportJobs.ImportAsync`: a contagem final consultava o banco antes de salvar os erros da revalidação e zerava as inválidas. Corrigido para contar os erros em memória e os persistidos na retomada. O teste de integração das 10 mil linhas agora também confere os totais finais 10.000 / 9.700 / 300.
- Proteção contra consulta de histórico durante a gravação do pedido, com regressão no Vitest. Datas de audiência usam o fuso legal, inclusive as regras históricas de horário de verão.
- Textos pt-BR/en, testes das cinco stores e dos auxiliares, e2e novos e relatório Playwright ligado também localmente, com trace/screenshot nas falhas.
- ADR 0031 proposta, `progress.md` atualizado e `AGENTS.md`/`CLAUDE.md` sincronizados integralmente (o `CLAUDE.md` anterior tinha só um resumo das regras).

**Verificado:** builds Release e Debug sem warnings; `dotnet format --verify-no-changes`; 721 testes .NET em 16 projetos (incluindo 14 do AppHost), com cobertura e gate de 80% aprovados; Litigation.Application 92,4% e Domain 97,9%; sem pacotes vulneráveis nem mudanças de modelo pendentes nos quatro módulos; lint, build Angular e 96 Vitest aprovados. **Playwright completo contra o AppHost: 14 aprovados em duas execuções consecutivas**, com WCAG 2.1 AA nas abas novas e na importação. Só o preparo da carga é pulado. Nenhum pacote, migration ou contrato novo.

**Armadilhas confirmadas nesta sessão:**
- A CLI do Aspire executou API/Worker em Debug mesmo ao chamar `dotnet run ... -c Release --no-build`. Depois de mudar o backend, compile Debug antes de usar `--no-build` e aguarde HTTP 200 em `:4200` antes dos e2e.
- Para avançar passos consecutivos, além de `.filter({ visible: true }).last()`, espere o conteúdo do passo anterior ficar escondido. O auxiliar `e2e/support/cases.ts` usa o `aria-controls` do cabeçalho expandido para isso.
- Use relatórios de cobertura de uma mesma versão dos fontes para o gate; a rodada final isolada está em `TestResults/5e2-final-validation`. O relatório de telas mais recente está em `src/Web/lexflow-web/playwright-report/index.html`.

**Próximos passos combinados com o usuário:**
1. Aguardar o pedido de push/PR. Código e documentos no mesmo push, pois o CI cancela o run anterior a cada push. Nunca fazer merge.
2. Quando o usuário avisar do merge, atualizar a `main`, concluir o 5E2 e a Etapa 5 no progresso, aceitar a ADR 0031 e criar o branch da Etapa 6; atualizar `AGENTS.md`, `CLAUDE.md` e esta passagem; fazer commit e push da arrumação.
3. Só então planejar a Etapa 6 (publicações e andamentos, seção 10 da `implementacao.md`), apresentar o plano e as dúvidas de negócio, e esperar aprovação antes de implementar. Sem pendência de código ou verificação no 5E2.

---

## 2026-10-19 — Claude Code (plano do 5E2 aprovado; passagem para o Codex)

**Branch:** `etapa-5e2-telas-valores-importacao` (já criado e enviado, com a arrumação do #29). **Nenhum código do 5E2 foi escrito.** O usuário aprovou o plano e pediu para outro agente (Codex) continuar daqui.

**Plano do PR 5E2 (aprovado pelo usuário em 2026-10-19; implementação ainda não começou).** Só frontend: as APIs existem desde o 5C1 ao 5D, e não há permissão nova.
- **Aba "Pedidos e valores"** no detalhe do processo (`features/cases/pages/case-detail-page.*`):
  - lista dos pedidos (objeto da tabela "Objetos/pedidos" via `options.claimTypes`, descrição, forma de correção, valores por instância);
  - cadastrar, editar e excluir (`cases.edit`; rotas `/api/v1/cases/{id}/claims`);
  - campo "Atualizar até" (padrão: hoje) → `GET /cases/{id}/claims?updateTo=`: valores corrigidos, totais por instância e o aviso `correctionError` por pedido;
  - histórico de cada pedido (`GET /cases/{id}/claims/{claimId}/history`).
- **Aba "Risco":**
  - risco atual (`GET /cases/{id}/risk`) com provisão e passivo contingente;
  - formulário de avaliação só com `*lfCan="'cases.risk.assess'"` (`POST /cases/{id}/risk-assessments`), por processo ou por pedido;
  - histórico (`GET /cases/{id}/risk/history`).
- **Aba "Acordos":**
  - lista (`GET /cases/{id}/settlements`) com situação, valor pedido × acordado (`difference`/`differencePercent`: economia quando o cliente paga, deságio quando recebe), rodadas e parcelas (`isOverdue` em destaque);
  - ações conforme a situação: nova rodada (`offers`), enviar termos (`submit`), aprovar (`approve`, só com `cases.settlements.approve`), devolver (`return`), homologar (`homologate`), recusar (`reject`), cancelar (`cancel`), baixa (`installments/{installmentId}/pay`);
  - **gerador de parcelas** na tela: número, primeiro vencimento e intervalo mensal, com a última parcela ajustando os centavos para a soma bater com o valor acordado (o servidor recusa com `litigation.settlement_installments_mismatch`).
- **Aba "Audiências":**
  - lista (`GET /cases/{id}/hearings`) por data, com o histórico de redesignações;
  - agendar e editar: o formato pede local (presencial), link (virtual) ou os dois (híbrida); advogado de `options.members`, preposto e testemunhas (busca de pessoas como no assistente);
  - redesignar com motivo (`reschedule`), realizar com resultado ou ata (`hold`), cancelar (`cancel`).
- **Tela de importação** `/processos/importar` (`cases.create`, link na lista):
  1. modelo (`GET /cases/imports/template`) e envio (`POST /cases/imports`, multipart campo `file`, resposta 202 com `importId`/`jobId`);
  2. acompanhamento por consulta periódica de `GET /cases/imports/{id}` (ou `GET /api/v1/jobs/{jobId}`) até sair de `validating`/`importing`;
  3. pré-visualização com totais, `preview` e os primeiros `errors`;
  4. confirmação (`POST /cases/imports/{id}/confirm`);
  5. andamento e resultado;
  6. relatório das inválidas (`GET /cases/imports/{id}/errors`, XLSX; baixar como Blob, ver `features/funnel/data/save-file.ts` e o spread `{ responseType: 'blob' } as object` usado no `FunnelApi`).
- **Onde pôr o código:**
  - chamadas novas no `CasesApi` (único ponto de HTTP);
  - stores novas em `features/cases/data` (ex.: `claims-store.ts`, `settlements-store.ts`, `hearings-store.ts`, `import-store.ts`), com testes Vitest no padrão de `cases-stores.spec.ts`;
  - se o detalhe crescer demais, extrair cada aba em componente próprio em `features/cases/pages/tabs/`.
- **Textos:** pt-BR e en no `core/i18n/*.json`, com as mesmas chaves. Os erros `litigation.*` e `platform.*` já estão traduzidos.
- **E2E** (`e2e/cases-values-flow.spec.ts` e `e2e/case-import-flow.spec.ts`), contra o AppHost, onde o Worker roda os jobs de verdade:
  - importação de CSV com linhas válidas e inválidas até a gravação e o relatório;
  - acordo da proposta à homologação e baixa de parcela;
  - audiência redesignada e realizada;
  - pedido com valor atualizado e histórico;
  - `expectAccessible` em cada aba;
  - abrir mat-select pelo teclado e clicar no "Próximo" visível com `.last()` (armadilhas no `AGENTS.md`);
  - rode os e2e com o relatório ligado: no 5E1, a primeira execução logo depois de subir o AppHost falhou uma vez num `toBeVisible` sem detalhe capturado.
- **Ao concluir:**
  - ADR 0031;
  - seção do 5E2 no `progress.md`: o 5E2 fecha a Etapa 5, então marque a etapa como concluída depois do merge;
  - `AGENTS.md`, entrada aqui, verificação completa e commit;
  - push e PR só quando o usuário pedir.

**Próximo agente:** leia o `AGENTS.md` (regras e armadilhas) e implemente o 5E2 conforme o plano acima, no branch `etapa-5e2-telas-valores-importacao`. Não é preciso mostrar o plano de novo, porque já foi aprovado; dúvidas de negócio que surgirem vão para o usuário.

---

## 2026-10-19 — Claude Code (arrumação depois do merge do #29)

**Branch:** `etapa-5e2-telas-valores-importacao`, criado a partir da `main`, com este commit de arrumação e push.

**Feito:**
- PR #29 (5E1) mergeado pelo usuário. Antes do merge, duas correções de CI:
  - a exceção do oasdiff;
  - o e2e passou a abrir os mat-select pelo teclado (registrado no `AGENTS.md`).
- `progress.md`: 5E1 concluído. ADR 0030 aceita. `AGENTS.md` atualizado.

**Próximo:** planejar o PR 5E2 (abas de Pedidos e valores, Risco, Acordos e Audiências, e a tela de importação) e mostrar o plano ao usuário antes de implementar.

---

## 2026-10-18 — Claude Code (PR 5E1 implementado)

**Branch:** `etapa-5e-telas-processos` · **Estado:** commitado; o push e o PR aguardam o pedido do usuário.

**Feito:**
- **Backend:**
  - preferências de tela por usuário (`platform.user_preferences`, `GET`/`PUT /api/v1/me/preferences`);
  - opções de cadastro (`GET /api/v1/cases/options`);
  - correção do contrato (`CaseAssignmentResponse`).
- **Frontend `features/cases`:**
  - lista com filtros salvos, colunas e lote;
  - assistente em passos;
  - detalhe com ações e as abas Resumo, Partes, Hierarquia, Linha do tempo e Sigilo;
  - numeração nas configurações;
  - textos pt-BR e en.
- **E2E** `cases-flow.spec.ts`: passou contra o AppHost, junto com a suíte inteira (12 aprovados).
- ADR 0030, `progress.md` e `AGENTS.md` atualizados.

**Atenção no CI:** o oasdiff vai acusar a troca do esquema de `CaseResponse.assignments`. É uma correção de descrição, porque o JSON não mudou. O CI agora lê `docs/openapi/oasdiff-ignore.txt`: copie para lá a mensagem exata que o CI mostrar e faça um commit pequeno.

**Próximo agente:**
1. Quando o usuário pedir, faça o push e abra o PR do 5E1. Trate a exceção do oasdiff como descrito acima.
2. Depois do merge, faça a arrumação:
   - ADR 0030 aceita;
   - crie o branch do 5E2;
   - atualize o `AGENTS.md` e escreva uma entrada aqui.
3. Planeje o 5E2.

---

## 2026-10-18 — Claude Code (arrumação depois do merge do #28)

**Branch:** `etapa-5e-telas-processos`, criado a partir da `main`, com este commit de arrumação e push.

**Feito:**
- PR #28 (5D) mergeado pelo usuário. Antes do merge, uma correção de CI: `Path.GetFileName` no Linux (registrado no `AGENTS.md`).
- `progress.md`: 5D concluído, e os 3 critérios da Etapa 5 atendidos.
- ADR 0029 marcada como aceita.
- "Onde paramos" do `AGENTS.md` atualizado.

**Pendência anotada:** o upload dos modelos de proposta (Crm) também usa `Path.GetFileName`. Um nome com caminho do Windows ficaria inteiro num servidor Linux. Isso pede um PR pequeno, se o usuário quiser.

**Plano do 5E mostrado ao usuário** (aguardando aprovação), dividido em dois PRs:
- **5E1:** `features/cases` com:
  - lista com filtros salvos, colunas configuráveis e operações em lote;
  - assistente de cadastro em passos;
  - detalhe com Resumo, Partes, Hierarquia, Linha do tempo, Sigilo e as abas reservadas (Documentos, Prazos, Financeiro);
  - aba "Numeração de processos" nas configurações;
  - e2e.
- **5E2:** abas de Pedidos e valores, Risco, Acordos e Audiências, e a tela de importação, com e2e contra o AppHost.

**Decisões do usuário (2026-10-18):**
- Filtros salvos e colunas da lista ficam **no servidor, por usuário** (preferências por tela, no backend do 5E1).
- O 5E é dividido em **5E1 e 5E2**.

---

## 2026-10-18 — Claude Code (PR 5D implementado)

**Branch:** `etapa-5d-timeline-importacao` · **Estado:** PR aberto (`gh pr list --head etapa-5d-timeline-importacao`), aguardando o CI e o merge do usuário.

**Feito:**
- **Linha do tempo:** `GET /cases/{id}/timeline`, com o contrato `ITimelineContributor` e a contribuição do Litigation.
- **Histórico de situação:** `case_status_changes`, só com inserções.
- **Lote:** `POST /cases/batch`.
- **Importação:** envio, validação em job, pré-visualização, confirmação, gravação retomável, relatório e modelo. O critério 2 está atendido.
- **Contratos novos:** `IJobTracker`, `ILookupItems`, `IOrganizationDirectory.ListAsync`/`MemberUserIdsAsync` e `IPeopleRegistry`.
- **Pacote:** ClosedXML 0.105.1, com a SixLabors.Fonts presa na 1.0.1. Registrado nas ADRs 0029 e 0002.
- **Testes de API:** `RecordingJobScheduler` e `RunJobsAsync` na fábrica, para executar os jobs.

**Próximo agente:**
1. Confirme com o usuário se o PR do 5D foi mergeado (`gh pr list --head etapa-5d-timeline-importacao --state all`). Não faça o merge você mesmo.
2. Depois do merge, faça a arrumação:
   - `progress.md` e ADR 0029 como aceita;
   - crie o branch `etapa-5e-telas-processos`;
   - atualize o `AGENTS.md` e escreva uma entrada aqui;
   - faça commit e push.
3. Planeje o 5E (frontend e e2e dos processos).

---

## 2026-10-18 — Claude Code (arrumação depois do merge do #27)

**Branch:** `etapa-5d-timeline-importacao`, criado a partir da `main` (3a87168), com este commit de arrumação e push.

**Feito:**
- PR #27 (5C2) mergeado pelo usuário;
- `progress.md`: 5C2 marcado como concluído e a linha de status atualizada;
- ADR 0028 marcada como aceita;
- "Onde paramos" do `AGENTS.md` atualizado.

**Combinado com o usuário:**
- Docs e código no mesmo push sempre que der: o CI cancela o run anterior a cada push novo no PR (`cancel-in-progress`). O "X" de cancelado não é falha de teste.

**Plano do 5D mostrado ao usuário** (aguardando aprovação):
- linha do tempo, com `ITimelineContributor` e `case_status_changes`;
- importação XLSX/CSV: upload, validação em job, pré-visualização, confirmação, gravação em lotes e relatório XLSX;
- operações em lote: responsável, fase e situação, em até 1.000 processos;
- ClosedXML 0.105.1 com a SixLabors.Fonts fixada em 1.0.1 (Apache-2.0); a 2.x tem licença comercial;
- ADR 0029.

**Decisões do usuário sobre a importação (2026-10-18):**
- Cliente ou parte contrária fora do cadastro: cadastrar automaticamente. Se o CPF/CNPJ já existe, usar a pessoa existente.
- Pasta interna: coluna opcional que mantém a numeração legada (única no escritório); vazia, usa a numeração do escritório.
- CNJ que já existe no escritório: a linha vai para o relatório (`litigation.cnj_taken`) e o processo existente não muda.
- Linhas inválidas: importa as válidas depois da confirmação e entrega o relatório das inválidas.

---

## 2026-10-18 — Claude Code (PR 5C2 implementado)

**Branch:** `etapa-5c2-risco-acordos` · **PR:** [#27](https://github.com/iagogoliveira/ProjetoLaw/pull/27), aberto e aguardando o CI e o merge do usuário.

**Feito:**
- **Risco:** `RiskAssessment`, só com inserções, e as rotas `GET /cases/{id}/risk`, `GET /cases/{id}/risk/history` e `POST /cases/{id}/risk-assessments`. Permissão nova `cases.risk.assess`, com migration de dados no Organization para os perfis Administrador e Sócio.
- **Acordos:** `Settlement`, com rodadas, envio dos termos, aprovação, devolução, homologação, recusa, cancelamento e baixa de parcela, mais o indicador pedido × acordado. A homologação publica `SettlementHomologatedIntegrationEvent`.
- **Audiências:** `Hearing`, com agendar, editar, redesignar (com histórico), realizar e cancelar.
- RLS nas 6 tabelas; risco e redesignações sem `UPDATE` nem `DELETE`. Também `CrossTenantCases`, OpenAPI só com adições, o cliente TypeScript, a ADR 0028 e o `progress.md`.
- **Refatoração:** `CaseWrites` (`Litigation.Application/Cases`) concentra as conferências de "processo aberto" usadas por pedidos, risco, acordos e audiências.

**Próximo agente:**
1. Confirme com o usuário se o PR #27 foi mergeado (`gh pr view 27 --json state`). Não faça o merge você mesmo.
2. Depois do merge, faça a arrumação:
   - `progress.md` e ADR 0028 como aceita;
   - crie o branch `etapa-5d-timeline-importacao`;
   - atualize o "Onde paramos" do `AGENTS.md` e escreva uma entrada aqui;
   - faça commit e push.
3. Planeje o **5D** (linha do tempo, importação XLSX/CSV com ClosedXML e operações em lote). O ClosedXML é pacote novo: confira a licença (MIT) e registre uma ADR.

---

## 2026-10-18 — Claude Code (PR 5C2 em andamento)

**Branch:** `etapa-5c2-risco-acordos`. O plano foi aprovado pelo usuário; a implementação está em andamento e **ainda não tem commit**. Se você (Codex) encontrar arquivos novos sem commit neste branch, são deste trabalho: confira com `git status` e continue de onde parou.

**Plano aprovado:**
- **Risco (`RiskAssessment`):**
  - feito para o processo ou para um pedido;
  - guarda a probabilidade (provável, possível ou remoto), o valor de perda estimado, a justificativa, o autor e a data;
  - só recebe inserções, e a última avaliação é a atual;
  - a resposta classifica: provável vira provisão, possível vira passivo contingente, remoto não entra.
- **Acordos:**
  - `Settlement`, com rodadas (`SettlementOffer`: nós ou a outra parte, valor, condições, data);
  - situações: negociação → aguardando aprovação → aprovado → homologado → cumprido, além de recusado ou cancelado;
  - parcelas (`SettlementInstallment`) com baixa manual, cuja soma precisa bater com o valor acordado;
  - indicador de economia: valor pedido × valor acordado;
  - a homologação publica `SettlementHomologatedIntegrationEvent`.
- **Audiências (`Hearing`):**
  - tipo, formato, data e hora, local ou link, advogado, preposto e testemunhas;
  - situação: agendada, realizada, redesignada ou cancelada;
  - resultado ou ata.

**Decisões do usuário (2026-10-18):**
- **Aprovação do acordo:** a negociação é livre. Os termos finais vão para a aprovação interna antes de serem assinados e homologados. Aprova quem tem `cases.settlements.approve`, inclusive o próprio acordo.
- **Risco:** permissão nova, `cases.risk.assess`. Vai para os perfis padrão Administrador e Sócio, também nos escritórios já existentes (migration de dados, como a da `crm.people.anonymize`).
- **Redesignação:** a audiência é a mesma; ela muda de data e guarda as datas anteriores com o motivo.
- **Parcelas:** baixa manual (data e valor pagos); "vencida" é calculada pela data. Na Etapa 10, o financeiro passa a dar a baixa.

---

## 2026-10-18 — Claude Code (arrumação depois do merge do #26)

**Branch:** `etapa-5c2-risco-acordos`, criado a partir da `main` (bb14aec), com este commit de arrumação e push.

**Feito:**
- PR #26 (5C1) mergeado pelo usuário em 2026-10-18;
- `progress.md`: 5C1 marcado como concluído e a linha de status da Etapa 5 atualizada;
- ADR 0027 marcada como aceita;
- "Onde paramos" do `AGENTS.md` atualizado.

**Próximo:**
- Planejar o PR 5C2 (risco, acordos e audiências) e mostrar o plano ao usuário antes de implementar. O escopo está no `AGENTS.md`.
- Se o Claude Code estiver no meio desse planejamento, a próxima entrada aqui dirá.

---

## 2026-10-18 — Claude Code

**Branch:** `etapa-5c-valores-risco` · **PR:** [#26](https://github.com/iagogoliveira/ProjetoLaw/pull/26), aberto e aguardando o CI e o merge do usuário.

**Feito:**
- PR 5C1:
  - índices monetários (`platform.monetary_indexes`) com seed real de 01/2000 a 09/2026 e importação diária;
  - `IMonetaryCorrection`;
  - pedidos do processo, com valores por instância e histórico;
  - ADR 0027;
  - critério 3 da Etapa 5 atendido, com os 10 casos calculados à mão, documentados na ADR 0027.
- Verificação completa feita; os detalhes estão na seção do PR 5C1 no `progress.md`.

**Decisões do usuário nesta sessão (2026-10-18):**
- **SELIC:** não leva juros extras e se acumula por soma simples.
- **Juros:** simples, pro rata dia, sobre o valor corrigido.
- **Deflação:** entra no cálculo, mas o valor nunca fica abaixo do original.
- **Acordos (5C2):** quem tem `cases.settlements.approve` aprova, inclusive o acordo que a própria pessoa cadastrou.
- **5C dividido:** 5C1 (este) e 5C2 (risco, acordos, audiências).
- **Documentação de agentes:** sempre atualizar o `AGENTS.md` e este arquivo ao terminar um trabalho.

**Próximo agente, faça nesta ordem:**
1. Confirme com o usuário se o PR #26 foi mergeado (`gh pr view 26 --json state`). Não faça o merge você mesmo.
2. Depois do merge, faça a arrumação pós-merge:
   - atualize a `main`;
   - crie o branch `etapa-5c2-risco-acordos`;
   - no `progress.md`, marque o 5C1 como concluído e mude a linha de status da etapa;
   - marque a ADR 0027 como aceita;
   - atualize o "Onde paramos" do `AGENTS.md` e escreva uma entrada aqui;
   - faça commit e push.
3. Planeje o **PR 5C2** e mostre o plano ao usuário antes de implementar:
   - **risco:** `RiskAssessment` por processo e por pedido (provável, possível ou remoto, CPC 25), com histórico;
   - **acordos:** `Settlement` (proposta, contraproposta, aprovação, homologação) e `SettlementInstallment`, com o indicador de economia (pedido × acordado). A homologação publica `SettlementHomologated`, para o financeiro da Etapa 10;
   - **audiências:** `Hearing` (presencial, virtual ou híbrida; local ou link; preposto, advogado e testemunhas; resultado ou ata).

**Atenção:**
- A API do BCB (`api.bcb.gov.br`) recusa as requisições desta máquina. Para dados da SELIC e da TR, use o Ipeadata.
- A Calculadora do Cidadão (`www3.bcb.gov.br/CALCIDADAO`) funciona e serve para conferir cálculos.
