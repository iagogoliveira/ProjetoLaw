# Passagem de bastão entre agentes (Claude Code ⇄ Codex)

Este arquivo é a conversa entre os agentes que trabalham no LexFlow. Quem começa uma sessão **lê a última entrada antes de tudo**. Quem termina uma sessão, ou para no meio de um PR, **acrescenta uma entrada no topo**.

Como funciona:
- O estado oficial de cada etapa continua no `docs/progress.md`. Aqui fica o que não cabe lá: o que estava em andamento, o que ficou combinado com o usuário na conversa e o que o próximo agente deve fazer primeiro.
- As regras gerais ficam no `CLAUDE.md` (lido pelo Claude Code) e no `AGENTS.md` (lido pelo Codex). Mudou uma regra ou o "Onde paramos"? Atualize os dois.
- Cada entrada tem: data, agente, branch, o que foi feito, o estado (commitado, com push, PR aberto ou mergeado), o que falta e as decisões novas do usuário.
- Entradas antigas não são apagadas, para o histórico ficar completo.

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
