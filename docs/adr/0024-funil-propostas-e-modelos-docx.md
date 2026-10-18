# ADR 0024 — Funil de oportunidades, propostas de honorários e modelos DOCX

- **Status:** Proposta (para revisão no PR 4C)
- **Data:** 2026-10-18
- **Etapa:** 4 (PR 4C)

## Contexto

O RF-08 pede quatro coisas:
- um funil de oportunidades (lead → reunião → proposta → aceita ou perdida), com origem e motivo de perda;
- propostas de honorários geradas de modelos, com escopo e condições;
- que a proposta aceita gere o cliente e o contrato de honorários;
- indicadores do funil.

A tarefa 6 da Etapa 4 pede modelo DOCX com mesclagem simples agora (o motor completo é da Etapa 9) e o evento `ProposalAccepted`, que o Finance consome na Etapa 10.

Decisões de negócio confirmadas em 2026-10-18:
- **lead:** dados soltos (tipo, nome, documento, e-mail, telefone) ou uma pessoa já cadastrada. A pessoa só nasce na aceitação;
- **modelo:** um modelo padrão do sistema, e o escritório pode enviar o próprio `.docx` com marcadores;
- **aprovação interna:** sempre. Quem elabora não aprova a própria proposta;
- **conflito:** a aceitação exige uma verificação de conflito do lado Cliente feita para o lead e já prosseguida (RF-07).

## Decisão

1. **`Opportunity`** (`crm.opportunities`, RLS, campos customizados `opportunities`):
   - o lead fica sempre gravado na oportunidade, como cópia da pessoa quando há uma ligada. É o que aparece no kanban e o que alimenta a verificação de conflito;
   - outros dados:
     - título;
     - origem: item da tabela auxiliar do sistema `opportunity_origins`;
     - valor estimado;
     - previsão de fechamento;
     - responsável: membro do escritório, conferido por `IMembershipLookup`;
     - observações;
   - etapas:
     - lead, reunião e proposta se movem livremente (`POST /opportunities/{id}/stage`);
     - "perdida" exige motivo da tabela auxiliar do sistema `loss_reasons`, com observação opcional, e substitui as propostas abertas;
     - a oportunidade perdida pode ser reaberta;
     - "aceita" só acontece pela aceitação da proposta e é final;
   - kanban: lista por etapa e responsável, com cursor, e `GET /opportunities/summary` com quantidade e valor estimado por etapa. Os indicadores completos (conversão, tempo de fechamento, receita por origem) ficam para o módulo de relatórios;
   - `POST /opportunities/{id}/conflict-check`:
     - roda a detecção do ADR 0023 com os dados do lead, do lado Cliente, e liga a verificação à oportunidade;
     - prosseguir é pelo `POST /conflict-checks/{id}/proceed` do 4B;
     - mudar o tipo, o nome ou o documento do lead desliga a verificação, que deixa de valer.
2. **`Proposal` e `ProposalItem`** (`crm.proposals` e `crm.proposal_items`, RLS):
   - cada item tem modalidade: fixo, mensal, por hora, êxito ou por ato;
   - nos itens com valor, o valor é quantidade × valor unitário, arredondado a 2 casas;
   - o êxito tem percentual (de 0 a 100) e fica fora do total, porque depende do resultado;
   - moeda BRL; título, escopo, condições, validade e o modelo escolhido (nulo usa o padrão);
   - fluxo:
     - rascunho → aguardando aprovação (`submit`, exige itens) → aprovada (`approve`, com `crm.proposals.approve`) → enviada (`send`) → aceita (`accept`) ou recusada (`decline`);
     - `return` volta para rascunho e tira a aprovação;
     - só o rascunho é editado;
     - quem criou ou enviou para aprovação não aprova (`crm.proposal_self_approval`);
     - validade vencida bloqueia o envio e o aceite;
     - excluir só antes do envio;
   - várias propostas por oportunidade. Aceita uma, as outras abertas ficam `Superseded`.
3. **Aceite** (`POST /proposals/{id}/accept`, numa transação):
   - a proposta precisa estar enviada, e a oportunidade, com verificação de conflito do lado Cliente prosseguida (`crm.conflict_check_required`);
   - o cliente é escolhido nesta ordem:
     - a pessoa ligada à oportunidade;
     - a pessoa com o mesmo documento;
     - um cadastro novo com os dados do lead;
   - em todos os casos a pessoa recebe o papel Cliente;
   - a oportunidade fecha ganha, apontando para o cliente;
   - `ProposalAcceptedIntegrationEvent` vai pelo outbox com os itens (modalidade por nome), o total e o cliente, para o Finance criar o contrato de honorários na Etapa 10. O caso, que é opcional, entra com o módulo de processos (Etapa 5).
