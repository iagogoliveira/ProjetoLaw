# ADR 0027 — Índices monetários, correção e pedidos do processo

- **Status:** Proposta (para revisão no PR 5C1)
- **Data:** 2026-10-18
- **Etapa:** 5 (PR 5C1)

## Contexto

O RF-13 pede os pedidos do processo, com valor pedido, estimado e de condenação por instância, a atualização monetária e os juros por índices oficiais (IPCA-E, SELIC, INPC, TR) mantidos pela plataforma, e o histórico das alterações de valor. O critério 3 da Etapa 5 pede que a correção monetária confira com 10 casos calculados à mão.

Decisões de negócio confirmadas em 2026-10-18:
- os índices vêm das APIs públicas do BCB e do IBGE, com fonte falsa em dev e nos testes;
- o cálculo é a correção pelo índice mais juros simples em % ao mês, a partir de uma data;
- **com a SELIC não há juros extras**, porque ela já os embute (STF, ADC 58);
- **a SELIC se acumula por soma simples** das taxas mensais, como na Receita Federal e no Manual de Cálculos da Justiça Federal;
- **os juros são pro rata dia** (meses completos mais os dias restantes / 30) e **incidem sobre o valor corrigido**;
- **a deflação entra, mas o valor corrigido nunca fica abaixo do original** (entendimento do STJ);
- a regra da Lei 14.905/2024 fica para os cálculos completos.

## Decisão

1. **Tabela `platform.monetary_indexes`:**
   - colunas: índice, mês (dia 1), variação em % (`numeric(9,4)`), origem e data da importação;
   - é dado de referência, igual para todos os escritórios: sem tenant, sem RLS e fora da trilha de auditoria;
   - IPCA-E é a variação mensal do IPCA-15, a série que os tribunais e a Calculadora do Cidadão (série SGS 10764) usam como IPCA-E.
2. **Seed:**
   - de janeiro de 2000 a setembro de 2026 (o IPCA-E começa em maio de 2000), com 1.280 meses, numa migration de dados;
   - IPCA-E e INPC vêm do IBGE (SIDRA, tabelas 3065 e 1736);
   - SELIC e TR vêm do Ipeadata (`BM12_TJOVER12` e `BM12_TJTR12`), que republica as séries do BCB. A API do BCB recusou as requisições a partir da máquina de desenvolvimento ("A sua requisição foi rejeitada"), tanto pelo curl quanto pelo navegador.
3. **Importação** (`MonetaryIndexImporter`, job Hangfire `platform-monetary-indexes-import`):
   - roda todo dia às 06:00 e grava só os meses que faltam, do seguinte ao último da tabela até o anterior ao mês corrente, porque o mês corrente ainda não fechou;
   - é idempotente: enquanto a fonte não publica o mês, nada muda;
   - fontes, tentadas na ordem:
     - IBGE SIDRA para IPCA-E e INPC;
     - BCB SGS (séries 4390 e 7811) para SELIC e TR, com o Ipeadata de reserva quando o BCB falha (rede, status de erro ou a página HTML de recusa);
   - em dev (AppHost) e nos testes, `Platform:MonetaryIndexes:Source = Fake`;
   - mês sem fonte disponível fica para a rodada seguinte, com log de erro.
4. **Correção** (`IMonetaryCorrection`, Platform.Contracts, com a regra em `MonetaryCorrectionCalculator`, no Domain):
   - **meses do cálculo:** a variação de cada mês é aplicada do mês de início até o anterior ao mês do cálculo, então um ano de atraso dá 12 variações. A Calculadora do Cidadão inclui o mês final: "01/2020 a 12/2020" lá equivale aqui a corrigir de 01/2020 até uma data de 01/2021;
   - **acumulação:** IPCA-E, INPC e TR são compostos; a SELIC é a soma simples;
   - **juros simples pro rata dia** sobre o valor corrigido. Na conta dos meses completos, o dia é ajustado ao fim do mês: de 31/01 a 28/02 conta um mês;
   - **arredondamento comercial** (meio para cima) só nos valores em reais, com 2 casas. O fator não é arredondado durante a conta e sai com 9 casas;
   - **mês sem índice publicado** é erro (`platform.monetary_index_missing`), nunca estimativa;
   - **em lote:** `CalculateManyAsync` lê a tabela uma vez por índice.
5. **Endpoints, com `cases.read`:**
   - `GET /api/v1/monetary-indexes?index=&from=&to=`;
   - `POST /api/v1/monetary-corrections`, a calculadora, que devolve a memória de cálculo mês a mês.
6. **Pedidos** (Litigation: `claims`, `claim_valuations` e `claim_value_changes`, todas com RLS e `ICaseScoped`):
   - o objeto vem da tabela auxiliar "Objetos/pedidos", com descrição e a forma de correção (índice, início da correção, início e taxa dos juros);
   - há um registro de valores por instância: pedido, estimado e condenação;
   - **histórico:** cada valor alterado, da criação em diante, grava o anterior, o novo, quem mudou e quando. A tabela só recebe inserções: a migration revoga da aplicação o `UPDATE`, o `DELETE` e o `TRUNCATE`;
   - **rotas:**
     - `GET /api/v1/cases/{id}/claims?updateTo=` traz os valores atualizados e os totais por instância. Os totais atualizados contam pelo valor original os pedidos sem correção. Se um pedido não pode ser atualizado (ex.: mês não publicado), ele traz `correctionError` em vez de falhar a lista;
     - `POST /api/v1/cases/{id}/claims`;
     - `PUT` e `DELETE /api/v1/cases/{id}/claims/{claimId}`. A exclusão é lógica e o histórico fica;
     - `GET /api/v1/cases/{id}/claims/{claimId}/history`;
   - **permissões:** leitura com `cases.read` e alteração com `cases.edit`, sempre pelo alcance do processo (`CaseAccess`). Processo encerrado não aceita alteração.

