# ADR 0037: calendário forense e calculadora de prazos

- **Status:** Proposta (para revisão no PR [#36](https://github.com/iagogoliveira/ProjetoLaw/pull/36), 7A)
- **Data:** 2026-10-20
- **Etapa:** 7 (PR 7A)

## Contexto

A Etapa 7 pede uma calculadora de prazos pura e determinística. Ela tem de considerar:
- feriados nacionais, estaduais, municipais e forenses;
- suspensões, como o recesso do CPC (art. 220) e as indisponibilidades de sistema;
- prazo em dobro;
- dias úteis ou dias corridos.

A calculadora devolve a data e a memória de cálculo, e o resultado é sempre uma sugestão que alguém confirma.

Antes deste PR, o Platform tinha só os feriados nacionais (data fixa ou relativa à Páscoa), e o Scheduling não tinha calendário.

O plano da Etapa 7 e as oito respostas foram aprovados pelo usuário em 2026-10-20. As respostas que valem aqui:
- **(1)** O agente redige os casos de prazo, com o fundamento legal, e um advogado revisa. O critério 1 só é marcado depois dessa revisão.
- **(2)** A plataforma mantém os feriados nacionais, o recesso, os da Justiça Federal e os estaduais. O escritório cadastra os municipais, os forenses próprios e as indisponibilidades.
- **(3)** O DJEN e o art. 231 do CPC definem o início da contagem. Dias úteis ou corridos e a dobra do prazo são escolhas explícitas.
- **(8)** FsCheck é usado nos testes de propriedade.

## Decisão

### 1. Calendário da plataforma (Platform)

O `Holiday` ganha:
- a abrangência: nacional, estadual (com a UF) ou Justiça Federal;
- o tipo: feriado (dia sem expediente) ou suspensão (os prazos não correm);
- a duração em dias seguidos a partir da data da regra.

A regra continua sendo uma data fixa ou um deslocamento a partir da Páscoa, então não é preciso cadastrar ano a ano.

O seed versionado traz:
- os 10 feriados nacionais;
- a suspensão de 20/12 a 20/01 (CPC, art. 220), com 32 dias;
- os dias sem expediente da Justiça Federal (Lei 5.010/1966, art. 62):
  - o recesso de 20/12 a 06/01;
  - a Semana Santa, de quarta-feira ao domingo de Páscoa;
  - a segunda e a terça-feira de Carnaval;
  - 11/08, 01/11 e 08/12;
- 31 feriados estaduais de 20 UFs; MS, PI e SC só tinham fontes fracas.

O contrato `IHolidayDirectory` (`Platform.Contracts`) devolve os dias de um intervalo para uma UF e para a Justiça Federal.

### 2. Critério de inclusão dos feriados estaduais

Os feriados estaduais vieram de uma pesquisa em atos oficiais de tribunais e governos de 2025 e 2026. O Planalto e as assembleias não responderam na pesquisa.

Entram no seed só os de confiança alta ou média, com a data confirmada em ato oficial recente ou em constituição estadual. Os de fonte fraca ficam de fora. O motivo é o efeito de cada erro:
- um feriado que falta deixa a sugestão de vencimento **mais cedo**, o que é o erro seguro;
- um feriado a mais a empurraria para depois, com risco de perder o prazo.

Ficaram de fora, para a revisão do advogado ou para cadastro pelo escritório:

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
| PE | 24/06 | São João (listado pelo TJPE, sem lei localizada) |

Os pontos facultativos não entram. Nenhum feriado estadual foi encontrado para MG, MT, PR e GO.

A redação do art. 62 da Lei 5.010/1966 e do art. 220 do CPC foi confirmada por atos de tribunais que os citam. Falta conferir no Planalto, o que fica para a revisão.

### 3. Calendário do escritório (Scheduling)

`CalendarEntry` (tabela `scheduling.calendar_entries`, com RLS) registra:
- o tipo: feriado, suspensão ou indisponibilidade de sistema;
- a abrangência: o escritório todo, uma UF, uma comarca (UF e município) ou um tribunal;
- o período, que pode se repetir todo ano.

O município é comparado sem acentos, sem diferença de maiúsculas e com espaços simples.

Uma indisponibilidade conta como dia sem expediente, porque prorroga o início e o vencimento (CPC, art. 224, § 1º).

A permissão nova `schedule.calendar.manage` é concedida aos perfis padrão Administrador, Sócio e Controladoria jurídica, e aos perfis desses tipos que já existem, por migration. A leitura usa `schedule.deadlines.read`.

### 4. Calculadora (`IDeadlineCalculator`, no domínio do Scheduling)

A calculadora é pura: recebe os dados e um `WorkingCalendar`, e não consulta relógio nem banco. As regras:

**Termo inicial**
- **Fato do art. 231 ou publicação:** o próprio dia.
- **DJEN:** a publicação é o primeiro dia útil após a disponibilização (Lei 11.419/2006, art. 4º, § 3º).
- **Portal:** a intimação é a data da consulta ou, sem consulta, o décimo dia corrido após o envio; se cair em dia sem expediente, passa ao primeiro dia útil (Lei 11.419/2006, art. 5º, §§ 1º a 3º).

**Contagem**
- **Dias úteis:** exclui o dia do começo e conta só os dias com expediente (CPC, arts. 219 e 224).
- **Dias corridos:**
  - o primeiro dia contado precisa ser útil;
  - depois conta todos os dias, exceto os de suspensão;
  - o vencimento em dia sem expediente é prorrogado para o primeiro dia útil.
- **Dobra:** multiplica a quantidade de dias por dois.

**Data interna:** recua N dias úteis a partir do vencimento (no máximo 30), sem passar do primeiro dia contado.

**Limites:** o prazo vai de 1 a 365 dias. Se a conta sair do intervalo carregado no calendário, a calculadora recusa (`scheduling.calendar_out_of_range`) em vez de supor que o dia é útil.

**Memória de cálculo:** cada linha traz a data, a etapa (fato, publicação, intimação, pulado, contado ou prorrogado), a ordem do dia contado e o motivo. O motivo inclui o nome do feriado e o fundamento legal.

### 5. Local do processo

`POST /api/v1/deadlines/calculate` exige `schedule.deadlines.manage`. Se a consulta traz um processo, também exige acesso a esse processo. A consulta não grava nada.

O local sai do processo: o tribunal (pelo CNJ) e a comarca. A consulta pode informar ou corrigir esses dados, e a resposta diz qual local foi usado.

A UF do tribunal só é presumida quando ele atende uma única UF:
- Justiça Estadual, Eleitoral e Militar estadual;
- TRTs de uma só UF.

Nos TRFs e nos TRTs da 8ª, 10ª, 11ª e 14ª Regiões, a UF precisa ser informada. Os dias da Justiça Federal valem quando o tribunal é do segmento 4.

O contrato `ICaseDirectory` ganhou `LocationAsync`, que devolve o tribunal e a comarca; a autorização é feita antes.

### 6. Testes

- **Suíte de aceite:** 72 casos de prazo (o critério pede pelo menos 60), cobrindo:
  - publicação e DJEN;
  - portal e fato do art. 231;
  - recesso;
  - dobra;
  - dias corridos;
  - feriados estaduais e da Justiça Federal;
  - calendário do escritório e data interna.

  Os valores esperados foram conferidos contra uma implementação de referência independente (script fora do repositório). **Revisão por advogado pendente.**
- **Testes de propriedade (FsCheck 3.4.0, BSD-3-Clause):**
  - o vencimento é dia útil;
  - a contagem bate com o total;
  - mais dias, ou a dobra, nunca antecipam o vencimento;
  - a memória sai em ordem;
  - o resultado é determinístico.
- **Testes da API com o seed real:**
  - TJSP com feriado estadual;
  - TRF1 com Semana Santa e sem UF presumida;
  - feriado municipal do escritório;
  - recesso;
  - erros e permissões.

## Pacotes

- **FsCheck 3.4.0** (BSD-3-Clause, publicado em 2026-08-20), só nos testes. Depende do FSharp.Core (MIT). A 3.5.0, publicada em 2026-10-17, foi evitada por ter menos de duas semanas.

## Consequências

- Uma mudança de feriado não altera prazos já confirmados. O alerta sobre prazos confirmados afetados fica no 7B, junto com o ciclo do prazo.
- Ampliar o seed estadual é uma migration aditiva com `UpdateData` ou `InsertData`. Os ids são determinísticos pela chave natural.
- A calculadora e o calendário ficam prontos para a tela do 7E e para a triagem, que passará a mostrar a sugestão.
