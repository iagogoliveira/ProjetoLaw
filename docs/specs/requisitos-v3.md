# LexFlow — Especificação de Requisitos v3.0 (ERP Jurídico)

Oct 16, 2026 · @Iago

## 1. Visão do produto

O LexFlow passa a ser um **ERP jurídico completo** — gestão de contencioso, consultivo, contratos, prazos, documentos, finanças e indicadores — com o copiloto de pesquisa e redação por IA como um módulo integrado, não como o produto inteiro.

**Versão:** 3.0 (substitui a v2.0, focada apenas no copiloto de IA) **Tipo de produto:** SaaS de gestão jurídica (ERP) com IA aplicada **Mercado:** advogados autônomos, escritórios de pequeno, médio e grande porte e departamentos jurídicos de empresas no Brasil **Referência de mercado:** soluções como eLaw, Projuris, Astrea e Legal One; o diferencial do LexFlow é a IA com fontes verificáveis embutida em todo o fluxo

### 1.1 Públicos-alvo por porte

| Porte | Perfil típico | O que mais importa |
| --- | --- | --- |
| Autônomo | 1 advogado, até \~200 processos | Prazos, publicações, agenda, financeiro simples, IA para peças |
| Pequeno escritório | 2–15 usuários, até \~3 mil processos | Divisão de trabalho, timesheet, honorários, portal do cliente |
| Médio/grande escritório | 15–1.000+ usuários, dezenas a centenas de milhares de processos, filiais | Workflow, controladoria jurídica, faturamento por cliente, BI, SSO, SLA |
| Departamento jurídico corporativo | Empresa com carteira própria e escritórios terceirizados | Provisionamento contábil, gestão de escritórios credenciados, contratos, societário |

### 1.2 Objetivos

- Centralizar toda a operação jurídica e financeira do escritório em uma única plataforma.
- Eliminar perda de prazo com captura automática de publicações e andamentos, distribuição e controle de cumprimento.
- Dar visibilidade gerencial: produtividade, rentabilidade por cliente/caso, risco e provisões.
- Reduzir o tempo de pesquisa e elaboração de peças com IA referenciada e revisão humana.
- Escalar do advogado autônomo ao grande escritório sem trocar de sistema.
- Monetizar por assinatura, por usuário e por módulo, com consumo de IA medido.

### 1.3 Princípios obrigatórios

1. **Prazo é crítico:** nenhuma publicação ou intimação capturada pode ficar sem triagem rastreável; falhas de captura geram alerta.
2. **Fontes verificáveis:** toda citação jurídica gerada pela IA aponta para uma fonte identificável; jurisprudência não confirmada nunca é apresentada como real.
3. **Rastreabilidade:** separar fatos do usuário, conteúdo de documentos, pesquisa externa e inferências da IA; auditar ações relevantes.
4. **Revisão humana:** peças, cálculos de prazo sugeridos e classificações de risco feitas por IA são sugestões até confirmação de um responsável.
5. **Privacidade e sigilo profissional por padrão:** isolamento total entre organizações e controle de acesso por caso dentro de cada organização (incluindo muralha ética).
6. **Configurável, não customizado:** necessidades de grandes clientes são atendidas por configuração (campos, workflows, relatórios, permissões), sem forks de código.

## 2. Edições e escalabilidade

Um único código-base atende todos os portes; o que muda entre edições são os módulos habilitados, os limites e os recursos de governança.

| Recurso | Solo | Escritório | Corporativo (Enterprise) |
| --- | --- | --- | --- |
| Usuários | 1–2 | 3–50 | Ilimitado (por licença) |
| Processos ativos | até 500 | até 20 mil | Sem limite técnico prático |
| Captura de publicações e andamentos | Sim (OABs do titular) | Sim (várias OABs) | Sim + regras de distribuição automática |
| Agenda, prazos, tarefas | Sim | Sim | Sim + workflows configuráveis e SLA |
| GED e modelos | Básico | Completo | Completo + retenção e assinatura em lote |
| Financeiro | Honorários e despesas | + timesheet, faturamento, NFS-e | + rateio por centro de custo, multimoeda, aprovações |
| Controladoria jurídica | — | Básica | Completa (fila, qualidade, auditoria de cumprimento) |
| Provisionamento e risco | — | Opcional | Sim (CPC 25 / IAS 37) |
| Gestão de escritórios terceirizados | — | — | Sim |
| Portal do cliente | — | Sim | Sim + white label |
| BI e relatórios | Painéis padrão | + relatórios customizáveis | + exportação para data warehouse e API de BI |
| IA (pesquisa e minutas) | Cota mensal | Cota por usuário | Cota contratada + modelo/região dedicados |
| SSO (SAML/OIDC), SCIM | — | Opcional | Sim |
| Filiais, áreas e centros de custo | — | Sim | Sim, hierárquico |
| Ambiente de homologação (sandbox) | — | — | Sim |
| SLA de disponibilidade | 99,5% | 99,5% | 99,9% contratual |

Requisitos de escala para a edição Corporativa:

- Suportar organizações com 500 mil+ processos e 5 milhões+ de documentos sem degradação das telas principais.
- Importação em massa de carteira (planilha, CSV, API) com validação, pré-visualização e relatório de erros.
- Operações em lote: redistribuir responsáveis, alterar status, gerar relatórios e baixar documentos de milhares de processos.
- Opção de isolamento físico (banco dedicado) para clientes que exigirem.

## 3. Estrutura organizacional, perfis e permissões

O acesso combina papel (RBAC) com atributos do caso e da estrutura (ABAC), sempre aplicado no servidor e sempre filtrado por `OrganizationId`.

### 3.1 Estrutura organizacional

