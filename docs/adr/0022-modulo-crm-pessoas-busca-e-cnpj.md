# ADR 0022 — Módulo Crm: cadastro de pessoas, busca aproximada e consulta de CNPJ

- **Status:** Aceita (PR #18 mergeado em 2026-10-17)
- **Data:** 2026-10-17
- **Etapa:** 4 (PR 4A)

## Contexto

O RF-06 pede um cadastro único de pessoas, física ou jurídica, em que cliente, parte contrária, testemunha, perito, correspondente e fornecedor são papéis da mesma pessoa. O cadastro reúne contatos, dados fiscais, grupos econômicos, detecção de duplicidade e consulta automática de CNPJ. A Etapa 4 define dois critérios de aceite para este PR:
- documento duplicado é bloqueado no mesmo escritório e permitido em outro;
- a busca "Joao da Silva" encontra "João da Silva Ltda.".

Decisão de negócio confirmada em 2026-10-17: a consulta de CNPJ usa a BrasilAPI.

## Decisão

1. **Módulo `Crm`** (schema `crm`, com RLS em todas as tabelas):
   - `people`, com tipo (física ou jurídica), nome civil ou razão social, nome social ou fantasia, documento só com dígitos, e-mail, telefone, observações, dados fiscais (inscrições e retenções de ISS, IR e PIS/COFINS/CSLL), grupo econômico e `custom_fields jsonb`;
   - `person_roles`, `addresses` (CEP e UF validados, endereço de cobrança) e `contacts` (área e canal preferido);
   - `economic_groups`.
   - A pessoa é o agregado: a edição substitui o cadastro inteiro, como a tela envia. O tipo não muda depois de criado.
2. **Documento:**
   - opcional, porque às vezes a parte contrária não tem documento conhecido;
   - conferido conforme o tipo (CPF na física, CNPJ na jurídica);
   - único no escritório entre os cadastros ativos, por índice parcial `(organization_id, document) WHERE document IS NOT NULL AND NOT is_deleted`, mais a checagem no handler, que responde 409 `crm.document_taken`;
   - excluir a pessoa (exclusão lógica) libera o documento.
3. **Busca:**
   - `search_name` guarda o nome e o nome alternativo sem acentos, em minúsculas e sem pontuação, normalizados na aplicação (`SearchText`). O termo buscado passa pela mesma normalização;
   - o PostgreSQL compara com `word_similarity` (pg_trgm, mapeada como função do EF): o termo encontra o nome mesmo dentro de um nome maior, e "joao da silva" em "joao da silva ltda" vale 1;
   - entram no resultado os nomes que contêm o termo, os documentos que começam pelos dígitos digitados e os nomes com semelhança a partir de 0,5. A ordem é documento, depois semelhança;
   - um índice GIN `gin_trgm_ops` em `search_name` atende o `LIKE` da busca;
   - sem termo, a lista vem em ordem alfabética com paginação por cursor.
4. **Possíveis duplicatas:** `GET /api/v1/people/similar` traz o mesmo documento ou a semelhança do nome inteiro (`similarity`) a partir de 0,6. Serve para a tela avisar antes de gravar. Nome parecido não bloqueia o cadastro; só documento igual bloqueia.
5. **Campos customizados de pessoas:** validados pelo `ICustomFieldValidator` (ADR 0021) com a entidade `people`. Os erros voltam em `errors["customFields.{chave}"]`.
6. **Consulta de CNPJ (`ICnpjLookup`):**
   - a interface fica no Crm.Application e a implementação na Infrastructure;
   - **BrasilAPI** (`GET api/cnpj/v1/{cnpj}`), gratuita e sem chave. Resposta 404 ou 400 vira "não encontrado". Erro do servidor, falha de rede ou tempo esgotado (8 s) vira 422 `crm.cnpj_lookup_unavailable`, e o cadastro segue manual;
   - `Crm:CnpjLookup:Provider=Fake` usa uma implementação determinística nos testes e em ambientes sem internet;
   - o `HttpClientFactory` vem do framework compartilhado `Microsoft.AspNetCore.App` (`FrameworkReference`, como no Identity). Não entra pacote novo.
7. **Permissões** (já previstas no catálogo da ADR 0017):
   - `crm.people.read` para lista, detalhe, duplicatas e grupos;
   - `crm.people.create` para criar e consultar CNPJ;
   - `crm.people.edit` para editar e gerenciar grupos;
   - `crm.people.delete` para excluir.

## Consequências

- **Índice de busca:** o GIN de trigramas não começa por `organization_id` (exigiria `btree_gin`). A RLS e o filtro de tenant continuam valendo; se a busca pesar com muitos escritórios, a saída é avaliar `btree_gin` ou o operador `%>` com índice.
- **Fica para o 4B:** fusão de duplicados, conflito de interesses e anonimização LGPD. Exportação de pessoas (`crm.people.export`) ainda não tem endpoint.
- **BrasilAPI sem SLA:** se a disponibilidade incomodar, troca-se de provedor implementando `ICnpjLookup`, sem mudar o módulo.
