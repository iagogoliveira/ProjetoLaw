# ADR 0031 — Telas de valores, acompanhamento e importação de processos

- **Status:** Proposta (para revisão no PR 5E2)
- **Data:** 2026-10-19
- **Etapa:** 5 (PR 5E2)

## Contexto

O plano aprovado em 2026-10-19 completa as telas da Etapa 5: pedidos e valores (RF-13), risco básico (RF-14), acordos (RF-16), audiências e importação (RF-09). Os contratos da API e as permissões foram entregues nos PRs 5C1, 5C2 e 5D.

## Decisão

1. Cada aba do detalhe tem um componente standalone em `features/cases/pages/tabs`, com OnPush, formulário reativo tipado e uma SignalStore em `data/`. O conteúdo é carregado ao abrir a aba (`matTabContent`). Todas as chamadas passam pelo `CasesApi`, sobre o cliente gerado; não há pacote novo nem alteração de contrato.
2. Pedidos: cadastro, edição e exclusão com confirmação; valores nas quatro instâncias; correção até uma data (hoje por padrão), totais e erro por pedido; histórico com autor, data e valores anteriores e novos. SELIC não permite juros adicionais, conforme a ADR 0027.
3. Risco: avaliações atuais, provisão e passivo contingente calculados pelo servidor; avaliação por processo ou pedido, histórico e formulário exclusivo de `cases.risk.assess`. A composição dos totais continua a da ADR 0028.
4. Acordos: rodadas, termos finais, aprovação com permissão própria, devolução, homologação, recusa, cancelamento e baixa manual. As ações seguem a situação retornada pelo servidor. O indicador identifica economia quando o cliente paga e deságio quando recebe.
   - O gerador divide o valor em centavos inteiros e ajusta a última parcela para preservar a soma.
   - Os vencimentos usam o intervalo mensal informado, ancorados no dia do primeiro vencimento e limitados ao último dia de cada mês. Exemplo: 31/01, 28/02 e 31/03.
   - A prévia mostra todas as parcelas antes do envio. O servidor continua validando o total.
5. Audiências: datas no fuso `America/Sao_Paulo`, formato com local e/ou link, advogado das opções do escritório, preposto e busca de pessoas para as testemunhas. A edição não muda a data; a redesignação guarda o histórico. Audiências realizadas ou canceladas não têm ações de edição.
6. Importação em `/processos/importar`, protegida por `cases.create`: modelo, envio multipart, consulta periódica durante validação e gravação, prévia, confirmação das válidas, totais e relatório XLSX em Blob. O identificador fica na query string para retomar após recarregar. A consulta para quando o componente é destruído, quando há erro de rede ou quando o processamento deixa de estar em andamento; erros de rede permitem consulta manual.
7. O e2e revelou que `CaseImportJobs.ImportAsync` consultava o total de inválidas antes de salvar os erros da revalidação. A contagem passa a considerar os erros em memória e, ao retomar, os erros já persistidos, contando uma vez cada linha. O teste de integração das 10 mil linhas também confere os totais finais (10.000 / 9.700 / 300).
8. Textos em pt-BR e en com as mesmas chaves; ações protegidas por `*lfCan`; erros estáveis e de validação traduzidos. Playwright mantém relatório HTML local e no CI, screenshot e trace nas falhas, inclusive na primeira execução.

## Consequências

- As telas usam as regras de cálculo, autorização, aprovação e importação já registradas nas ADRs 0027 a 0029; não há nova decisão de negócio.
- Vitest cobre as cinco stores, respostas atrasadas, erros, interrupção da consulta periódica e o gerador de parcelas. Os e2e percorrem pedidos, risco, acordo até a baixa, audiência redesignada e realizada, importação parcial com relatório e WCAG 2.1 AA.
- A correção da contagem não exige migration nem regeneração do OpenAPI. Segurança e isolamento permanecem nos endpoints existentes.
- O 5E2 encerra o escopo da Etapa 5; a conclusão oficial da etapa e a aceitação desta ADR aguardam o merge pelo usuário. Agenda, financeiro, fechamento contábil e cálculos completos continuam nas etapas previstas.