- **Organização** (tenant): o escritório ou empresa contratante.
- **Filial/unidade:** cidade ou escritório físico, com dados fiscais próprios (CNPJ, inscrição municipal) para faturamento.
- **Área de atuação/núcleo:** trabalhista, cível, tributário, empresarial etc.
- **Equipe:** grupo de usuários que compartilha carteira e fila de trabalho.
- **Centro de custo:** usado em rateio de despesas, timesheet e rentabilidade.
- **Sócios e participação:** percentual por sócio para relatórios de distribuição de resultado.

### 3.2 Perfis padrão (editáveis por organização)

| Perfil | Escopo principal |
| --- | --- |
| Administrador da plataforma | Planos, limites, suporte; acesso a dados de clientes só com autorização registrada e temporária |
| Administrador do escritório | Usuários, perfis, estrutura, assinatura, configurações e integrações |
| Sócio | Visão completa da sua área ou do escritório; aprovações financeiras; BI |
| Advogado | Casos sob sua responsabilidade ou equipe; peças, prazos, timesheet |
| Estagiário/paralegal | Tarefas atribuídas, cadastro e anexos; sem aprovação ou exclusão |
| Controladoria jurídica | Triagem de publicações, cadastro de prazos, distribuição, conferência de cumprimento |
| Financeiro | Contas, faturamento, NFS-e, conciliação; sem conteúdo jurídico por padrão |
| Cliente (portal) | Somente seus casos, documentos liberados, faturas e relatórios |
| Correspondente/escritório parceiro | Somente diligências e casos delegados a ele |

### 3.3 Regras de permissão

- Permissões granulares por módulo e ação (ver, criar, editar, excluir, aprovar, exportar).
- Escopo de dados: próprio, equipe, área, filial ou organização inteira.
- **Casos sigilosos e muralha ética:** um caso pode restringir acesso a uma lista nominal, sobrepondo o perfil; tentativas de acesso negado são auditadas.
- Delegação temporária (férias, licenças) com data de expiração.
- Aprovações configuráveis por alçada (ex.: acordo acima de R$ 50 mil exige sócio; despesa acima de R$ 2 mil exige financeiro).
- Usuários externos (cliente, correspondente) nunca enxergam dados fora do que foi explicitamente compartilhado.

## 4. Mapa de módulos

O LexFlow é organizado em seis módulos de negócio e três serviços transversais, todos sobre um núcleo comum; cada edição habilita um subconjunto deles.

&#91;embedded content: mapa de módulos · 9 módulos em 4 camadas\]

A IA deixa de ser o produto e vira um serviço usado por todos os módulos: triagem de publicações, extração de dados, minutas e relatórios.

## 5. Requisitos funcionais — Núcleo da plataforma

### RF-01 — Autenticação e acesso

- Cadastro, login, logout, recuperação de senha e convites por e-mail.
- MFA obrigatório configurável por organização (TOTP e e-mail; WebAuthn/passkeys na edição Corporativa).
- SSO via SAML 2.0 e OIDC (Microsoft Entra ID, Google Workspace, Okta) e provisionamento SCIM.
- Política de senha, expiração de sessão, bloqueio por tentativas e restrição por IP/horário configuráveis.
- Gestão de sessões ativas e encerramento remoto.

### RF-02 — Multi-tenant e estrutura

- Isolamento por `OrganizationId` em todas as consultas, filas, arquivos, índices de busca e caches.
- Cadastro de filiais, áreas, equipes, centros de custo e sócios (seção 3).
- Usuário pode pertencer a mais de uma organização (ex.: advogado associado a dois escritórios), com troca de contexto explícita.

### RF-03 — Configuração e personalização

- **Campos customizados** por entidade (processo, cliente, contrato etc.): texto, número, data, lista, moeda, usuário, com validação e obrigatoriedade por tipo.
- **Tabelas auxiliares** editáveis: tipos de ação, fases, objetos/pedidos, motivos de encerramento, tipos de despesa, tipos de tarefa.
- **Layouts de tela** por perfil: quais abas e campos aparecem para cada tipo de caso.
- Numeração interna (pasta) configurável por área/filial.
- Calendário de feriados nacionais, estaduais, municipais e forenses por tribunal, mantido pela plataforma e complementável pelo escritório.

### RF-04 — Busca global e navegação

- Busca unificada por nome, CPF/CNPJ, número CNJ, pasta, parte contrária e conteúdo de documentos (respeitando permissões).
- Filtros salvos e compartilháveis; visões em lista, kanban e calendário.
- Histórico de itens recentes e favoritos.

### RF-05 — Notificações

- Canais: in-app, e-mail, push (PWA), WhatsApp Business (opcional) e webhooks.
- Preferências por usuário e regras obrigatórias por organização (ex.: prazo fatal sempre notifica responsável e coordenador).
- Resumo diário configurável (agenda do dia, prazos da semana, publicações pendentes).

## 6. Requisitos funcionais — Pessoas e CRM

### RF-06 — Clientes, contatos e partes

- Cadastro único de **pessoas** (física/jurídica) com papéis: cliente, parte contrária, testemunha, perito, correspondente, fornecedor.
- Grupos econômicos: cliente matriz com filiais/CNPJs vinculados e consolidação de carteira e faturamento.
- Contatos por cliente (financeiro, jurídico interno, diretoria), com preferência de canal.
- Dados fiscais para faturamento: CNPJ/CPF, inscrições, endereço de cobrança, retenções aplicáveis (ISS, IR, PIS/COFINS/CSLL).
- Consulta automática de CNPJ (Receita Federal) para preenchimento e atualização.
- Detecção de duplicidade por documento e nome aproximado, com fusão de cadastros auditada.
- Arquivamento e anonimização conforme política de retenção (LGPD).

### RF-07 — Conflito de interesses

- Antes de abrir novo cliente ou caso, pesquisar o nome/documento em clientes, partes contrárias e grupos econômicos.
- Exibir resultado com risco (direto, indireto, nenhum) e exigir justificativa registrada para prosseguir em caso de alerta.
- Relatório de verificações realizadas para auditoria e compliance da OAB.

