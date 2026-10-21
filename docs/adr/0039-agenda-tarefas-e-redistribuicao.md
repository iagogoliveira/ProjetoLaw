# ADR 0039 — Agenda, tarefas e redistribuição em lote

- **Status:** Proposta (para revisão no PR #38)
- **Data:** 2026-10-20
- **Etapa:** 7 (7C; RF-24)

## Contexto

O 6D criou tarefas pendentes somente pela triagem, vinculadas a publicação e processo. O 7B entregou o ciclo dos prazos. O plano 7C aprovado prevê tarefas completas, compromissos, agenda unificada e redistribuição, com as telas no 7F.

## Decisões do usuário

Em 2026-10-20, o usuário confirmou:

1. Tarefas e compromissos podem existir sem processo, para atividades administrativas.
2. Concluir uma tarefa exige resolver o checklist e as subtarefas.
3. Tarefas usam Pendente, Em andamento, Concluída e Cancelada; prioridades Baixa, Normal, Alta e Urgente. Tarefas concluídas ou canceladas podem ser reabertas.
4. Subtarefas têm responsável, vencimento, prioridade e checklist próprios, com apenas um nível. Subtarefas concluídas ou canceladas liberam a conclusão da tarefa principal.

## Decisão técnica

- `TaskItem` conserva IDs, origem, datas e confirmação da triagem. Processo e publicação passam a ser opcionais no novo cadastro. O estado persistido usa `TaskState`, mantendo a representação `Pending` legada; prioridade inicial `Normal`. `TaskChecklistItem` e `TaskComment` são filhos com ID do domínio, tenant, vínculo de processo e auditoria. Descrições e comentários sensíveis não são duplicados nos changes da auditoria.
- Subtarefas são tarefas completas com `ParentTaskId` imutável. Compartilham o vínculo de processo do pai. Somente um pai aberto e de primeiro nível aceita subtarefas; criação exige autorização no pai e no destino. Uma tarefa principal fechada impede editar ou reabrir suas subtarefas até sua própria reabertura. Cancelar o pai não altera silenciosamente os filhos. Reabrir volta a Pendente e remove os campos de fechamento, mantendo a auditoria anterior.
- O cadastro exige responsável ativo e com leitura do destino; quando há processo, também exige processo aberto e alcance ao processo. Editar, comentar ou alterar checklist exige `schedule.tasks.manage`; as mutações são bloqueadas em estados fechados. A lista legada por processo preserva `status: pending` e recebe `lifecycleStatus`, conforme a estratégia aditiva do 7B.
- Compromissos têm responsável único, título, descrição, local, início/fim e estado agendado/cancelado. Instantes são normalizados para UTC. O fim precisa ser posterior ao início; compromissos simultâneos são permitidos. Cancelamento preserva o registro e impede novas alterações. Processo e responsável permanecem protegidos pela autorização do destino; o vínculo de processo não é trocado na edição.
- Itens com processo exigem a permissão do tipo e `cases.read`, com alcance e sigilo verificados por Litigation. Itens administrativos usam os escopos próprio (responsável), equipe (membros atuais das equipes concedidas) e escritório; escopos de área/filial não ampliam acesso a itens sem processo e sem esses vínculos. Filtrar por pessoa/equipe restringe o resultado, sem conceder acesso. Atribuir alguém não lhe concede permissões.
- A consulta unificada usa `schedule.events.read` como acesso à agenda e confere a leitura de cada fonte: prazos, tarefas, compromissos e audiências. Audiências continuam pertencendo a Litigation, por `IHearingCalendar`, que aplica `cases.read` e devolve projeção mínima, sem notas, testemunhas, resultado ou histórico. Não há consulta ao DbContext de outro módulo.
- O intervalo da agenda usa datas civis de São Paulo, início inclusivo e fim exclusivo, até 366 dias. Prazos aparecem na data fatal, com a data interna no item; tarefas aparecem no vencimento, ambos como dia inteiro. Compromissos sobrepostos ao intervalo entram mesmo quando iniciam antes dele. Fechados são excluídos por padrão. Paginação por cursor ordena instante, tipo e ID e limita cada fonte antes da composição; o cursor não concede acesso. A lista de tarefas também tem paginação por ID e filtros.
- Redistribuição recebe de 1 a 1000 IDs distintos e um responsável. Cada item é autorizado e validado; o retorno registra sucesso ou código de erro individual, sem conteúdo de recursos ocultos. Itens terminais não são redistribuídos, e o destino precisa ter acesso de leitura ao processo e ao tipo. Prazos preservam datas, memória e conferência, com atividade de redistribuição na timeline. Uma falha inesperada desfaz a transação; falhas individuais previstas permitem persistir os válidos.
- O mesmo advisory lock transacional por escritório do Scheduling serializa cadastro, edição, transições, dependências e redistribuição; `xmin` continua ativo. A migration aditiva `AgendaAndTasks` habilita RLS nas três novas tabelas e preserva tarefas legadas. Migrations anteriores não são alteradas.
- Erros de vínculo de tarefa/compromisso a processo oculto usam o mesmo código do recurso inexistente, preservando a auditoria de acesso negado feita por Litigation. Para itens administrativos, a leitura do destinatário precisa alcançar a própria atribuição, não apenas existir em algum escopo. Requisições HTTP malformadas (como uma data inválida na query) mantêm o status 400 em ProblemDetails, com código estável `http.bad_request` e sem detalhes internos, em vez de virarem erro 500. Exceções inesperadas continuam 500 ocultas.
- Sem novo pacote. Anexos dependem do GED da Etapa 9; notificações/preferências adicionais ficam no 7D, telas no 7F e sincronização externa no 7G.

## Verificação

Verificação integral aprovada em 2026-10-21: builds Release/Debug sem warnings/erros; format; 1.124 testes .NET em 20 projetos; gate de 80% (Scheduling.Domain 98,8%, Application 99,2%); dependências, seis modelos EF e oasdiff v1.33.0 sem quebra; audit zero, lint, 134 Vitest, build Angular e duas rodadas consecutivas de 17 Playwright contra o AppHost com acessibilidade. Somente a preparação opcional de carga ignorada em cada rodada. Migração 7B→7C, RLS, auditoria, escopo/sigilo, dependências de subtarefas, reabertura, cursor entre quatro fontes e redistribuição parcial verificados. Ambiente encerrado e volumes preservados. Detalhes e evidências no `docs/progress.md`. Commit local, sem push/PR da implementação.
