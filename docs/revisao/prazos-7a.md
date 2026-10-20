# Revisão jurídica: casos de prazo e calendário (PR 7A)

Este documento serve para a revisão por advogado prevista na decisão de 2026-10-20 (critério 1 da Etapa 7). Os casos estão em `tests/LexFlow.Scheduling.UnitTests/DeadlineCases.cs` e passam no código. O critério só é marcado depois que esta revisão for registrada. Fundamentos: [ADR 0037](../adr/0037-calendario-forense-e-calculadora-de-prazos.md).

## Como conferir

Para cada caso, confirme o primeiro dia contado e o vencimento. Marque **ok** na última coluna, ou escreva a data correta e o motivo.

Regras aplicadas:
- **Publicação no diário e fato do art. 231:** o prazo começa no dia útil seguinte ao fato.
- **DJEN:** a publicação é o primeiro dia útil após a disponibilização.
- **Portal:** a intimação ocorre na consulta ou, sem consulta, no 10º dia corrido após o envio, passando ao dia útil seguinte se esse dia não tiver expediente.
- **Recesso:** de 20/12 a 20/01 (CPC, art. 220), os prazos ficam suspensos.
- **Dias corridos:** começam em dia útil e são prorrogados se vencerem em dia sem expediente.

Carnaval e Corpus Christi não são feriados nacionais. Só contam como feriado onde há lei estadual ou na Justiça Federal.

## Casos (72)