### RF-08 — Prospecção, propostas e contratação

- Funil de oportunidades: lead → reunião → proposta → aceita/perdida, com origem e motivo de perda.
- Gerador de propostas de honorários a partir de modelos, com tabelas de preço, escopo e condições.
- Proposta aceita gera automaticamente cliente, contrato de honorários e, opcionalmente, o caso.
- Assinatura eletrônica da proposta/contrato de honorários (RF-29).
- Indicadores: taxa de conversão, tempo de fechamento, receita prevista por origem.

## 7. Requisitos funcionais — Contencioso

### RF-09 — Processos e casos

- Cadastro com número CNJ (validação de dígito verificador), pasta interna, tribunal, instância, comarca, vara/órgão, rito, classe, assunto, área, fase, status, valor da causa e datas (distribuição, citação, encerramento).
- Polo e partes (cliente, contrárias, advogados contrários com OAB), responsáveis (sócio, advogado, equipe) e cliente pagador.
- Hierarquia de processos: principal, incidentes, recursos, cumprimento de sentença, apensos; navegação entre instâncias.
- Casos sem processo judicial (administrativos, PROCON, extrajudiciais) com o mesmo modelo.
- Encerramento com motivo, resultado (procedente, improcedente, acordo, extinto) e valores finais.
- Linha do tempo única: andamentos, publicações, prazos, tarefas, documentos, peças, despesas e horas.

### RF-10 — Captura de publicações e intimações

- Captura diária de Diários Oficiais (DJEN, DJe estaduais/federais) por OAB, nome e CNPJ, via fornecedor licenciado de recortes.
- Captura de intimações eletrônicas dos sistemas judiciais (PJe, e-SAJ, Projudi, eproc) via integração autorizada ou fornecedor.
- Vinculação automática ao processo pelo número CNJ; publicações sem vínculo vão para fila de tratamento.
- **Fila de triagem da controladoria:** cada publicação é lida, classificada (intimação com prazo, mero expediente, audiência, decisão, sentença) e baixada com registro de quem tratou.
- IA sugere classificação, prazo e tarefa (ex.: “contestação — 15 dias úteis”), sempre exigindo confirmação humana.
- Monitoramento da própria captura: alerta se uma OAB ou diário não retornar dados no dia.

### RF-11 — Andamentos processuais (push)

- Acompanhamento automático de movimentações por número CNJ (DataJud e fornecedores de andamento).
- Registro da origem e da data de cada atualização; andamentos manuais permitidos.
- Regras de alerta por tipo de movimentação (ex.: sentença, trânsito em julgado, expedição de alvará).

### RF-12 — Audiências e diligências

- Agendamento de audiências (presencial, virtual, híbrida) com link, local, preposto, testemunhas e advogado designado.
- Checklist pré-audiência e ata/resultado pós-audiência.
- Solicitação de diligências e audiências a **correspondentes**: cotação, aceite, execução, comprovante, avaliação e pagamento.

### RF-13 — Pedidos, valores e cálculos

- Cadastro dos pedidos/objetos do processo (ex.: horas extras, danos morais) com valor pedido, valor estimado e valor de condenação por instância.
- Atualização monetária e juros por índices oficiais (IPCA-E, SELIC, INPC, TR) com tabelas mantidas pela plataforma.
- Histórico das alterações de valor para auditoria.

### RF-14 — Risco e provisionamento contábil

- Classificação de risco por processo e por pedido: provável, possível, remoto (CPC 25 / IAS 37).
- Cálculo de provisão e passivo contingente; fechamento mensal com trava do período e relatório para a contabilidade.
- Workflow de revisão periódica (ex.: trimestral) com aprovação do responsável.
- Exportação para ERP contábil (SAP, TOTVS, Oracle) via arquivo ou API.

### RF-15 — Garantias, depósitos e alvarás

- Registro de depósitos judiciais, recursais, penhoras, seguros-garantia e cartas de fiança, com vencimentos e renovações.
- Controle de levantamento (alvarás) e conciliação com o financeiro.

### RF-16 — Acordos

- Proposta, contraproposta, alçada de aprovação, homologação e parcelas.
- Parcelas geram contas a receber/pagar e alertas de inadimplência.
- Indicadores de economia obtida (valor pedido × valor acordado).

### RF-17 — Gestão de escritórios terceirizados (edição Corporativa)

- Departamento jurídico distribui casos a escritórios credenciados, que acessam o LexFlow com escopo restrito.
- Regras de distribuição por região, área e carga; SLA de cadastro e de resposta.
- Avaliação de desempenho (êxito, prazos cumpridos, custo) e conferência de faturas por tabela de honorários contratada.

## 8. Requisitos funcionais — Consultivo, contratos e societário

### RF-18 — Consultivo

- Registro de consultas/pareceres com solicitante, área, prazo interno, complexidade e status.
- Fila de demandas para departamentos jurídicos com SLA por tipo de solicitação e formulário de abertura pelo cliente interno.
- Vínculo com horas, documentos e faturamento.

### RF-19 — Gestão de contratos (CLM)

- Ciclo de vida: solicitação → elaboração (a partir de modelo) → negociação com versões → aprovação → assinatura → vigência → renovação/encerramento.
- Cláusulas-padrão em biblioteca reutilizável; comparação entre versões.
- Datas críticas (vencimento, reajuste, aviso prévio) geram prazos e alertas automáticos.
- Obrigações contratuais com responsável e evidência de cumprimento.
- IA extrai partes, valores, vigência e cláusulas de risco de contratos enviados (com revisão humana).

### RF-20 — Societário

- Cadastro de empresas do grupo, quadro societário, administradores, capital e participações (organograma societário).
- Controle de atos societários (atas, alterações contratuais), registros na Junta Comercial e certidões com vencimento.
- Calendário de obrigações (AGO, entrega de livros, renovação de mandatos).

### RF-21 — Procurações e documentos de representação

