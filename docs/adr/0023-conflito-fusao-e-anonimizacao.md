# ADR 0023 — Conflito de interesses, fusão de cadastros e anonimização LGPD

- **Status:** Proposta (para revisão no PR 4B)
- **Data:** 2026-10-17
- **Etapa:** 4 (PR 4B)

## Contexto

O RF-07 pede que, antes de abrir cliente ou caso, se pesquise nome e documento em clientes, partes contrárias e grupos econômicos, com o risco classificado em direto, indireto ou nenhum. Prosseguir com alerta exige uma justificativa registrada, e as verificações formam um relatório para auditoria e compliance da OAB.

O RF-06 pede também a fusão auditada de cadastros duplicados e a anonimização conforme a LGPD. Os processos só chegam na Etapa 5.

Decisões de negócio confirmadas em 2026-10-17:
- **lado:** a consulta informa se a pessoa será cliente ou parte contrária;
- **conflito indireto:** mesmo grupo econômico, nome parecido ou contato compartilhado;
- **critério de aceite com processos:** stub agora, prova com processos reais na Etapa 5;
- **fusão:** o cadastro mantido fica com os próprios dados, e os campos vazios recebem os do duplicado;
- **anonimização:** permissão nova, só para Administrador do escritório e Sócio, com motivo obrigatório.

## Decisão

1. **Conflito (`crm.conflict_checks` e `crm.conflict_check_hits`, com RLS):**
   - o lado oposto ao consultado é o que gera conflito: futuro cliente que já é parte contrária, ou futura parte contrária que já é cliente. Ser cliente e virar cliente de novo não é conflito;
   - **direto:**
     - mesmo documento de uma pessoa do lado oposto;
     - mesmo nome normalizado (`SearchText`) de uma pessoa do lado oposto;
     - parte do lado oposto num processo de outro cliente, vinda de `ILitigationLookup`;
   - **indireto:**
     - nome parecido com o de uma pessoa do lado oposto (`similarity` a partir de 0,6, com a semelhança gravada);
     - pessoa do lado oposto no mesmo grupo econômico da pessoa consultada, quando esta já está cadastrada;
     - e-mail compartilhado: os e-mails informados na consulta ou os da pessoa já cadastrada (próprio e de contatos) aparecem em uma pessoa do lado oposto;
   - **resultado:** o risco da verificação é o maior entre os achados. Cada pessoa ou processo aparece uma vez, com o maior risco;
   - **prosseguir** (`POST /api/v1/conflict-checks/{id}/proceed`) exige justificativa quando há risco e acontece uma vez só;
   - **relatório** (`GET /api/v1/conflict-checks`): das verificações mais recentes às mais antigas, com filtro por risco e cursor;
   - tudo exige `crm.conflicts.check`.
2. **`ILitigationLookup`:**
   - a interface fica no Crm.Application, que é quem consome;
   - o stub registrado (`NoCasesLitigationLookup`, com `TryAdd`) não encontra nada;
   - o módulo de processos da Etapa 5 registra a implementação real;
   - o critério de aceite já é testado com uma implementação falsa (`ConflictTests.An_opponent_in_a_case_of_another_client_is_a_direct_conflict`).
3. **Fusão** (`POST /api/v1/people/{id}/merge`, `crm.people.merge`):
   - só entre cadastros do mesmo tipo, e nunca com documentos diferentes;
   - o mantido fica com os próprios dados e recebe nos vazios os do duplicado;
   - papéis, endereços (sem repetir CEP, rua e número), contatos (sem repetir e-mail, ou o nome quando não há e-mail) e campos customizados (sem sobrescrever chave) são somados;
   - o duplicado fica excluído logicamente, com `merged_into_id`, e os achados de conflito passam para o mantido;
   - o documento passa em duas gravações na mesma transação, porque o índice único não aceita os dois cadastros com o mesmo documento ao mesmo tempo;
   - o evento `PeopleMergedIntegrationEvent` avisa os módulos que guardarem o id da pessoa.
4. **Anonimização** (`POST /api/v1/people/{id}/anonymize`, permissão nova `crm.people.anonymize`):
   - exige motivo, que fica gravado com a data;
   - troca o nome por "Pessoa anonimizada {id curto}" e apaga documento, e-mail, telefone, observações, inscrições, endereços, contatos e campos customizados;
   - id, papéis, grupo e vínculos ficam, para manter a integridade referencial;
   - nos achados de conflito o nome também é trocado, e nas verificações cujo nome ou documento consultado era o da pessoa, a consulta é apagada;
   - depois disso o cadastro não pode ser editado nem fundido. O evento `PersonAnonymizedIntegrationEvent` avisa os outros módulos;
   - a permissão entra nos perfis padrão Administrador e Sócio pelo catálogo, e nos escritórios existentes pela migration de dados `PeopleAnonymizePermission`.
5. **Filtros de enum na query** (`?role=`, `?risk=`):
   - são lidos sem diferenciar maiúsculas, porque a API serializa enums em camelCase e o binding padrão compara o nome exato. Valor desconhecido responde 400 `crm.filter_invalid`;
   - o contrato OpenAPI continua apontando para o schema do enum, por um transformador de operação.

## Consequências

- **O critério "parte contrária em processo de outro cliente"** só fecha de verdade na Etapa 5, quando `ILitigationLookup` tiver implementação real.
- **LGPD × auditoria:** a trilha `audit.audit_logs` é só de inserção e guarda os valores anteriores das alterações. A anonimização não apaga esses registros. Prazo de retenção e tratamento da trilha para titulares é decisão jurídica a tomar antes de produção.
- **Fusão sem desfazer:** desfazer é um novo cadastro, e o histórico fica na auditoria.