| Caso | Situação | Início | Regra | Calendário | 1º dia contado | Vencimento | Data interna | Revisão |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| P01 | 15 dias úteis, publicação numa segunda sem feriados | Publicação em 02/03/2026 (seg) | 15 dias úteis | nacionais + recesso | 03/03/2026 (ter) | **23/03/2026 (seg)** | — | |
| P02 | 15 dias úteis, publicação numa sexta: começa na segunda | Publicação em 06/03/2026 (sex) | 15 dias úteis | nacionais + recesso | 09/03/2026 (seg) | **27/03/2026 (sex)** | — | |
| P03 | 15 dias úteis atravessando a Sexta-feira Santa e Tiradentes | Publicação em 30/03/2026 (seg) | 15 dias úteis | nacionais + recesso | 31/03/2026 (ter) | **22/04/2026 (qua)** | — | |
| P04 | 5 dias úteis com o 1º de maio no meio | Publicação em 28/04/2026 (ter) | 5 dias úteis | nacionais + recesso | 29/04/2026 (qua) | **06/05/2026 (qua)** | — | |
| P05 | 1 dia útil: vence no dia útil seguinte | Publicação em 03/06/2026 (qua) | 1 dias úteis | nacionais + recesso | 04/06/2026 (qui) | **04/06/2026 (qui)** | — | |
| P06 | 1 dia útil numa véspera de feriado nacional (Independência na segunda) | Publicação em 04/09/2026 (sex) | 1 dias úteis | nacionais + recesso | 08/09/2026 (ter) | **08/09/2026 (ter)** | — | |
| P07 | 15 dias úteis com 7/9 e 12/10 (segundas) | Publicação em 01/09/2026 (ter) | 15 dias úteis | nacionais + recesso | 02/09/2026 (qua) | **23/09/2026 (qua)** | — | |
| P08 | 15 dias úteis com Finados e Consciência Negra | Publicação em 28/10/2026 (qua) | 15 dias úteis | nacionais + recesso | 29/10/2026 (qui) | **19/11/2026 (qui)** | — | |
| P09 | Carnaval não é feriado nacional: conta na Justiça comum sem lei local | Publicação em 13/02/2026 (sex) | 5 dias úteis | nacionais + recesso | 16/02/2026 (seg) | **20/02/2026 (sex)** | — | |
| P10 | 30 dias úteis em 2025 com Sexta-feira Santa e Tiradentes | Publicação em 01/04/2025 (ter) | 30 dias úteis | nacionais + recesso | 02/04/2025 (qua) | **16/05/2025 (sex)** | — | |
| P11 | 15 dias úteis em 2025 com Consciência Negra numa quinta | Publicação em 10/11/2025 (seg) | 15 dias úteis | nacionais + recesso | 11/11/2025 (ter) | **02/12/2025 (ter)** | — | |
| P12 | 10 dias úteis com 15/11 num domingo (2026) | Publicação em 09/11/2026 (seg) | 10 dias úteis | nacionais + recesso | 10/11/2026 (ter) | **24/11/2026 (ter)** | — | |
| P13 | 8 dias úteis (recurso trabalhista) sem feriados | Publicação em 03/08/2026 (seg) | 8 dias úteis | nacionais + recesso | 04/08/2026 (ter) | **13/08/2026 (qui)** | — | |
| P14 | 3 dias úteis terminando numa sexta | Publicação em 04/08/2026 (ter) | 3 dias úteis | nacionais + recesso | 05/08/2026 (qua) | **07/08/2026 (sex)** | — | |
| R01 | Publicação em 18/12: o prazo volta a correr em 21/01 | Publicação em 18/12/2026 (sex) | 15 dias úteis | nacionais + recesso | 21/01/2027 (qui) | **10/02/2027 (qua)** | — | |
| R02 | Prazo iniciado em dezembro fica suspenso no recesso e retoma em 21/01 | Publicação em 10/12/2026 (qui) | 15 dias úteis | nacionais + recesso | 11/12/2026 (sex) | **02/02/2027 (ter)** | — | |
| R03 | Publicação durante o recesso: começa em 21/01 | Publicação em 08/01/2027 (sex) | 5 dias úteis | nacionais + recesso | 21/01/2027 (qui) | **27/01/2027 (qua)** | — | |
| R04 | Recesso 2025/2026 com prazo longo (30 dias) | Publicação em 01/12/2025 (seg) | 30 dias úteis | nacionais + recesso | 02/12/2025 (ter) | **11/02/2026 (qua)** | — | |
| R05 | Dias corridos também param no recesso | Publicação em 14/12/2026 (seg) | 10 dias corridos | nacionais + recesso | 15/12/2026 (ter) | **25/01/2027 (seg)** | — | |
| R06 | Publicação em 19/12 (sábado não há; sexta 18/12 em 2026) com 1 dia | Publicação em 18/12/2026 (sex) | 1 dias úteis | nacionais + recesso | 21/01/2027 (qui) | **21/01/2027 (qui)** | — | |
| J01 | DJEN disponibilizado na quinta: publicado na sexta, conta da segunda | Disponibilização no DJEN em 05/03/2026 (qui) | 15 dias úteis | nacionais + recesso | 09/03/2026 (seg) | **27/03/2026 (sex)** | — | |
| J02 | DJEN disponibilizado na sexta: publicado na segunda, conta da terça | Disponibilização no DJEN em 06/03/2026 (sex) | 15 dias úteis | nacionais + recesso | 10/03/2026 (ter) | **30/03/2026 (seg)** | — | |
| J03 | DJEN disponibilizado na véspera da Sexta-feira Santa | Disponibilização no DJEN em 02/04/2026 (qui) | 5 dias úteis | nacionais + recesso | 07/04/2026 (ter) | **13/04/2026 (seg)** | — | |
| J04 | DJEN disponibilizado em 30/04 (1º de maio é sexta) | Disponibilização no DJEN em 30/04/2026 (qui) | 15 dias úteis | nacionais + recesso | 05/05/2026 (ter) | **25/05/2026 (seg)** | — | |
| J05 | DJEN disponibilizado em 17/12: publicado em 18/12, prazo suspenso no recesso | Disponibilização no DJEN em 17/12/2026 (qui) | 15 dias úteis | nacionais + recesso | 21/01/2027 (qui) | **10/02/2027 (qua)** | — | |
| J06 | DJEN disponibilizado em 18/12: publicação presumida só em 21/01 | Disponibilização no DJEN em 18/12/2026 (sex) | 15 dias úteis | nacionais + recesso | 22/01/2027 (sex) | **11/02/2027 (qui)** | — | |
| J07 | DJEN com 5 dias úteis e internal 2 dias antes | Disponibilização no DJEN em 15/06/2026 (seg); data interna 2 dia(s) útil(eis) antes | 5 dias úteis | nacionais + recesso | 17/06/2026 (qua) | **23/06/2026 (ter)** | 19/06/2026 (sex) | |
| J08 | DJEN com prazo de 30 dias úteis em dobro (Fazenda) | Disponibilização no DJEN em 09/03/2026 (seg) | 30 dias úteis em dobro | nacionais + recesso | 11/03/2026 (qua) | **05/06/2026 (sex)** | — | |
| E01 | Consulta no 3º dia: intimação na consulta | Envio ao portal em 02/03/2026 (seg); consulta em 05/03/2026 (qui) | 15 dias úteis | nacionais + recesso | 06/03/2026 (sex) | **26/03/2026 (qui)** | — | |
| E02 | Sem consulta: intimação tácita no 10º dia (dia útil) | Envio ao portal em 02/03/2026 (seg) | 15 dias úteis | nacionais + recesso | 13/03/2026 (sex) | **02/04/2026 (qui)** | — | |
| E03 | Sem consulta: 10º dia cai no sábado e passa para a segunda | Envio ao portal em 04/03/2026 (qua) | 15 dias úteis | nacionais + recesso | 17/03/2026 (ter) | **07/04/2026 (ter)** | — | |
| E04 | Consulta num domingo: intimação na segunda | Envio ao portal em 02/03/2026 (seg); consulta em 08/03/2026 (dom) | 5 dias úteis | nacionais + recesso | 10/03/2026 (ter) | **16/03/2026 (seg)** | — | |
| E05 | Consulta depois dos 10 dias não adia a intimação tácita | Envio ao portal em 02/03/2026 (seg); consulta em 20/03/2026 (sex) | 5 dias úteis | nacionais + recesso | 13/03/2026 (sex) | **19/03/2026 (qui)** | — | |
| E06 | Consulta no próprio dia do envio | Envio ao portal em 03/08/2026 (seg); consulta em 03/08/2026 (seg) | 10 dias úteis | nacionais + recesso | 04/08/2026 (ter) | **17/08/2026 (seg)** | — | |
| E07 | Tácita caindo em Tiradentes passa para o dia útil seguinte | Envio ao portal em 11/04/2026 (sáb) | 5 dias úteis | nacionais + recesso | 23/04/2026 (qui) | **29/04/2026 (qua)** | — | |
| E08 | Envio no recesso: tácita em 31/12 vai a 21/01 | Envio ao portal em 21/12/2026 (seg) | 5 dias úteis | nacionais + recesso | 22/01/2027 (sex) | **28/01/2027 (qui)** | — | |
| F01 | Juntada do AR numa quarta | Fato do art. 231 em 06/05/2026 (qua) | 15 dias úteis | nacionais + recesso | 07/05/2026 (qui) | **27/05/2026 (qua)** | — | |
| F02 | Ciência numa sexta: começa na segunda | Fato do art. 231 em 08/05/2026 (sex) | 10 dias úteis | nacionais + recesso | 11/05/2026 (seg) | **22/05/2026 (sex)** | — | |
| F03 | Juntada num sábado (sistema): começa no primeiro dia útil | Fato do art. 231 em 09/05/2026 (sáb) | 5 dias úteis | nacionais + recesso | 11/05/2026 (seg) | **15/05/2026 (sex)** | — | |
| F04 | Juntada na véspera de feriado nacional (Independência) | Fato do art. 231 em 04/09/2026 (sex) | 5 dias úteis | nacionais + recesso | 08/09/2026 (ter) | **14/09/2026 (seg)** | — | |
| D01 | 15 dias em dobro (30 dias úteis) | Publicação em 02/03/2026 (seg) | 15 dias úteis em dobro | nacionais + recesso | 03/03/2026 (ter) | **14/04/2026 (ter)** | — | |
| D02 | 5 dias em dobro (10 dias úteis) com Sexta-feira Santa | Publicação em 30/03/2026 (seg) | 5 dias úteis em dobro | nacionais + recesso | 31/03/2026 (ter) | **14/04/2026 (ter)** | — | |
| D03 | 15 dias em dobro atravessando o recesso | Publicação em 01/12/2026 (ter) | 15 dias úteis em dobro | nacionais + recesso | 02/12/2026 (qua) | **12/02/2027 (sex)** | — | |
| D04 | Prazo corrido em dobro (10 → 20 dias) | Publicação em 03/08/2026 (seg) | 10 dias corridos em dobro | nacionais + recesso | 04/08/2026 (ter) | **24/08/2026 (seg)** | — | |
| C01 | 5 dias corridos terminando no sábado: prorroga para segunda | Publicação em 02/03/2026 (seg) | 5 dias corridos | nacionais + recesso | 03/03/2026 (ter) | **09/03/2026 (seg)** | — | |
| C02 | 10 dias corridos sem prorrogação | Publicação em 03/03/2026 (ter) | 10 dias corridos | nacionais + recesso | 04/03/2026 (qua) | **13/03/2026 (sex)** | — | |
| C03 | Publicação na sexta: corridos começam na segunda (CPC, art. 224, § 1º) | Publicação em 06/03/2026 (sex) | 5 dias corridos | nacionais + recesso | 09/03/2026 (seg) | **13/03/2026 (sex)** | — | |
| C04 | Corridos vencendo na Sexta-feira Santa prorrogam até segunda | Publicação em 29/03/2026 (dom) | 5 dias corridos | nacionais + recesso | 30/03/2026 (seg) | **06/04/2026 (seg)** | — | |
| C05 | Corridos contam feriado no meio (Tiradentes) | Publicação em 16/04/2026 (qui) | 10 dias corridos | nacionais + recesso | 17/04/2026 (sex) | **27/04/2026 (seg)** | — | |
| C06 | 30 dias corridos | Publicação em 01/06/2026 (seg) | 30 dias corridos | nacionais + recesso | 02/06/2026 (ter) | **01/07/2026 (qua)** | — | |
| S01 | SP: Revolução Constitucionalista (9/7) não conta | Publicação em 06/07/2026 (seg) | 5 dias úteis | nacionais + recesso + SP | 07/07/2026 (ter) | **14/07/2026 (ter)** | — | |
| S02 | Mesmo prazo fora de SP conta o 9/7 | Publicação em 06/07/2026 (seg) | 5 dias úteis | nacionais + recesso | 07/07/2026 (ter) | **13/07/2026 (seg)** | — | |
| S03 | RJ: São Jorge (23/4) e Tiradentes | Publicação em 17/04/2026 (sex) | 5 dias úteis | nacionais + recesso + RJ | 20/04/2026 (seg) | **28/04/2026 (ter)** | — | |
| S04 | RJ: terça de Carnaval é feriado estadual | Publicação em 12/02/2026 (qui) | 5 dias úteis | nacionais + recesso + RJ | 13/02/2026 (sex) | **20/02/2026 (sex)** | — | |
| S05 | RJ: Corpus Christi por lei estadual a partir de 2026 | Publicação em 01/06/2026 (seg) | 5 dias úteis | nacionais + recesso + RJ | 02/06/2026 (ter) | **09/06/2026 (ter)** | — | |
| S06 | ES: Nossa Senhora da Penha (segunda após a Páscoa) | Publicação em 08/04/2026 (qua) | 5 dias úteis | nacionais + recesso + ES | 09/04/2026 (qui) | **16/04/2026 (qui)** | — | |
| S07 | PE: Data Magna (6/3) numa sexta | Publicação em 03/03/2026 (ter) | 5 dias úteis | nacionais + recesso + PE | 04/03/2026 (qua) | **11/03/2026 (qua)** | — | |
| S08 | Vencimento corrido num feriado estadual prorroga (SP) | Publicação em 03/07/2026 (sex) | 6 dias corridos | nacionais + recesso + SP | 06/07/2026 (seg) | **13/07/2026 (seg)** | — | |
| JF1 | JF: Semana Santa de quarta a domingo | Publicação em 30/03/2026 (seg) | 5 dias úteis | nacionais + recesso + JF | 31/03/2026 (ter) | **09/04/2026 (qui)** | — | |
| JF2 | JF: segunda e terça de Carnaval | Publicação em 12/02/2026 (qui) | 5 dias úteis | nacionais + recesso + JF | 13/02/2026 (sex) | **23/02/2026 (seg)** | — | |
| JF3 | JF: 11 de agosto | Publicação em 07/08/2026 (sex) | 3 dias úteis | nacionais + recesso + JF | 10/08/2026 (seg) | **13/08/2026 (qui)** | — | |
| JF4 | JF: 8 de dezembro antes do recesso | Publicação em 03/12/2026 (qui) | 5 dias úteis | nacionais + recesso + JF | 04/12/2026 (sex) | **11/12/2026 (sex)** | — | |
| JF5 | JF: 1º de novembro num domingo e Finados | Publicação em 29/10/2026 (qui) | 3 dias úteis | nacionais + recesso + JF | 30/10/2026 (sex) | **04/11/2026 (qua)** | — | |
| O01 | Feriado municipal cadastrado pelo escritório | Publicação em 16/03/2026 (seg) | 5 dias úteis | nacionais + recesso, feriado municipal em 19/03/2026 | 17/03/2026 (ter) | **24/03/2026 (ter)** | — | |
| O02 | Indisponibilidade no vencimento prorroga (dias úteis) | Publicação em 08/05/2026 (sex) | 5 dias úteis | nacionais + recesso, indisponibilidade em 15/05/2026 | 11/05/2026 (seg) | **18/05/2026 (seg)** | — | |
| O03 | Indisponibilidade no vencimento prorroga (dias corridos) | Publicação em 10/05/2026 (dom) | 5 dias corridos | nacionais + recesso, indisponibilidade em 15/05/2026 | 11/05/2026 (seg) | **18/05/2026 (seg)** | — | |
| O04 | Suspensão determinada pelo tribunal por uma semana | Publicação em 03/06/2026 (qua) | 5 dias úteis | nacionais + recesso, suspensão de 08 a 12/06/2026 | 04/06/2026 (qui) | **17/06/2026 (qua)** | — | |
| O05 | Suspensão do tribunal também para os dias corridos | Publicação em 05/06/2026 (sex) | 5 dias corridos | nacionais + recesso, suspensão de 08 a 12/06/2026 | 15/06/2026 (seg) | **19/06/2026 (sex)** | — | |
| O06 | Indisponibilidade no primeiro dia: começa no dia útil seguinte | Publicação em 27/02/2026 (sex) | 3 dias úteis | nacionais + recesso, indisponibilidade em 02/03/2026 | 03/03/2026 (ter) | **05/03/2026 (qui)** | — | |
| I01 | Data interna 3 dias úteis antes do vencimento | Publicação em 02/03/2026 (seg); data interna 3 dia(s) útil(eis) antes | 15 dias úteis | nacionais + recesso | 03/03/2026 (ter) | **23/03/2026 (seg)** | 18/03/2026 (qua) | |
| I02 | Data interna não passa do primeiro dia contado | Publicação em 02/03/2026 (seg); data interna 5 dia(s) útil(eis) antes | 2 dias úteis | nacionais + recesso | 03/03/2026 (ter) | **04/03/2026 (qua)** | 03/03/2026 (ter) | |
| I03 | Data interna pula o feriado ao recuar | Publicação em 14/04/2026 (ter); data interna 2 dia(s) útil(eis) antes | 5 dias úteis | nacionais + recesso | 15/04/2026 (qua) | **22/04/2026 (qua)** | 17/04/2026 (sex) | |