- Cadastro de procurações e substabelecimentos com outorgantes, outorgados, poderes e validade.
- Alertas de vencimento e consulta rápida de quem pode representar o cliente em cada ato.

### RF-22 — Propriedade intelectual (módulo opcional)

- Marcas, patentes e desenhos industriais com número de processo no INPI, classes, titular e status.
- Captura de publicações da Revista da Propriedade Industrial (RPI) e prazos de prorrogação.

## 9. Requisitos funcionais — Agenda, prazos, tarefas e workflow

### RF-23 — Prazos processuais

- Cadastro de prazo com data de publicação/intimação, regra de contagem (dias úteis/corridos, em dobro para Fazenda/Defensoria, litisconsortes com procuradores diferentes), data interna (meta) e data fatal.
- **Calculadora de prazos** com feriados forenses por tribunal e suspensões (recesso de 20/12 a 20/01, indisponibilidades de sistema); o resultado é sugestão e exige confirmação de quem cadastra.
- Dupla conferência opcional: prazo cadastrado por um usuário e validado por outro (padrão em grandes escritórios).
- Status: pendente, em andamento, cumprido (com evidência: protocolo ou peça), perdido (com justificativa obrigatória e alerta à coordenação).
- Painel de prazos por responsável, equipe e período, com semáforo de urgência.

### RF-24 — Agenda e tarefas

- Agenda unificada (prazos, audiências, reuniões, tarefas) por usuário, equipe e escritório.
- Sincronização bidirecional com Google Calendar e Microsoft 365.
- Tarefas com responsável, prazo, prioridade, checklist, subtarefas, comentários e anexos.
- Visões em lista, kanban e calendário; redistribuição em lote.

### RF-25 — Workflow e automações

- Editor visual de fluxos por tipo de caso (ex.: “novo processo trabalhista”: cadastro → análise de risco → defesa → audiência), com etapas, responsáveis, SLA e condições.
- Gatilhos: novo caso, publicação classificada, mudança de fase, prazo próximo, documento recebido, fatura vencida.
- Ações: criar tarefa/prazo, notificar, mudar status, atribuir responsável, gerar documento por modelo, chamar webhook.
- Distribuição automática de trabalho por regra (área, carga atual, rodízio).
- Histórico de execução de cada automação para diagnóstico.

### RF-26 — Controladoria jurídica

- Painel único da controladoria: publicações não tratadas, prazos sem responsável, prazos sem evidência de cumprimento, casos sem andamento há X dias.
- Indicadores de qualidade: tempo médio de triagem, prazos cadastrados após o limite interno, retrabalho.
- Relatório diário de fechamento (todas as publicações do dia tratadas) com assinatura do responsável.

## 10. Requisitos funcionais — Documentos (GED), modelos e assinatura

### RF-27 — Gestão eletrônica de documentos

- Upload de PDF, DOCX, XLSX, imagens, e-mails (.eml/.msg), áudio e vídeo; limite configurável por plano.
- Estrutura de pastas por caso/cliente, tags, tipo documental e metadados; versões com autor e data.
- Antivírus em todo upload; validação de extensão e conteúdo real (magic bytes).
- Extração de texto e OCR assíncronos; indexação para busca de conteúdo.
- Visualização no navegador sem download; download por link temporário assinado.
- Check-in/check-out para edição; integração com edição via Microsoft 365/Google Docs (opcional).
- Captura de e-mails: encaminhar para endereço do caso ou suplemento para Outlook/Gmail.
- Política de retenção e descarte por tipo documental; lixeira com restauração por 30 dias.
- Compartilhamento externo controlado (cliente, perito) com expiração e registro de acesso.

### RF-28 — Modelos e geração de documentos

- Biblioteca de modelos (petições, procurações, contratos, cartas, relatórios) com campos de mesclagem (`{{cliente.nome}}`, `{{processo.numero}}`).
- Geração individual ou em lote (ex.: 300 procurações para uma carteira).
- Controle de versão e aprovação de modelos pela coordenação.

### RF-29 — Assinatura eletrônica

- Integração com provedores de assinatura eletrônica e ICP-Brasil (certificado A1/A3 via provedor).
- Envio, acompanhamento de status, lembretes e armazenamento do documento assinado com trilha de evidências.
- Assinatura em lote para sócios.

## 11. Requisitos funcionais — Financeiro

### RF-30 — Contratos de honorários

- Modalidades: valor fixo (mensal/por caso), por hora (tabela por profissional/cargo), êxito (% sobre benefício econômico), por ato/fase, partido + êxito, e híbridos.
- Tabelas de preço por cliente, com reajuste anual por índice e data-base.
- Limites (caps) de horas ou valor por caso e alerta ao atingir percentuais.
- Honorários de sucumbência: previsão, recebimento e regra de distribuição entre advogados/sócios.

### RF-31 — Timesheet

- Lançamento de horas por caso/cliente/atividade, com cronômetro, lançamento rápido e lançamento sugerido a partir da atividade no sistema (peças editadas, audiências, e-mails).
- Códigos de atividade (inclusive padrões UTBMS/LEDES para clientes internacionais).
- Aprovação pelo responsável do caso; horas não faturáveis e internas classificadas.
- Travamento de período após fechamento.

### RF-32 — Despesas e custas

- Lançamento de custas, diligências, deslocamentos e despesas reembolsáveis com comprovante.
- Adiantamento e prestação de contas por advogado/correspondente.
- Marcação de reembolsável pelo cliente e inclusão automática na pré-fatura.
- Geração de guias de custas (quando o tribunal disponibilizar integração) e controle de pagamento.

### RF-33 — Faturamento

