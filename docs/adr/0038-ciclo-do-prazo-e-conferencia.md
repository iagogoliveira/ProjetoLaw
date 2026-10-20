# ADR 0038 — Ciclo do prazo, conferência e alertas do calendário

- **Status:** Proposta (para revisão no PR 7B)
- **Data:** 2026-10-20
- **Etapa:** 7 (7B; RF-23)

## Contexto

O 6D antecipou prazos pendentes criados pela triagem, e o 7A entregou a calculadora e o calendário. O plano 7A–7G aprovado em 2026-10-20 prevê cadastro manual, cumprimento com evidência, perda com justificativa, conferência opcional e alertas sem alterar prazos confirmados.

**Decisão adicional do usuário nesta sessão:** quando a conferência é obrigatória, bloquear apenas o cumprimento e permitir iniciar o trabalho. As demais decisões aprovadas continuam valendo: conferente diferente do autor, salvo escritório com um único membro ativo; protocolo textual até o GED da Etapa 9; aviso ao responsável, ao conferente e a quem tenha `schedule.deadlines.verify` com acesso ao processo.

## Decisão

1. `Deadline` evolui aditivamente. Mantém os identificadores, datas e confirmações legadas e aceita origem sem publicação no cadastro manual. `WorkItemStatus` acrescenta `InProgress`, `Completed` e `Missed`; tarefas continuam somente no núcleo pendente até o 7C. Nenhum prazo legado é recalculado ou marcado como perdido pela migration.
2. O cadastro manual informa tipo textual, responsável e regra do cálculo. O servidor calcula novamente e exige confirmação humana das datas interna e fatal atuais. Divergência entre prévia e gravação retorna `scheduling.calculation_changed` (409), para nova conferência. A regra, o local utilizado e a memória ficam guardados como snapshot. A consulta de detalhe tem DTO próprio; na lista antiga, a ausência de publicação é representada por `Guid.Empty`, preservando o contrato não anulável já entregue.
3. A configuração `Organization.RequireDeadlineReview`, desabilitada por padrão, é acessada por contrato e gerida com `settings.organization.manage`. O prazo guarda a exigência vigente no cadastro, inclusive quando vem da triagem. A configuração não reabre nem reescreve prazos anteriores. A conferência exige `schedule.deadlines.verify` e autorização no processo; a exceção solo reutiliza `IOrganizationMembers.CountActiveAsync` do fluxo de propostas. Iniciar continua permitido antes da revisão; cumprir exige revisão quando registrada como obrigatória.
4. Cumprir exige protocolo textual e perder exige justificativa (até 4000 caracteres). A perda é uma decisão explícita; ultrapassar a data, sozinho, não inventa uma justificativa nem muda o status. Cumprido e perdido são terminais neste PR. As transições registram autor e instante UTC e não alteram as datas confirmadas. Protocolos, justificativas e snapshots são sensíveis e não são duplicados nos changes de auditoria ou na timeline.
5. Cada transição gera `DeadlineActivity`, dado de escritório e de processo, com RLS e relacionamento com o prazo. Atividades são registradas na mesma transação das mudanças. Um advisory lock transacional por escritório serializa cadastro, transições e alterações do calendário; utiliza SQL parametrizado do EF. O `xmin` existente continua como defesa adicional. Repetições de operações terminais retornam conflito sem novo histórico ou evento.
6. O CRUD de dias sem expediente compara os dias efetivos antes e depois, com alcance, intervalo e recorrência. Alterações relevantes geram atividade e alerta para prazos confirmados ainda abertos e com vencimento atual/futuro; alterações apenas editoriais ou fora do local/período não geram alerta. A data confirmada e o snapshot permanecem intactos; novas sugestões consultam o calendário atualizado. Prazos novos usam o local preservado e os legados, o local disponível do processo. O calendário da plataforma permanece versionado por migrations conforme a ADR 0037; não há interface de edição desses seeds neste PR.
7. Alertas de perda e calendário seguem pelo outbox para o Platform, cujo consumidor idempotente persiste a notificação e confere o recurso pelo contrato de Scheduling. A política revalida membro ativo, MFA, leitura de prazos e processos, escopo e sigilo, além do vínculo de responsável/conferente ou alcance de `verify`. A autorização também vale para recibos; receber um alerta não concede acesso. O contador `lexflow_deadlines_missed_total` é incrementado após a persistência de uma nova notificação de perda, sem incrementar na reentrega já processada. O meter `LexFlow.Scheduling` entra no OpenTelemetry.
8. Scheduling contribui à timeline somente se o usuário também alcança a leitura dos prazos. Os novos códigos têm traduções pt-BR/en. O feed recebe o `caseId` opcional de forma aditiva para apontar o alerta ao processo. As telas completas de prazo permanecem no 7E; as preferências e os demais canais de notificação, no 7D.
9. Duas migrations novas: `DeadlineLifecycle` (Scheduling) e `RequireDeadlineReview` (Organization). `deadline_activities` recebe `EnableTenantIsolation`/`DisableTenantIsolation`. Migrations aplicadas permanecem intactas. Não há pacote novo.

## Verificação

Domínio, configuração por tenant, endpoints, permissões e sigilo; confirmação de datas e calendário; conferência solo e independente; concorrência e operações terminais; notificações, duplicidade, recibos e revogação de acesso; métrica pelo método público descoberto pelo Wolverine; timeline; RLS e migrations, incluindo upgrade 7A→7B com dados preservados.

Verificação integral aprovada em 2026-10-20: builds sem warnings, formatação, 1.074 testes .NET, gate de cobertura de 80%, dependências, seis modelos EF, OpenAPI/cliente, lint, 134 Vitest, build Angular e duas rodadas de 17 Playwright contra o AppHost com acessibilidade. A única preparação opcional de carga foi ignorada em cada rodada, sem `LOAD_OUTPUT`. Ambiente encerrado e volumes preservados. Evidências e limitações no `docs/progress.md`.