## Feriados estaduais no seed da plataforma

| UF | Data | Feriado | Fundamento |
| --- | --- | --- | --- |
| AC | 23/01 | Dia do Evangélico | Lei estadual 1.538/2004 (a partir de 2004) |
| AC | 08/03 | Dia Internacional da Mulher | Lei estadual 1.411/2001 (a partir de 2001) |
| AC | 15/06 | Aniversário do Acre | Lei estadual 14/1964 (a partir de 1964) |
| AC | 05/09 | Dia da Amazônia | Lei estadual 1.526/2004 (a partir de 2004) |
| AL | 24/06 | São João | Lei estadual 5.508/1993 (a partir de 1993) |
| AL | 29/06 | São Pedro | Lei estadual 5.509/1993 (a partir de 1993) |
| AL | 30/11 | Dia do Evangélico | Lei estadual 7.530/2013 (a partir de 2014) |
| AM | 05/09 | Elevação do Amazonas à categoria de Província | Lei estadual 25/1977 (a partir de 1977) |
| AP | 19/03 | Dia de São José | Lei estadual 667/2002 (a partir de 2002) |
| AP | 13/09 | Criação do Território Federal do Amapá | Constituição do Estado do Amapá (a partir de 1991) |
| BA | 02/07 | Independência da Bahia | Constituição do Estado da Bahia, art. 6º, § 3º (a partir de 1989) |
| CE | 25/03 | Data Magna do Ceará | Constituição do Estado do Ceará, art. 18, parágrafo único (a partir de 1989) |
| DF | 30/11 | Dia do Evangélico | Lei distrital 963/1995 (a partir de 1995) |
| MA | 28/07 | Adesão do Maranhão à Independência | Lei estadual 2.457/1964 (a partir de 1964) |
| PA | 15/08 | Adesão do Pará à Independência | Lei estadual 5.999/1996 (a partir de 1996) |
| PB | 05/08 | Fundação do Estado da Paraíba | Lei estadual 3.489/1967 (a partir de 1967) |
| PE | 06/03 | Data Magna de Pernambuco (Revolução de 1817) | Lei estadual 16.241/2017 (a partir de 2018) |
| RJ | 23/04 | Dia de São Jorge | Lei estadual 5.198/2008 (a partir de 2008) |
| RN | 03/10 | Mártires de Cunhaú e Uruaçu | Lei estadual 8.913/2006 (a partir de 2007) |
| RO | 04/01 | Criação do Estado de Rondônia | Lei estadual 2.291/2010 (a partir de 2011) |
| RO | 18/06 | Dia do Evangélico | Lei estadual 1.026/2001 (a partir de 2002) |
| RR | 05/10 | Criação do Estado de Roraima | Constituição do Estado de Roraima, art. 9º (a partir de 1991) |
| RS | 20/09 | Revolução Farroupilha | Constituição do Estado do Rio Grande do Sul, art. 6º, parágrafo único (a partir de 1989) |
| SE | 08/07 | Emancipação Política de Sergipe | Constituição do Estado de Sergipe, art. 269 (a partir de 1989) |
| SP | 09/07 | Revolução Constitucionalista de 1932 | Lei estadual 9.497/1997 (a partir de 1997) |
| TO | 15/04 | Senhor do Bonfim | Lei estadual 4.509/2024 (a partir de 2025) |
| TO | 08/09 | Nossa Senhora da Natividade | Lei estadual 627/1993 (a partir de 1994) |
| TO | 05/10 | Criação do Estado do Tocantins | Lei estadual 98/1989 (a partir de 1990) |
| ES | Páscoa +8 dias | Nossa Senhora da Penha | Lei estadual 11.010/2019 (a partir de 2020) |
| RJ | Páscoa -47 dias | Terça-feira de Carnaval | Lei estadual 5.243/2008 (a partir de 2009) |
| RJ | Páscoa +60 dias | Corpus Christi | Lei estadual 11.002/2025 (a partir de 2026) |

**Ficaram fora do seed por falta de fonte confiável** (confirmar, ou cadastrar no calendário do escritório):

| UF | Data | Feriado |
| --- | --- | --- |
| AL | 16/09 | Emancipação política |
| AP | 25/07 | São Tiago (ADI 4820 sem desfecho localizado) |
| MS | 11/10 | Criação do estado |
| PI | 19/10 | Dia do Piauí |
| RN | 07/08 | Dia do Rio Grande do Norte |
| SC | 11/08 | Dia de Santa Catarina |
| SC | 25/11 | Santa Catarina de Alexandria |
| TO | 18/03 | Autonomia do Estado |
| PE | 24/06 | São João (o TJPE lista, sem lei localizada) |

Nenhum feriado estadual por lei foi encontrado em MG, MT, PR e GO.

**Justiça Federal** (Lei 5.010/1966, art. 62):
- recesso de 20/12 a 06/01;
- Semana Santa, de quarta-feira ao domingo de Páscoa;
- segunda e terça-feira de Carnaval;
- 11/08, 01/11 e 08/12.

Conferir a redação no Planalto.

## Registro da revisão

- Revisor (nome e OAB):
- Data:
- Casos com divergência:
