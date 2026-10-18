# ADR 0028 — Risco, acordos e audiências do processo

- **Status:** Proposta (para revisão no PR 5C2)
- **Data:** 2026-10-18
- **Etapa:** 5 (PR 5C2)

## Contexto

A Etapa 5 pede, além dos pedidos do 5C1:
- a classificação de risco básica (RF-14): provável, possível ou remoto, por processo e por pedido;
- a estrutura dos acordos (RF-16): proposta, contraproposta, aprovação, homologação e parcelas, com o indicador de economia;
- as audiências (RF-12, só a parte do processo; a agenda é da Etapa 7).

Decisões de negócio confirmadas em 2026-10-18:
- a negociação do acordo é livre; os **termos finais vão à aprovação interna antes de serem assinados e homologados**;
- aprova quem tem `cases.settlements.approve`, **inclusive o acordo que a própria pessoa enviou**;
- avaliar risco pede uma **permissão própria, `cases.risk.assess`**;
- a **redesignação mantém a mesma audiência** e guarda as datas anteriores com o motivo;
- as parcelas recebem **baixa manual** até o financeiro da Etapa 10.

## Decisão

1. **Risco (`litigation.risk_assessments`):**
   - cada avaliação é um registro novo, do processo inteiro ou de um pedido, com a probabilidade, o valor de perda estimado, a justificativa, o autor e a data;
   - a tabela só recebe inserções: a aplicação perde o `UPDATE`, o `DELETE` e o `TRUNCATE`;
   - a mais recente de cada assunto é a atual;
   - classificação contábil (CPC 25): provável vira provisão, possível vira passivo contingente (só divulgado), remoto não entra;
   - **totais do processo:** somam as avaliações atuais dos pedidos; sem nenhuma, vale a do processo, para não contar o mesmo risco duas vezes. Pedido excluído sai do risco atual, mas fica no histórico;
   - `GET /cases/{id}/risk`, `GET /cases/{id}/risk/history` e `POST /cases/{id}/risk-assessments`;
   - **permissão `cases.risk.assess`:** entra no catálogo e nos perfis padrão Administrador e Sócio. Os escritórios já existentes a recebem por migration de dados no Organization; os perfis criados por eles não mudam.
2. **Acordos (`settlements`, `settlement_offers` e `settlement_installments`):**
   - **situações:** negociação → aguardando aprovação → aprovado → homologado → cumprido, além de recusado (sem acordo) e cancelado;
   - **direção:** o cliente recebe (contas a receber) ou paga (contas a pagar);
   - **rodadas numeradas** (nós ou a outra parte, valor, condições, data), só durante a negociação;
   - **termos enviados à aprovação:** o valor e de 1 a 360 parcelas, cuja soma precisa ser o valor. Devolver à negociação (com o motivo) limpa a aprovação, e um novo envio troca as parcelas;
   - **baixa de parcela** só depois da homologação. Com todas pagas, o acordo está cumprido. A parcela "vencida" é calculada pela data (dia de São Paulo);
   - **indicador:** o valor pedido (soma dos pedidos, na primeira instância com valor de cada um; sem pedidos, o valor da causa) menos o valor acordado (antes dele, a última proposta). Quando o cliente paga, a diferença é a economia; quando recebe, é o deságio;
   - **evento:** a homologação publica `SettlementHomologatedIntegrationEvent` (Litigation.Contracts), com a direção e as parcelas, na mesma transação (outbox), para o financeiro da Etapa 10;
   - **processo encerrado:** não aceita acordo novo, rodada nem envio. Aprovar, homologar, devolver, encerrar e dar baixa seguem permitidos, porque o acordo costuma encerrar o processo.
3. **Audiências (`hearings` e `hearing_reschedules`):**
   - **dados:** tipo (conciliação, instrução, una, julgamento ou outra), formato, data e hora em UTC, advogado designado (membro do escritório), preposto, testemunhas (pessoas do Crm, em `uuid[]`) e observações;
   - **local e link:** o formato pede o local (presencial), o link http ou https (virtual) ou os dois (híbrido);
   - **situação:** agendada, realizada (com o resultado ou a ata) ou cancelada (com o motivo). Depois de realizada ou cancelada, não muda mais;
   - **redesignação:** troca a data e grava a anterior, a nova, o motivo, quem e quando. A tabela das redesignações só recebe inserções. A edição comum não muda a data;
   - a agenda, o checklist e os correspondentes ficam para a Etapa 7.
4. **Em todas as partes:**
   - RLS e `ICaseScoped` nas seis tabelas;
   - o alcance do processo (`CaseAccess`) em todas as rotas, com 404 fora dele;
   - leitura com `cases.read` e alteração com `cases.edit`; avaliar risco e aprovar acordo pedem as permissões próprias, também conferidas no escopo.

## Consequências

- **Etapa 5:** risco, acordos e audiências ficam disponíveis para as telas do 5E.
- **Pendências desta etapa:** o critério 2 (importação) e a linha do tempo seguem para o 5D.
- **Para depois:**
  - provisão contábil com fechamento mensal e trava, revisão periódica e exportação ao ERP: com o financeiro e o BI;
  - alçada de aprovação por valor: se o escritório pedir;
  - contas a receber e a pagar a partir das parcelas: Etapa 10, consumindo o evento.
- **Testemunhas em `uuid[]`, sem FK para o Crm**, que é outro módulo. A existência é conferida no cadastro pelo `IPeopleDirectory`. Uma pessoa excluída depois continua na lista, e a tela a mostra como removida.