- Pré-fatura por cliente/caso/período reunindo honorários, horas aprovadas e despesas reembolsáveis.
- Revisão pelo sócio: ajustes, descontos e baixas (write-off) com motivo.
- Emissão de fatura, boleto e Pix (com conciliação automática) e cobrança recorrente para contratos mensais.
- **NFS-e** emitida automaticamente por filial, com retenções corretas, cancelamento e substituição; adequação ao padrão nacional da NFS-e e à transição da reforma tributária (IBS/CBS).
- Relatório de faturamento detalhado para o cliente (com descrições das horas), exportação LEDES.
- Régua de cobrança automática (e-mail/WhatsApp) e controle de inadimplência.

### RF-34 — Contas a pagar, a receber e tesouraria

- Contas a pagar (fornecedores, correspondentes, aluguel, folha) com aprovação por alçada.
- Contas a receber com baixa manual ou automática.
- Conciliação bancária por OFX e Open Finance; múltiplas contas e filiais.
- Fluxo de caixa realizado e projetado; plano de contas e DRE gerencial por área, filial e centro de custo.
- Exportação para a contabilidade (arquivo padrão e integração com sistemas contábeis).

### RF-35 — Repasses e remuneração

- Cálculo de repasse a correspondentes e escritórios parceiros.
- Participação de advogados em êxito/sucumbência conforme regra cadastrada.
- Relatório de distribuição de resultado por sócio.

### RF-36 — Rentabilidade

- Rentabilidade por cliente, caso, área e profissional: receita × custo (horas × custo-hora + despesas).
- Realização: horas trabalhadas × faturadas × recebidas.

## 12. Requisitos funcionais — IA jurídica (LexFlow AI)

Todo o escopo de IA da v2.0 (RF-05 a RF-09 antigos) continua válido e passa a usar o contexto do ERP: o caso, as partes, os documentos do GED, os pedidos e o histórico já estão no sistema.

### RF-37 — Pesquisa jurídica

- Pesquisa de legislação e jurisprudência em fontes oficiais ou fornecedores licenciados, com filtros por tribunal, classe, assunto, período e tipo de fonte.
- Registro de consulta, data, fonte e metadados confirmados (tribunal, número, relator, órgão julgador, data, URL).
- Diferenciar inteiro teor, ementa, notícia, metadado e texto legal; informar quando a fonte não puder ser confirmada.
- Pesquisas salvas no caso e reaproveitáveis pela equipe; base interna de teses e peças do próprio escritório (RAG privado por organização).
- DataJud serve para metadados e movimentações, não como base universal de inteiro teor.

### RF-38 — Elaboração de peças com fundamentação verificável

- Solicitação a partir do caso: tipo de peça, objetivo, fatos (pré-preenchidos do cadastro), documentos do GED, tribunal, teses a usar/evitar, extensão e estilo.
- Resumo prévio das informações consideradas para correção antes da geração.
- Fluxo: extrair fatos → separar fatos de inferências → identificar questões e teses → pesquisar → **verificar existência e metadados das fontes** → avaliar pertinência e atualidade → estruturar → redigir com marcadores de citação → listar alertas e lacunas.
- A minuta distingue: fundamento confirmado, fundamento a confirmar e argumento baseado em fatos do advogado.
- Fonte não localizada pode virar sugestão de pesquisa, nunca precedente verificado.
- Modelos e estilo do próprio escritório (RF-28) usados como referência de estrutura.

### RF-39 — Conferência de fontes, editor e versões

- Tela lado a lado: trecho da minuta, fonte, metadados confirmados, trecho de suporte, link original e status (confirmado, parcial, desatualizado, não confirmado), com ações aceitar/remover/substituir.
- Editor com versões, regeneração por seção sem sobrescrever o restante, exportação DOCX/PDF e aviso de minuta assistida por IA.
- Peça aprovada é salva no GED do caso e pode cumprir um prazo (evidência do RF-23).

### RF-40 — IA aplicada ao ERP

- Classificação de publicações e sugestão de prazo/tarefa (RF-10).
- Resumo do caso e da linha do tempo para quem assume o processo ou para o relatório ao cliente.
- Extração de dados de petições iniciais recebidas (partes, pedidos, valores) para pré-cadastro do processo.
- Extração de dados de contratos (RF-19).
- Sugestão de lançamento de timesheet a partir da atividade.
- Sugestão de classificação de risco com justificativa, sempre sujeita a aprovação (RF-14).
- Assistente conversacional sobre os dados do escritório (“quais prazos fatais da equipe trabalhista vencem nesta semana?”), respeitando as permissões do usuário.

### RF-41 — Governança e consumo de IA

- Cotas por plano, organização e usuário; consumo por recurso e solicitação; bloqueio ou autorização antes de exceder limites.
- Não cobrar duas vezes uma solicitação que falhou.
- Administrador do escritório pode desativar IA por área, caso sigiloso ou cliente.
- Documentos tratados como dados não confiáveis (proteção contra prompt injection).
- Registro de modelo, versão de prompt, fontes recuperadas e parâmetros por geração.

## 13. Requisitos funcionais — BI, portal, integrações e administração

### RF-42 — BI e relatórios

- Painéis padrão: carteira (novos × encerrados, por área/fase/tribunal), prazos (cumpridos, em risco, perdidos), produtividade por profissional, financeiro (faturado, recebido, inadimplência, rentabilidade), contingência e provisão, êxito por tese/vara/escritório parceiro.
- Gerador de relatórios com filtros, agrupamentos, colunas customizadas e campos customizados; agendamento por e-mail.
- Exportação XLSX, CSV e PDF; relatórios gerenciais para cliente com marca do escritório.
- Edição Corporativa: conector para Power BI e exportação incremental para data warehouse.

### RF-43 — Portal do cliente

- Acesso do cliente aos casos liberados: status em linguagem simples, próximas audiências, documentos compartilhados, faturas e relatórios.
- Envio de documentos e mensagens pelo cliente vinculados ao caso.
- Aprovação de propostas, acordos e faturas pelo portal.
- White label (domínio e marca do escritório) na edição Corporativa.
- Aplicativo/PWA responsivo para clientes e advogados.

