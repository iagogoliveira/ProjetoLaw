# ADR 0029 — Linha do tempo, importação de processos e operações em lote

- **Status:** Aceita (PR #28 mergeado em 2026-10-18)
- **Data:** 2026-10-18
- **Etapa:** 5 (PR 5D)

## Contexto

A Etapa 5 pede ainda (tarefas 4, 6 e 7):
- a linha do tempo do processo, juntando os acontecimentos de todos os módulos por `ITimelineContributor`;
- a importação em massa por XLSX/CSV com validação linha a linha, pré-visualização, processamento em job e relatório de erros para baixar;
- as operações em lote: redistribuir o responsável e alterar fase e situação.

O critério 2 pede que uma importação de 10 mil linhas com 3% inválidas conclua e gere o relatório exato das inválidas.

Decisões de negócio confirmadas em 2026-10-18:
- cliente ou parte contrária fora do cadastro é **cadastrado automaticamente**; se o CPF/CNPJ já existe, usa-se a pessoa existente;
- a planilha **pode trazer a pasta interna do sistema antigo** (única no escritório); sem ela, vale a numeração do escritório;
- **CNJ que já existe no escritório vai para o relatório**, e o processo existente não muda;
- **importam-se as linhas válidas** depois da confirmação, e as inválidas ficam no relatório.

## Decisão

1. **Linha do tempo (`GET /cases/{id}/timeline`):**
   - o contrato `ITimelineContributor` (Litigation.Contracts) devolve `TimelineEntry` com:
     - um id estável;
     - a data;
     - um código (`modulo.evento`), que a tela traduz;
     - os dados para o texto (valores em formato neutro: `yyyy-MM-dd`, ISO 8601, ponto decimal);
   - a rota junta todas as fontes e ordena da mais recente para a mais antiga, desempatando pelo id. A paginação é por cursor (data e id) e há filtro pelo começo do código;
   - **o Litigation contribui com:**
     - cadastro e mudanças de situação;
     - pedidos e alterações de valor (não os valores iniciais);
     - avaliações de risco;
     - acordos: abertura, rodadas, envio, aprovação, homologação, baixas e fim sem acordo;
     - audiências: agendamento, redesignações, realização e cancelamento;
   - **histórico de situação:** antes, suspender e reativar não guardavam data. A tabela `case_status_changes` passa a guardar cada mudança (de, para, observação, quem, quando) e só aceita inserções.
2. **Importação (`case_imports`, `case_import_errors`; permissão `cases.create`):**
   - **envio** (`POST /cases/imports`, multipart): até 10 MB e 20 mil linhas. O arquivo passa no antivírus, e o formato e o cabeçalho (`tipo` e `cliente_nome` obrigatórios) são conferidos na hora. Ele é guardado no armazenamento de objetos e a resposta é 202, com o job;
   - **validação** (job): valida todas as linhas com os cadastros carregados uma vez (áreas, filiais, fases e tipos de ação pelo nome, sem acento nem caixa; membros pelo e-mail; CNJs e pastas já usados). Grava os erros por célula, com o código estável, e as 20 primeiras linhas válidas da pré-visualização;
   - **repetição dentro do arquivo:** CNJ ou pasta repetidos dentro do arquivo são erro da linha que vem depois (`litigation.import_duplicate_in_file`). Se já existem no escritório, os erros são `litigation.cnj_taken` e `litigation.folder_taken`;
   - **confirmação** (`POST /cases/imports/{id}/confirm`, uma vez só, com alguma linha válida). O job de gravação:
     1. revalida, porque os cadastros podem ter mudado;
     2. grava em lotes de 500: as pessoas (contrato `IPeopleRegistry` do Crm), os processos e o avanço;
     3. anota a última linha gravada no mesmo `SaveChanges` do lote. Uma nova tentativa do Hangfire continua dali, sem duplicar;
   - **pessoas:**
     - com documento, a pessoa que o tem;
     - sem documento, a de mesmo nome e sem documento;
     - senão, um cadastro novo (CPF: física; CNPJ ou sem documento: jurídica);
     - a pessoa recebe o papel de cliente ou de parte contrária;
     - o polo do cliente vem da coluna `cliente_polo` (ativo, por padrão), e a contrária fica no polo oposto;
   - **relatório:** `GET /cases/imports/{id}/errors`, em XLSX, com linha, coluna, valor, código e mensagem;
   - **modelo:** `GET /cases/imports/template`, com as 23 colunas e um exemplo;
   - **acompanhamento:** o contrato novo `IJobTracker` (Platform.Contracts) cria e atualiza o job de `GET /api/v1/jobs/{id}` (seção 4.4). Também acompanham o `GET /cases/imports/{id}` (totais, pré-visualização e os 100 primeiros erros);
   - **contratos novos usados aqui:**
     - `IOrganizationDirectory.ListAsync` e `MemberUserIdsAsync`;
     - `ILookupItems` (os itens ativos de uma tabela auxiliar).
3. **Lote (`POST /cases/batch`, `cases.edit`):**
   - redistribuir (incluir um responsável ou substituir um por outro), mudar a fase, suspender ou reativar até 1.000 processos;
   - só os processos ao alcance de quem edita (escopo e sigilo) e abertos entram. Os demais voltam em `skipped`, com o código;
   - tudo é gravado numa transação, e as mudanças de situação entram no histórico.
4. **Pacote ClosedXML 0.105.1 (MIT):**
   - lê e escreve XLSX; o CSV tem leitor próprio (separador ";" ou ",", aspas, UTF-8);
   - as dependências ClosedXML.Parser, ExcelNumberFormat e RBush.Signed são MIT;
   - **a SixLabors.Fonts fica presa na 1.0.1 (Apache-2.0)** pela pinagem transitiva central: o ClosedXML aceita de 1.0 a 3.0, e da 2.0 em diante a licença é a Six Labors Split License, comercial acima de um porte de empresa;
   - registrado na ADR 0002.

## Consequências

- **Critério 2 da Etapa 5 atendido:** `CaseImportTests.Ten_thousand_rows_with_three_percent_invalid_import_the_rest_and_report_exactly_the_invalid_ones`.
  - O arquivo tem 10 mil linhas, com 300 inválidas de 6 tipos: tipo, dígito do CNJ, área, data, CPF e CNJ repetido.
  - Resultado: 9.700 processos gravados, e o relatório lista exatamente as 300 linhas, cada uma com o código esperado. Leva cerca de 25 s no teste.
- **Autor dos registros:** a gravação roda no Worker, sem usuário. Os processos e as pessoas importados ficam com o autor do sistema; quem enviou fica na importação (`created_by` de `case_imports`).
- **Pessoas sem documento:** a reutilização pelo nome exato pode juntar homônimos sem documento. A planilha deve trazer o CPF/CNPJ sempre que houver, e a fusão de cadastros do Crm corrige os casos restantes.
- **Os outros módulos** (prazos, documentos, financeiro) entram na linha do tempo nas etapas deles, registrando o próprio `ITimelineContributor`.
- **Ficam para o 5E:** a tela da importação e a da linha do tempo.
