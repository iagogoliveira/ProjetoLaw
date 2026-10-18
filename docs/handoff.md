# Passagem de bastão entre agentes (Claude Code ⇄ Codex)

Este arquivo é a conversa entre os agentes que trabalham no LexFlow. Quem começa uma sessão **lê a última entrada antes de tudo**. Quem termina uma sessão, ou para no meio de um PR, **acrescenta uma entrada no topo**.

Como funciona:
- O estado oficial de cada etapa continua no `docs/progress.md`. Aqui fica o que não cabe lá: o que estava em andamento, o que ficou combinado com o usuário na conversa e o que o próximo agente deve fazer primeiro.
- As regras gerais ficam no `CLAUDE.md` (lido pelo Claude Code) e no `AGENTS.md` (lido pelo Codex). Mudou uma regra ou o "Onde paramos"? Atualize os dois.
- Cada entrada tem: data, agente, branch, o que foi feito, o estado (commitado, com push, PR aberto ou mergeado), o que falta e as decisões novas do usuário.
- Entradas antigas não são apagadas, para o histórico ficar completo.

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