### RF-44 — Integrações

| Integração | Uso | Observação |
| --- | --- | --- |
| DataJud (CNJ) | Metadados e movimentações | API pública; não traz inteiro teor de decisões |
| Fornecedor de publicações e andamentos | Recortes de diários e push de movimentações | Contrato licenciado; redundância com 2º fornecedor no Corporativo |
| PJe, e-SAJ, Projudi, eproc | Intimações, consulta e, futuramente, protocolo | Via MNI/APIs oficiais ou parceiro; sem automação irrestrita de sites |
| Fornecedor de jurisprudência | Pesquisa da IA | Licença que permita uso em IA |
| Prefeituras / NFS-e nacional | Emissão de notas | Via emissor intermediário |
| Bancos, Pix, boleto, Open Finance | Cobrança e conciliação |  |
| Assinatura eletrônica e ICP-Brasil | Contratos, procurações |  |
| Google Workspace / Microsoft 365 | Agenda, e-mail, documentos, SSO |  |
| WhatsApp Business API | Notificações e cobrança | Opt-in do destinatário |
| ERP contábil (SAP, TOTVS etc.) | Provisões e lançamentos | Edição Corporativa |
| Receita Federal (CNPJ) | Cadastro de pessoas jurídicas |  |

- **API pública do LexFlow** (REST, OAuth 2.0 client credentials) e **webhooks** para clientes integrarem seus sistemas.
- Importação de dados de outros sistemas jurídicos (planilhas e conectores para concorrentes mais comuns) — essencial para migração de grandes escritórios.

### RF-45 — Administração da plataforma, assinatura e auditoria

- Planos, módulos, limites, preços e estado das assinaturas; cobrança por usuário/módulo/consumo.
- Tela do administrador do escritório: usuários, licenças, consumo de IA, armazenamento e faturas do LexFlow.
- Trilha de auditoria imutável: logins, alterações de permissão, visualização de casos sigilosos, exportações, exclusões, alterações de prazo e valores, acessos do suporte.
- Exportação completa dos dados da organização e encerramento de conta com política de retenção/exclusão.
- Logs técnicos sem conteúdo integral de documentos ou peças.
- Central de suporte, base de conhecimento e status da plataforma.

## 14. Requisitos não funcionais

### Segurança e privacidade

- HTTPS (TLS 1.2+) em trânsito, criptografia em repouso; chaves por organização (BYOK) opcional na edição Corporativa.
- Segredos em cofre dedicado; nenhum segredo em código ou configuração versionada.
- Autorização sempre no servidor; testes automatizados de isolamento entre organizações e entre casos sigilosos.
- Proteção OWASP Top 10, WAF, rate limiting, antivírus em uploads e varredura de dependências no CI.
- Pentest externo anual e antes de cada lançamento maior.
- Roadmap de certificação ISO 27001 e relatório SOC 2 para atender grandes escritórios e empresas.
- LGPD: definição de controlador/operador, DPA com clientes e subprocessadores, registro de operações, atendimento a titulares, plano de resposta a incidentes e comunicação à ANPD.
- Dados hospedados no Brasil por padrão; provedores de IA avaliados quanto a retenção, treinamento e localização; nenhum dado de cliente usado para treinar modelos sem autorização expressa.

### Desempenho e escala

| Métrica | Meta |
| --- | --- |
| Telas de listagem e detalhe (p95) | < 1,5 s com 500 mil processos na organização |
| Busca global (p95) | < 2 s |
| Processamento da captura diária de publicações | Concluído até 7h (horário de Brasília) |
| Usuários simultâneos por organização | 1.000+ |
| Upload | Até 500 MB por arquivo (Corporativo) |

### Disponibilidade e continuidade

- SLA 99,5% (Solo/Escritório) e 99,9% (Corporativo), com página pública de status.
- Backups automáticos com retenção de 35 dias, testes de restauração mensais; RPO ≤ 15 min e RTO ≤ 4 h.
- Processamento assíncrono com filas, retentativas limitadas, idempotência e dead-letter para captura, OCR, IA, NFS-e e notificações.
- Degradação elegante quando fornecedores externos falham (publicações, tribunais, IA), com alerta e reprocessamento.

### Observabilidade

- Logs estruturados, métricas e rastreamento distribuído (OpenTelemetry).
- Alertas de negócio, não só técnicos: captura sem retorno, fila de publicações acumulada, falha de emissão de NFS-e.

### Usabilidade e acessibilidade

- Interface responsiva (desktop prioritário, uso pleno em tablet e celular via PWA).
- WCAG 2.1 AA; atalhos de teclado para operações repetitivas da controladoria.
- Onboarding guiado por porte e importação assistida.

### Qualidade da IA

- Conjunto de avaliação revisado por advogados; métricas de precisão de citações, pertinência, cobertura e taxa de referências não confirmadas.
- Testes com fontes contraditórias, documentos incompletos, legislação revogada e instruções maliciosas em arquivos.
- Uma resposta fluente nunca é tratada como evidência de correção jurídica.

## 15. Arquitetura técnica, modelo de dados e APIs

A arquitetura continua sendo um **monólito modular** em .NET + Angular, agora com fronteiras explícitas por módulo de negócio para permitir extrair serviços (captura, IA, faturamento) só quando o volume justificar.

### 15.1 Stack