## Os 10 casos do critério 3

O cálculo à mão usa aritmética decimal exata sobre os valores do seed. Os casos de IPCA-E e INPC foram conferidos na Calculadora do Cidadão do BCB em 2026-10-18: o fator coincide até a 7ª casa, que é a precisão que ela mostra, e o valor corrigido é o mesmo. Teste: `MonetaryIndexTests.Correction_matches_the_hand_calculated_cases`.

| Caso | Índice | Valor (R$) | Correção | Meses | Juros | Fator | Corrigido (R$) | Juros (R$) | Total (R$) | Calculadora do Cidadão |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| C1 | INPC | 10.000,00 | 15/01/2020 a 15/01/2021 | 01/2020 a 12/2020 (12) | — | 1,054473253 | 10.544,73 | — | 10.544,73 | 1,05447330 · R$ 10.544,73 |
| C2 | IPCA-E | 5.000,00 | 10/03/2015 a 10/03/2016 | 03/2015 a 02/2016 (12) | — | 1,108377834 | 5.541,89 | — | 5.541,89 | 1,10837780 · R$ 5.541,89 |
| C3 | INPC | 1.000,00 | 01/07/2026 a 01/09/2026 | 07 e 08/2026 (−0,01% e −0,32%) | — | 0,996700320 | **1.000,00** (piso) | — | 1.000,00 | 0,99670030 · R$ 996,70 (sem o piso) |
| C4 | INPC | 2.500,00 | 01/06/2026 a 01/10/2026 | 06 a 09/2026 (4, com deflação no meio) | — | 1,006280085 | 2.515,70 | — | 2.515,70 | 1,00628010 · R$ 2.515,70 |
| C5 | IPCA-E | 20.000,00 | 10/01/2024 a 20/03/2025 | 01/2024 a 02/2025 (14) | 1% a.m. desde 05/02/2024: 13 meses e 15 dias = 13,5% | 1,061111380 | 21.222,23 | 2.865,00 | 24.087,23 | 1,06111140 · R$ 21.222,23 |
| C6 | INPC | 1.000,00 | 01/01/2000 a 01/10/2026 | 01/2000 a 09/2026 (321) | — | 4,956878629 | 4.956,88 | — | 4.956,88 | 4,95687860 · R$ 4.956,88 |
| C7 | SELIC | 50.000,00 | 01/01/2023 a 01/01/2024 | 01 a 12/2023: soma de 12,31% | — (a SELIC já inclui) | 1,123100000 | 56.155,00 | — | 56.155,00 | não se aplica (ela compõe a SELIC diária) |
| C8 | TR | 100.000,00 | 01/01/2023 a 01/01/2024 | 01 a 12/2023 (12) | 0,5% a.m. desde 31/01/2023: 11 meses e 1 dia = 5,516667% | 1,017600161 | 101.760,02 | 5.613,76 | 107.373,78 | não se aplica (TR por dia) |
| C9 | IPCA-E | 3.000,00 | 05/09/2026 a 28/09/2026 | nenhum (mesmo mês) | 1% a.m.: 23 dias = 0,766667% | 1,000000000 | 3.000,00 | 23,00 | 3.023,00 | — |
| C10 | IPCA-E | 7.531,29 | 30/06/2019 a 30/06/2024 | 06/2019 a 05/2024 (60) | 1% a.m. desde 31/08/2019: 58 meses (31/08 + 58 meses = 30/06) | 1,324273098 | 9.973,48 | 5.784,62 | 15.758,10 | 1,32427310 · R$ 9.973,48 |

Como cada valor foi obtido:
- **corrigido** = arredonda(valor × fator), com o fator igual ao produto de (1 + variação/100) dos meses, ou 1 + soma/100 na SELIC;
- **juros** = arredonda(corrigido × taxa × meses de juros / 100);
- **total** = corrigido + juros.

Exemplo, C5: 21.222,23 × 1% × 13,5 = 2.865,00105, que arredonda para 2.865,00.

## Consequências

- **Critério 3 da Etapa 5 atendido:** 10 casos conferidos à mão; os 6 de IPCA-E e INPC também batem com a Calculadora do Cidadão, salvo o piso da deflação, que é decisão nossa.
- **O adaptador do BCB não foi exercitado contra a API real**, que recusa esta máquina; está coberto por respostas no formato documentado. Em produção, se o BCB também recusar, o Ipeadata assume. Vale conferir o log da primeira importação em produção.
- **Índices revisados:** a tabela não é corrigida sozinha. Se o IBGE ou o BCB revisarem um mês já gravado, a correção entra por uma migration de dados.
- **Para depois:** os cálculos completos (regra da Lei 14.905/2024, juros compostos, multas, honorários) e os índices de outras áreas (IGP-M para contratos) ficam para a etapa dos cálculos.