4. **Modelos DOCX** (`DocumentFormat.OpenXml` 3.5.1, licença MIT; dependências `DocumentFormat.OpenXml.Framework` e `System.IO.Packaging`, também MIT; só no Crm.Infrastructure, atrás de `IProposalDocuments`):
   - **marcadores** `{{grupo.campo}}`:
     - cliente: `nome`, `documento`, `email`;
     - `oportunidade.titulo`;
     - proposta: `titulo`, `escopo`, `condicoes`, `data`, `validade`, `total`;
     - em uma linha de tabela, que é repetida por item: `item.descricao`, `item.modalidade`, `item.quantidade`, `item.valor`, `item.total`;
   - **onde valem:** no corpo, nos cabeçalhos e nos rodapés;
   - **formatos:** valores em reais com vírgula decimal e datas em dd/mm/aaaa;
   - **marcador partido pelo Word em vários trechos:** o parágrafo é reunido no primeiro trecho, cuja formatação vale para o parágrafo inteiro. O marcador inteiro num trecho é trocado ali mesmo, sem mexer na formatação. Quebras de linha viram `w:br`;
   - **modelo padrão:** montado em código, sem binário no repositório. É conferido pelo `OpenXmlValidator` nos testes e fica disponível em `GET /proposal-templates/default` para o escritório adaptar;
   - **upload** (`POST /proposal-templates`, `multipart/form-data`, até 5 MB):
     - antivírus síncrono (`IFileScanner`);
     - o arquivo precisa abrir como DOCX, sem macros;
     - só são aceitos marcadores conhecidos. Os desconhecidos são listados em `crm.template_unknown_marker`;
     - o arquivo vai para o `IObjectStorage` (`{org}/crm/{id}`);
     - o formulário é lido da requisição, e não por `IFormFile` no parâmetro. Assim o roteamento não responde 415 antes da autenticação. O contrato OpenAPI é declarado por um transformador;
   - **documento da proposta:**
     - enquanto não foi enviada, `GET /proposals/{id}/document` gera o DOCX na hora;
     - no envio, o DOCX é gerado e congelado no armazenamento, e o download passa a devolver o congelado;
     - modelo excluído depois de escolhido faz a proposta sair no modelo padrão.
5. **Tabelas auxiliares novas:**
   - "Origens de oportunidade" e "Motivos de perda", com itens sugeridos;
   - escritórios novos as recebem no onboarding (`LookupTableSeeder`); os já criados, pela migration de dados `OpportunityLookupTables` do Platform;
   - o Platform expõe `ILookupItemValidator`, que confere se o item é ativo de uma tabela do sistema.
6. **Permissões:** as do catálogo, já existentes desde a Etapa 3, sem migration:
   - `crm.opportunities.read` e `crm.opportunities.manage`;
   - `crm.proposals.read`, `crm.proposals.manage` e `crm.proposals.approve`.
   O Advogado tem as de oportunidades e as de ler e gerenciar propostas; Sócio e Administrador também aprovam. Os modelos usam `crm.proposals.manage`.
7. **Fusão e anonimização (ADR 0023):** a fusão faz as oportunidades passarem para o cadastro mantido. A anonimização apaga os dados do lead nas oportunidades ligadas à pessoa.

## Consequências

- **Escritório de uma pessoa só:** com a aprovação interna sempre exigida e quem elabora proibido de aprovar, quem trabalha sozinho não consegue enviar proposta. É uma decisão de negócio pendente. Uma opção é dispensar a regra quando o escritório tem um único membro ativo.
- **Mesclagem simples:** não há laços além da linha de itens, nem condicionais e imagens. Fica para o motor da Etapa 9, que pode substituir a implementação de `IProposalDocuments`.
- **Modelo partido pelo Word:** a formatação mista dentro de um parágrafo com marcador partido se perde, e vale a do primeiro trecho.
- **Arquivo órfão:** se o envio falhar depois de gravar o DOCX no armazenamento, o arquivo fica sem referência. A limpeza de órfãos entra com o GED (Etapa 9).
- **Indicadores do RF-08:** taxa de conversão, tempo de fechamento e receita por origem chegam com os relatórios. O funil já grava etapa, data da mudança, origem e valor.
- **Assinatura eletrônica** da proposta (RF-29) fica para a Etapa 9; o aceite hoje é registrado pelo escritório.