- **Frontend:** Angular + TypeScript, PWA, biblioteca de componentes própria com design system; editor rico para peças e modelos.
- **Backend:** ASP.NET Core Web API (C#), EF Core, SQL Server (transacional), Hangfire ou Worker Services para jobs.
- **Busca:** índice de texto completo (Elasticsearch/OpenSearch ou Azure AI Search) para processos, pessoas e conteúdo de documentos.
- **Arquivos:** armazenamento de objetos privado compatível com S3 ou Azure Blob, com links temporários.
- **Mensageria:** fila (RabbitMQ, Azure Service Bus ou similar) obrigatória nesta versão, por causa de captura, OCR, IA, NFS-e e notificações.
- **Cache:** Redis para sessões, permissões calculadas e limites de uso.
- **IA:** camada de abstração de provedores; banco vetorial (pgvector, Azure AI Search ou similar) particionado por organização.
- **Autenticação:** ASP.NET Core Identity + OpenIddict ou provedor OIDC externo (para SSO/SCIM).
- **Infra:** containers (Kubernetes ou Azure Container Apps), IaC (Terraform/Bicep), região Brasil.

### 15.2 Estrutura da solução

```text
src/
  LexFlow.Api/                 # host HTTP, versionamento, autenticação
  LexFlow.Worker/              # jobs e consumidores de fila
  LexFlow.SharedKernel/        # tenant, auditoria, eventos, resultados
  Modules/
    LexFlow.Identity/          # usuários, perfis, SSO, permissões
    LexFlow.Organization/      # filiais, áreas, equipes, centros de custo
    LexFlow.Crm/               # pessoas, conflito, oportunidades
    LexFlow.Litigation/        # processos, pedidos, risco, garantias, acordos
    LexFlow.Publications/      # captura, triagem, andamentos
    LexFlow.Advisory/          # consultivo, contratos, societário, procurações
    LexFlow.Scheduling/        # prazos, agenda, tarefas, calculadora
    LexFlow.Workflow/          # motor de fluxos e automações
    LexFlow.Documents/         # GED, modelos, assinatura
    LexFlow.Finance/           # honorários, timesheet, faturamento, contas
    LexFlow.AI/                # pesquisa, RAG, minutas, verificação, uso
    LexFlow.Reporting/         # relatórios e BI
    LexFlow.Portal/            # portal do cliente e parceiros
    LexFlow.Integrations/      # conectores externos e webhooks
    LexFlow.Billing/           # assinatura SaaS do próprio LexFlow
  LexFlow.Web/                 # Angular
tests/
  LexFlow.UnitTests/
  LexFlow.IntegrationTests/
  LexFlow.SecurityTests/       # isolamento de tenant e casos sigilosos
  LexFlow.ArchitectureTests/   # regras de dependência entre módulos
```

Cada módulo tem seu próprio schema no SQL Server, expõe contratos públicos e se comunica com os demais por interfaces ou eventos de domínio — nunca acessando tabelas de outro módulo.

### 15.3 Modelo de dados — entidades principais por módulo

| Módulo | Entidades |
| --- | --- |
| Identity / Organization | `Organization`, `Branch`, `PracticeArea`, `Team`, `CostCenter`, `User`, `OrganizationMember`, `Role`, `Permission`, `AccessRestriction` |
| CRM | `Person`, `PersonRole`, `EconomicGroup`, `Contact`, `ConflictCheck`, `Opportunity`, `Proposal` |
| Litigation | `LegalCase`, `CaseParty`, `CaseAssignment`, `CaseRelation`, `Claim` (pedido), `ClaimValuation`, `RiskAssessment`, `Provision`, `Guarantee`, `Settlement`, `SettlementInstallment`, `Hearing`, `Diligence` |
| Publications | `Publication`, `PublicationTriage`, `CaseMovement`, `CaptureSource`, `CaptureRun` |
| Advisory | `AdvisoryRequest`, `Contract`, `ContractVersion`, `ContractObligation`, `Company`, `Shareholding`, `CorporateAct`, `PowerOfAttorney`, `IpAsset` |
| Scheduling / Workflow | `Deadline`, `DeadlineRule`, `Holiday`, `CalendarEvent`, `Task`, `WorkflowDefinition`, `WorkflowInstance`, `AutomationRule` |
| Documents | `Document`, `DocumentVersion`, `DocumentExtraction`, `Folder`, `DocumentTemplate`, `SignatureRequest` |
| Finance | `FeeAgreement`, `RateTable`, `TimeEntry`, `Expense`, `PreInvoice`, `Invoice`, `ServiceInvoice` (NFS-e), `Receivable`, `Payable`, `BankAccount`, `BankTransaction`, `PayoutRule` |
| AI | `LegalResearchQuery`, `LegalSource`, `LegalSourceSnapshot`, `DraftRequest`, `DraftDocument`, `DraftVersion`, `DraftCitation`, `AIUsageRecord` |
| Plataforma | `CustomFieldDefinition`, `CustomFieldValue`, `Notification`, `AuditLog`, `Webhook`, `Plan`, `Module`, `Subscription` |

Toda entidade de escritório carrega `OrganizationId` com filtro global obrigatório; dados jurídicos guardam origem, data de coleta e identificador externo.

### 15.4 APIs (exemplos, versionadas em `/api/v1`)

- `GET|POST /cases`, `GET /cases/{id}/timeline`, `POST /cases/import`
- `GET /publications?status=pending`, `POST /publications/{id}/triage`
- `GET|POST /deadlines`, `POST /deadlines/calculate`, `POST /deadlines/{id}/complete`
- `POST /cases/{id}/claims`, `POST /cases/{id}/risk-assessments`, `GET /provisions?period=2026-09`
- `GET|POST /time-entries`, `POST /pre-invoices`, `POST /invoices/{id}/issue-nfse`
- `GET|POST /contracts`, `POST /documents`, `POST /documents/{id}/signature-requests`
- `POST /conflict-checks`, `GET|POST /workflows`
- `POST /legal-research/search`, `POST /drafts`, `POST /drafts/{id}/citations/{citationId}/verify`
- `GET /reports/{id}/run`, `GET /usage`, `POST /webhooks`

Operações longas (importação, captura, IA, relatórios pesados, lote) retornam um identificador de tarefa com consulta de status e evento de conclusão via webhook.

## 16. Roadmap, critérios de aceite, riscos e estratégia comercial

### 16.1 Roadmap em fases

O ERP completo não cabe em um único MVP: a sequência abaixo leva primeiro um produto vendável para autônomos e pequenos escritórios e só depois os recursos de grande porte. Estimativas para um time de 4–6 pessoas (3–4 devs full stack .NET/Angular, 1 QA, 1 produto/UX com apoio de um advogado consultor); para um desenvolvedor sozinho, multiplique por 3–4.

1. **Fase 0 — Validação (3–4 semanas).** Entrevistar 15–20 escritórios de portes diferentes; contratar fornecedor de publicações/andamentos e de jurisprudência; fechar escopo da Fase 2 e preços. *Gate:* fornecedores contratados e 5 escritórios piloto comprometidos.
2. **Fase 1 — Fundação (6–8 semanas).** Solução modular, CI/CD, identidade, MFA, multi-tenant, perfis e permissões, estrutura organizacional, campos customizados, auditoria, notificações. *Gate:* testes de isolamento passando.
3. **Fase 2 — MVP comercial Solo/Escritório (4–5 meses).** Pessoas e conflito básico, processos, captura de publicações e andamentos com triagem, prazos com calculadora, agenda e tarefas, GED com OCR, honorários e despesas simples, IA de pesquisa e minutas (escopo da v2.0), importação de planilha. *Gate:* zero publicação perdida em 30 dias de piloto e precisão de citações dentro da meta.
4. **Fase 3 — Financeiro completo e portal (3 meses).** Timesheet, pré-fatura e faturamento, NFS-e, boleto/Pix, contas a pagar/receber, conciliação, rentabilidade, portal do cliente, propostas de honorários. *Gate:* um ciclo mensal completo de faturamento em produção nos pilotos.
5. **Fase 4 — Operação em escala (3 meses).** Controladoria jurídica, workflow e automações, modelos e geração em lote, assinatura eletrônica, BI e gerador de relatórios, CRM completo, IA aplicada ao ERP (RF-40). *Gate:* escritório piloto com 30+ usuários operando sem planilhas paralelas.
6. **Fase 5 — Corporativo (4–6 meses).** SSO/SCIM, casos sigilosos e muralha ética, pedidos e provisionamento CPC 25, garantias e acordos, gestão de escritórios terceirizados, contratos (CLM), societário, procurações, API pública e webhooks, testes de carga com 500 mil processos, ambiente sandbox, início de ISO 27001/SOC 2. *Gate:* primeiro cliente Corporativo em produção.

**Total estimado:** cerca de 18–24 meses até a edição Corporativa, com receita a partir do fim da Fase 2 (≈ 7–8 meses).

### 16.2 Critérios de aceite por módulo crítico

- **Publicações:** 100% das publicações capturadas são vinculadas ou entram na fila; nenhuma é descartada sem registro de quem a tratou; falha de captura alerta em até 1 hora.
- **Prazos:** todo prazo tem responsável, data fatal e status; prazo cumprido exige evidência; cálculo automático nunca é salvo sem confirmação humana.
- **Isolamento:** testes automatizados provam que nenhum endpoint retorna dados de outra organização ou de caso sigiloso sem permissão.
- **Financeiro:** valores fechados são imutáveis (estorno em vez de edição); NFS-e emitida confere com a fatura; nenhuma cobrança duplicada.
- **IA:** cada citação tem fonte e trecho de suporte; metadados não confirmados são sinalizados; nenhum identificador inventado; nenhuma peça protocolada automaticamente.
- **Migração:** importação de 50 mil processos com relatório de erros em menos de 2 horas.

### 16.3 Riscos e mitigação

| Risco | Mitigação |
| --- | --- |
| Perda de prazo por falha de captura | Dois fornecedores no Corporativo, monitoramento por OAB/diário, fechamento diário da controladoria |
| Escopo grande demais para o time | Fases com gate, edição Solo/Escritório vendável antes do Corporativo |
| Citações ou precedentes falsos | Fontes rastreáveis, verificação independente, bloqueio de referências não confirmadas |
| Vazamento entre escritórios ou casos | Isolamento no servidor, testes de segurança no CI, auditoria, pentest |
| Erro fiscal na NFS-e / reforma tributária | Emissor intermediário homologado, revisão com contador, configuração por município |
| Custo de fornecedores (publicações, jurisprudência, IA) | Medição por organização, limites por plano, repasse de excedente |
| Concorrência estabelecida (eLaw, Projuris, Astrea, Legal One) | Diferencial de IA verificável, migração assistida, preço de entrada menor |
| Migração difícil de clientes grandes | Importadores dedicados, serviço de implantação pago, sandbox |
| Responsabilidade profissional | Revisão humana obrigatória, termos claros, auditoria das aprovações |
| Dependência de fornecedor | Camada de abstração e plano de contingência por integração |

### 16.4 Estratégia comercial

Preços são hipóteses a validar na Fase 0, após medir o custo real de publicações, jurisprudência e IA por usuário.

| Edição | Preço hipotético | Inclui |
| --- | --- | --- |
| Solo | R$ 129–199/mês | 1–2 usuários, publicações de até 2 OABs, cota de IA |
| Escritório | R$ 89–149 por usuário/mês (mínimo 3) | Todos os módulos de gestão e financeiro, cota de IA por usuário |
| Corporativo | Sob proposta (a partir de \~R$ 5 mil/mês) | Módulos corporativos, SSO, SLA 99,9%, implantação dedicada |

- Receita adicional: implantação e migração, OABs extras, créditos de IA, armazenamento extra, módulos opcionais (PI, societário).
- Canais: venda direta para pequenos (self-service com teste de 14 dias), venda consultiva para médios e grandes, parcerias com seccionais da OAB e consultorias de gestão jurídica.

**Definição de sucesso:** um escritório de qualquer porte opera clientes, processos, prazos, documentos e financeiro inteiramente no LexFlow, sem planilhas paralelas, e usa a IA para produzir peças com fontes verificáveis sob controle profissional.
