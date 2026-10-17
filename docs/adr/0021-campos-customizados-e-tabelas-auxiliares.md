# ADR 0021 — Campos customizados e tabelas auxiliares

- **Status:** Aceita (PR #15 mergeado em 2026-10-17, com os itens sugeridos)
- **Data:** 2026-10-17
- **Etapa:** 3 (PR 3E)

## Contexto

O RF-03 pede dois recursos:
- **campos customizados** por entidade (texto, número, data, lista, moeda e usuário), com validação e obrigatoriedade;
- **tabelas auxiliares** editáveis: tipos de ação, fases, objetos/pedidos, motivos de encerramento, tipos de despesa e tipos de tarefa.

A seção 6.1 define onde isso mora: definições em `platform.custom_field_definitions` e valores na coluna `custom_fields jsonb` de cada entidade, validados na aplicação. As entidades em si só chegam a partir da Etapa 4.

Decisões de negócio confirmadas em 2026-10-17:
- o campo lista tira as opções de uma tabela auxiliar;
- as 6 tabelas nascem com itens sugeridos;
- existe uma permissão de leitura nova, dada a todos os perfis padrão;
- as entidades com campos customizados são processos e casos, pessoas e clientes, contratos de honorários e oportunidades.

## Decisão

1. **Módulo Platform, com RLS** em `custom_field_definitions`, `lookup_tables` e `lookup_items`.
2. **Definição de campo:**
   - **chave** em `snake_case`, única por entidade no escritório;
   - **entidade** (`cases`, `people`, `fee_agreements` ou `opportunities`; o catálogo `CustomFieldEntities` fica em Platform.Contracts);
   - **tipo**, rótulo, ajuda, obrigatório, filtrável, posição e ativo;
   - **limites por tipo:** tamanho máximo do texto (padrão 500, até 4000), mínimo e máximo de número e moeda, casas decimais (número de 0 a 6, moeda sempre 2), e a tabela de um campo lista, com ou sem várias opções;
   - **chave, entidade e tipo não mudam,** e nem a tabela de um campo lista, porque os valores gravados dependem deles;
   - **nada é excluído:** campo, tabela e item são desativados. Um campo inativo some do formulário, deixa de ser obrigatório e mantém o valor gravado.
3. **Formato dos valores no JSON:**
   - texto: string;
   - número e moeda: number;
   - data: `yyyy-MM-dd`;
   - lista: id do item, ou lista de ids quando aceita várias opções;
   - usuário: id do usuário. A pertença é conferida pelo `IMembershipLookup`, com cache.
4. **Validação compartilhada:**
   - `ICustomFieldValidator` (Platform.Contracts) é o que os módulos chamam antes de gravar `custom_fields`;
   - devolve os valores normalizados e os erros por chave, com códigos estáveis `custom_fields.*`;
   - `ToValidationError()` transforma os erros em 400, com `errors["customFields.{chave}"]`;
   - chave desconhecida é erro, para não gravar lixo na coluna;
   - a opção precisa estar ativa **na tabela do próprio campo**;
   - `POST /api/v1/custom-fields/validate` expõe a mesma validação sem gravar nada, para o formulário de teste da tela.
5. **Tabelas do sistema:**
   - as 6 tabelas, com `SystemKey` fixo (`SystemLookupTables`), nascem no onboarding pelo evento novo `OrganizationCreatedIntegrationEvent`, que o Organization publica e o Worker consome no Platform;
   - o seeder é idempotente: tabela que já existe não é tocada;
   - o escritório renomeia as tabelas do sistema, mas não as desativa, porque os módulos dependem delas. Também cria as próprias tabelas para usar em campos lista.
6. **Permissões:**
   - `settings.custom_fields.read` é nova e entra em todos os perfis padrão. Os escritórios já criados recebem a permissão pela migration de dados `CustomFieldsReadPermission`; perfis criados pelo próprio escritório não mudam;
   - criar e editar continuam com `settings.custom_fields.manage`;
   - isso altera a matriz da ADR 0017 só nessa permissão.
7. **Frontend:**
   - `lf-custom-fields` (`shared/ui`) recebe as definições, o `FormGroup` da entidade, as tabelas e os usuários. Cria um controle por chave, com os mesmos limites do servidor;
   - mostra os erros traduzidos (`errors.custom_fields.*`), inclusive os devolvidos pela API (`applyServerErrors`);
   - `customFieldValues` monta o JSON para a API.

**Itens sugeridos para aprovação:**

| Tabela | Itens |
| --- | --- |
| Tipos de ação | Reclamação trabalhista; Ação de cobrança; Ação de indenização por danos morais; Ação de indenização por danos materiais; Execução de título extrajudicial; Execução fiscal; Embargos à execução; Mandado de segurança; Ação revisional de contrato; Ação de despejo; Divórcio; Inventário; Ação de alimentos |
| Fases | Pré-processual; Conhecimento; Recursal; Execução / cumprimento de sentença; Acordo em cumprimento; Arquivado |
| Objetos/pedidos | Horas extras; Verbas rescisórias; Adicional de insalubridade; Adicional de periculosidade; Reconhecimento de vínculo empregatício; Danos morais; Danos materiais; Lucros cessantes; Repetição de indébito; Obrigação de fazer |
| Motivos de encerramento | Acordo; Procedência; Procedência parcial; Improcedência; Extinção sem resolução do mérito; Desistência; Prescrição; Pagamento / satisfação da obrigação; Revogação do mandato |
| Tipos de despesa | Custas iniciais; Custas recursais (preparo); Depósito recursal; Honorários periciais; Diligência de oficial de justiça; Correspondente; Cópias e autenticações; Deslocamento; Postagem |
| Tipos de tarefa | Elaborar petição; Protocolar; Preparar audiência; Reunião com cliente; Analisar documentos; Elaborar cálculo; Diligência; Contato telefônico |

## Consequências

- **Responsabilidade de cada módulo:** cada módulo dono de entidade (Etapas 4, 5 e 10) cria a coluna `custom_fields jsonb`, chama `ICustomFieldValidator` antes de gravar e cria o índice GIN quando houver campo filtrável.
- **Escritórios criados antes deste PR** (só em desenvolvimento) não ganham as tabelas do sistema sozinhos: o evento só sai no onboarding. Para eles, reprocessar com o `LookupTableSeeder`.
- **Sem pacotes novos.**
- **Fica para o PR 3F:** a tela de campos customizados e o e2e do critério de aceite ("campo obrigatório criado na tela aparece e é validado no formulário de teste").
