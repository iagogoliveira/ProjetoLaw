# ADR 0025 — Módulo Litigation: processos e casos, numeração interna e partes do Crm

- **Status:** Proposta (para revisão no PR 5A)
- **Data:** 2026-10-18
- **Etapa:** 5 (PR 5A)

## Contexto

O RF-09 pede o cadastro de processos e casos. São processos judiciais com número CNJ e casos sem processo (administrativos, PROCON, extrajudiciais), com:
- partes e advogados contrários com OAB;
- responsáveis e cliente pagador;
- hierarquia (principal, incidente, recurso, cumprimento, apenso);
- encerramento com motivo, resultado e valores.

A Etapa 5 pede ainda numeração interna configurável (`{AREA}-{ANO}-{SEQ:00000}`), com contador por organização, e o pré-preenchimento do tribunal pelo número CNJ. Também entrega o `ILitigationLookup` real, que fecha com processos reais o critério de conflito da Etapa 4.

Decisões de negócio confirmadas em 2026-10-18:
- a sequência da numeração é contada **por área e por ano**;
- o processo tem um campo **filial** opcional, que vai definir o escopo por filial no PR 5B;
- os índices monetários vêm do BCB e do IBGE, e os juros são simples em % ao mês. As duas decisões entram no PR 5C.

## Decisão

1. **Módulo `Litigation`** (schema `litigation`, com RLS nas 6 tabelas):
   - `cases`:
     - tipo (judicial, administrativo, extrajudicial) e pasta interna, única no escritório;
     - CNJ com 20 dígitos, único entre os processos ativos;
     - tribunal, instância, comarca, vara, rito, classe, assunto, tipo de ação e fase (as duas últimas vêm das tabelas auxiliares);
     - área, filial, situação, valor da causa e datas;
     - descrição, cliente pagador, encerramento e `custom_fields`;
   - `case_parties`: a pessoa do Crm, o polo (ativo, passivo, terceiro), se é cliente do escritório, e o advogado da parte com OAB;
   - `case_assignments`: sócio e advogado são usuários; equipe é uma equipe do Organization;
   - `case_relations`: principal → vinculado, com o tipo. O vinculado tem no máximo um principal (índice único), e a hierarquia não tem ciclos, conferidos no caso de uso;
   - `case_numberings` (formato e siglas das áreas) e `case_counters`.
2. **Regras do cadastro:**
   - o processo judicial exige CNJ com dígito verificador válido; os outros tipos não têm CNJ;
   - o tipo e a pasta interna não mudam depois do cadastro;
   - é preciso ao menos uma parte cliente, sem repetir a mesma pessoa, e o pagador é uma das partes clientes;
   - um caso encerrado não muda até ser reaberto. A reabertura apaga o encerramento, que fica na auditoria;
   - uma pessoa anonimizada não entra como parte nova.
3. **Numeração:**
   - o formato tem exatamente um `{SEQ}` (com zeros opcionais, ex. `{SEQ:00000}`), e pode ter `{AREA}` e `{ANO}`;
   - a sigla da área é a configurada (de 1 a 10 letras ou números) ou as quatro primeiras letras do nome sem acentos (ex.: "Trabalhista" vira `TRAB`). Sem área, a sigla é `GERAL`;
   - o ano é o do cadastro, no fuso legal;
   - o contador é `INSERT … ON CONFLICT DO UPDATE … RETURNING`, parametrizado, na transação do cadastro. Cadastros simultâneos nunca recebem o mesmo número, e um cadastro que falha devolve o número;
   - mudar o formato não renumera os processos existentes.
4. **Contratos entre módulos**, sem tocar no banco dos outros módulos:
   - **Crm.Contracts** passa a ter o `ILitigationLookup`, que saiu do Crm.Application para o Litigation poder implementá-lo. O registro `CaseParty` foi renomeado para `CasePartyMatch`;
   - **Crm.Contracts** ganha também o `IPeopleDirectory` (pessoas por id; ids por nome normalizado ou documento);
   - **Platform.Contracts:** `ICourtDirectory` (tribunal pelo segmento J e pelo código TR do CNJ);
   - **Organization.Contracts:** `IOrganizationDirectory` (áreas, filiais e equipes por id);
   - continuam valendo o `ILookupItemValidator`, o `ICustomFieldValidator` e o `IMembershipLookup`.
5. **Partes no conflito de interesses:**
   - o `CasePartyLookup` acha, pelo `IPeopleDirectory`, as pessoas com o nome ou o documento consultados;
   - devolve os processos em que elas aparecem, com o lado (cliente ou parte contrária) e o nome dos clientes do processo;
   - é registrado depois do Crm e substitui o stub `TryAdd` da Etapa 4.
6. **Fusão de cadastros:** o consumidor `PeopleMergedIntegrationEvent`, no Worker e com inbox, aponta as partes e o pagador para o cadastro mantido, sem repetir a pessoa no mesmo processo. A anonimização não exige nada aqui, porque o Litigation guarda só o id da pessoa.
7. **Endpoints e permissões:**
   - leitura com `cases.read`, cadastro com `cases.create`, edição, encerramento, situação e hierarquia com `cases.edit`, e exclusão (lógica) com `cases.delete`;
   - a numeração é configurada com `settings.organization.manage`;
   - nesta fase quem tem `cases.read` vê todos os processos do escritório. O escopo por responsável, equipe, área e filial, e os casos sigilosos, chegam no PR 5B.
8. **Apoio:** os filtros de enum na query (`EnumQuery`) foram para o Web.Common, para o Crm e o Litigation usarem o mesmo código. Cada módulo mantém o próprio código de erro.

## Consequências

- **Listas com nomes de outros módulos:** a lista e o detalhe buscam os nomes das partes no Crm, numa chamada por página. Para a meta de 100 mil processos (PR 5B), o teste de carga vai dizer se é preciso guardar o nome na parte.
- **Sigla derivada do nome:** pode coincidir entre áreas (ex.: "Civil" e "Cível" viram `CIVI` e `CIVE`; já "Tributário" e "Tributos" viram `TRIB` as duas). O escritório resolve configurando as siglas, e a tela para isso entra no PR 5E.
- **Lacunas na sequência:** nunca há repetição, mas um processo excluído deixa o número dele vago.
- **Classe e assunto** são texto livre por enquanto. As tabelas processuais unificadas do CNJ (TPU) ficam para quando houver integração com os tribunais (Etapa 6).
