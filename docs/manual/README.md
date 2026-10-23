# Manual de utilização do LexFlow

**Revisão — 2026-10-23.** Este manual acompanha as telas disponíveis até o 6E (#35), os recursos de backend do 7A (#36) e do 7B (#37) disponíveis na main e o backend do 7C disponível na main pelo [PR #38](https://github.com/iagogoliveira/ProjetoLaw/pull/38), mergeado em 2026-10-21. O backend do 7D está disponível na main pelo [PR #39](https://github.com/iagogoliveira/ProjetoLaw/pull/39), mergeado em 2026-10-21 com CI integral aprovado; o sino trata o novo lembrete. As telas do 7E estão disponíveis na main pelo [PR #40](https://github.com/iagogoliveira/ProjetoLaw/pull/40), mergeado em 2026-10-22 com CI integral aprovado, conforme a seção 18. O 7F está disponível na main pelo [PR #41](https://github.com/iagogoliveira/ProjetoLaw/pull/41), mergeado em 2026-10-22 com CI integral aprovado, e acrescenta agenda, tarefas, controladoria e preferências, conforme a seção 19. O 7G está publicado no [PR #42](https://github.com/iagogoliveira/ProjetoLaw/pull/42), ainda aguardando revisão e merge, e acrescenta a sincronização simulada descrita abaixo; os adaptadores externos reais continuam pendentes. Recursos ainda previstos são identificados nas seções correspondentes.

Para iniciar o sistema no computador, consulte [Ambiente local](ambiente-local.md). Para navegar com o ambiente iniciado, abra [LexFlow](http://localhost:4200).

## 1. Primeiro acesso

1. Na página de entrada, escolha **Criar conta**.
2. Informe nome completo, e-mail e senha e confirme o cadastro.
3. No ambiente local, abra o [Mailpit](http://localhost:8025), encontre a mensagem enviada ao endereço informado e abra o link de confirmação. O Mailpit recebe os e-mails de desenvolvimento; não é necessário procurar essa mensagem na sua caixa externa.
4. Depois da confirmação, volte ao LexFlow e escolha **Entrar**. Informe o e-mail e a senha cadastrados.
5. No primeiro acesso sem escritório, preencha **Nome do escritório** e escolha **Criar escritório**.
6. Confira o nome do escritório ativo no cabeçalho. O menu lateral permite acessar as áreas autorizadas para sua conta.

Você também pode entrar por um convite de um escritório existente: abra o link recebido, crie a conta com o e-mail convidado se ainda não a tiver, confirme o e-mail, entre e escolha **Aceitar convite**.

A página **Início** ainda não apresenta os painéis gerenciais planejados. Uma página inicial simples é o comportamento esperado nesta versão; explore os módulos pelo menu lateral.

## 2. Conta e permissões

Em **Minha conta**, consulte seu perfil, configure a verificação em duas etapas e confira as sessões abertas. Para configurar um aplicativo autenticador, inicie a configuração, cadastre o código no aplicativo e confirme com o código temporário solicitado. Guarde os códigos de recuperação apresentados. Também existe a opção de segundo fator por e-mail.

Use **Sair**, no cabeçalho, para encerrar o acesso atual. Para encerrar outra sessão, use a lista de sessões em **Minha conta**.

As telas, abas e ações variam conforme as permissões. Ter acesso a um processo também depende do alcance atribuído ao perfil e das restrições daquele caso. Se uma ação descrita neste manual não aparecer, peça ao administrador que confira seu perfil e alcance; digitar o endereço da tela não concede a permissão.

## 3. Preparar o escritório

Abra **Configurações**. As seções disponíveis dependem das permissões administrativas.

| Seção | Uso |
| --- | --- |
| Escritório | Consultar e alterar as configurações apresentadas do escritório |
| Usuários e convites | Consultar membros, convidar pessoas e administrar suas atribuições |
| Perfis | Consultar e ajustar permissões dos perfis |
| Estrutura | Cadastrar filiais, áreas, equipes, centros de custo e sócios |
| Campos customizados | Criar campos adicionais e experimentar sua validação no formulário de teste da própria tela |
| Modelos de proposta | Baixar o modelo padrão e administrar modelos usados nas propostas |
| Numeração de processos | Configurar a numeração interna das pastas |

Para convidar alguém, acesse a seção de membros, escolha **Convidar pessoa**, preencha o e-mail e as atribuições e use **Enviar convite**. No ambiente local, o destinatário encontra a mensagem no Mailpit. Ele precisa abrir o link e aceitar o convite; enviar o convite sozinho não conclui a entrada no escritório.

Cadastre a estrutura que pretende usar antes de organizar processos por filial, área ou equipe. Alterações de permissão podem mudar o que cada membro consegue consultar e executar.

## 4. Pessoas, clientes e partes

### Cadastrar e localizar

1. Abra **Pessoas** e escolha a ação de novo cadastro.
2. Selecione pessoa física ou jurídica.
3. Preencha nome ou razão social, documento quando aplicável e os papéis da pessoa, como cliente ou parte contrária.
4. Acrescente contatos, endereços e demais informações necessárias.
5. Salve e confira o detalhe do cadastro.

Na lista, procure por nome ou documento e use o filtro de papel. A busca por nome trata acentos. Se o sistema apontar um documento já cadastrado no escritório, consulte o registro existente antes de tentar cadastrar novamente. O mesmo documento pode existir em escritórios diferentes, cujos dados são separados.

O formulário pode sugerir pessoas semelhantes. Confira nome e documento para decidir se já existe um cadastro adequado. A consulta de CNPJ, quando usada, deve ser conferida antes de salvar os dados recebidos.

### Verificar conflito de interesses

1. Abra **Conflito de interesses**, preencha os dados solicitados e use **Verificar conflito**.
2. Leia os resultados e os vínculos apresentados.
3. Se decidir prosseguir com um alerta, informe a justificativa exigida e confirme **Prosseguir**.

O resultado auxilia a análise do escritório. Uma verificação sem alerta não substitui a conferência humana das pessoas e relações envolvidas. A contratação de uma oportunidade exige a verificação correspondente antes do aceite da proposta.

## 5. Processos e casos

### Cadastrar pelo assistente

1. Abra **Processos** e escolha **Novo processo**.
2. Selecione **Judicial** ou **Extrajudicial**. Para judicial, preencha o número CNJ e use **Consultar CNJ** para conferir sua estrutura e tribunal. Essa consulta não importa o processo judicial nem busca seus autos.
3. Avance pelos dados do caso, preenchendo as informações aplicáveis, como área, filial e comarca.
4. Inclua as partes e identifique o cliente e a parte contrária. O assistente permite cadastrar pessoas durante esse preenchimento.
5. Inclua os responsáveis: selecione o membro e confirme **Incluir**.
6. Revise as informações e escolha **Cadastrar processo**.

Ao concluir, o sistema abre o detalhe e mostra a pasta interna. O número CNJ e a pasta interna têm finalidades distintas: a pasta identifica o caso na organização, inclusive quando não há CNJ.

### Encontrar e acompanhar

Na lista de processos, use a busca e os filtros de situação, tipo, área, filial, fase e responsável disponíveis. Para reutilizar uma combinação, informe **Nome do filtro** e escolha **Salvar filtro**. Depois, selecione-a em **Filtros salvos**.

Abra um processo pela pasta. Consulte os dados, partes, responsáveis e **Linha do tempo**. A linha do tempo reúne os acontecimentos já integrados ao caso, como cadastro e alterações de situação.

No detalhe, as ações permitidas incluem editar, suspender, reativar, encerrar e reabrir. Ao encerrar, preencha o motivo, resultado e demais informações solicitadas. Confira o caso antes de confirmar mudanças de situação.

Para uma operação em lote, marque os processos na lista, selecione a **Ação** e use **Aplicar aos selecionados**. Confira o resultado informado. Uma alteração pode fazer o processo desaparecer do filtro atual: por exemplo, reativar um caso enquanto a lista mostra somente suspensos.

### Restrições de acesso

As restrições do processo controlam quem pode vê-lo. O bloqueio de uma pessoa ou equipe prevalece sobre a permissão de consulta. Quando há uma lista de permitidos, o acesso segue essa lista e as regras de administração de restrições.

Antes de mudar restrições, confirme quem precisa continuar acompanhando o processo. Para testar o alcance, entre com a conta de um membro com o perfil correspondente; a visualização do administrador não representa necessariamente a de todos os usuários.

## 6. Pedidos e valores

1. No detalhe do processo, abra **Pedidos e valores** e escolha **Novo pedido**.
2. Selecione o **Objeto do pedido**, descreva-o e preencha os valores por instância aplicáveis.
3. Informe o índice e início da correção e os juros quando aplicáveis.
4. Escolha **Salvar pedido**.
5. Para consultar valores corrigidos, informe **Atualizar até** e use **Atualizar valores**.
6. Para consultar alterações de valores, abra **Histórico de valores**.

Confira sempre a data de referência e os parâmetros usados no cálculo. Na versão atual, a SELIC não recebe juros extras; os juros simples usam cálculo proporcional por dia, e a correção considera deflação com piso no valor original. As regras e a disponibilidade de índices devem ser consideradas ao comparar resultados.

## 7. Risco

1. Abra a aba **Risco**.
2. Selecione o **Âmbito da avaliação**, que pode ser o processo ou um pedido.
3. Informe a **Probabilidade de perda**, a perda estimada quando aplicável e a justificativa.
4. Escolha **Registrar avaliação**.
5. Confira as avaliações atuais e o **Histórico de risco**.

A ação exige permissão de avaliação de risco. O registro preserva o histórico; a avaliação não representa uma análise jurídica automática.

## 8. Acordos

1. Abra **Acordos**, escolha **Novo acordo**, confira a direção do pagamento e use **Abrir negociação**.
2. Em **Nova rodada**, informe o valor e condições e escolha **Registrar proposta**. Registre novas rodadas conforme a negociação evoluir.
3. Quando houver termos finais, escolha **Enviar termos**. Preencha valor acordado, número de parcelas e primeiro vencimento.
4. Confira a **Prévia das parcelas**, incluindo valores e datas, e confirme o envio dos termos.
5. Uma pessoa com a permissão correspondente pode usar **Aprovar acordo** e confirmar a aprovação. A regra atual permite aprovar o próprio acordo com essa permissão.
6. Depois da aprovação, use **Homologar acordo**, informe a data e confirme a ação.
7. Quando uma parcela for paga, use **Dar baixa na parcela**, confira a data e o valor do pagamento e confirme.

Os termos finais passam pela aprovação antes da homologação. A baixa de parcelas é manual nesta versão; não há conciliação bancária automática nem confirmação automática do recebimento.

## 9. Audiências

1. Abra **Audiências** e escolha **Agendar audiência**.
2. Informe tipo, formato, data e hora, local ou link, conforme o formato.
3. Selecione o advogado designado e inclua preposto ou testemunhas quando necessários.
4. Escolha **Salvar audiência** e confira o registro.
5. Para alterar a data, use **Redesignar audiência**, informe a nova data/hora e o motivo e confirme. A audiência mantém seu histórico de redesignações.
6. Depois da audiência, use **Registrar realização**, preencha o resultado ou ata e confirme. Use o cancelamento quando essa for a situação efetiva.

A agenda unificada e a sincronização com calendários externos ainda pertencem a etapas futuras. Registre aqui a situação da audiência sem presumir que já foi enviada ao Google Calendar ou Microsoft 365.

## 10. Importar processos

1. Em **Processos**, escolha **Importar processos**.
2. Use **Baixar modelo** para obter a planilha com o layout esperado.
3. Preencha o arquivo e selecione-o em **Arquivo XLSX ou CSV**.
4. Escolha **Enviar para validação** e aguarde **Validação concluída. Confira antes de importar.**
5. Revise as primeiras linhas válidas e os erros apresentados. A validação ainda não é a confirmação de importação.
6. Para prosseguir, escolha **Confirmar importação das válidas**.
7. Aguarde **Importação concluída.** e confira as quantidades finais.
8. Quando houver linhas inválidas, use **Baixar relatório das inválidas**, corrija os dados e revise o próximo envio para não repetir linhas já importadas.

Pessoas ainda não cadastradas podem ser criadas; documento já existente reaproveita o cadastro. A pasta legada informada é preservada. Um CNJ já existente no escritório aparece no relatório, sem criar outro processo com aquele número. O fluxo importa as linhas válidas e mantém as inválidas no relatório.

## 11. Funil comercial e propostas

1. Abra **Funil comercial** e escolha **Nova oportunidade**.
2. Preencha título, dados do interessado, origem, valor estimado e demais informações e salve.
3. No quadro, acompanhe a oportunidade e use **Mover para** para mudar a etapa. Abra o cartão para consultar seus dados.
4. No detalhe da oportunidade, escolha **Nova proposta**. Informe título e itens, confira modalidade, quantidade, valor e total e use **Salvar rascunho**.
5. Use **Enviar para aprovação**. A pessoa autorizada analisa e aprova ou devolve conforme as ações disponíveis.
6. Em escritório com apenas um membro ativo, a regra de propostas permite a aprovação pelo próprio elaborador autorizado. Com outros membros ativos, aplicam-se as restrições de aprovação do fluxo.
7. Depois de aprovada, use **Registrar envio ao cliente**. O documento gerado pode ser baixado em **Baixar DOCX**.
8. Execute a verificação de conflito correspondente e registre o prosseguimento antes de **Registrar aceite**.

Registrar envio e aceite representa o registro dessas etapas no sistema; não confunda essas ações com assinatura eletrônica ou envio automático por um canal externo. A contratação pode converter o interessado em cliente. O financeiro completo e a cobrança de honorários ainda estão previstos para uma etapa posterior.

## 12. Roteiro para experimentar a versão atual

Use um escritório de demonstração e pessoas fictícias para manter o ensaio separado de sua operação real.

1. Crie sua conta, confirme o e-mail pelo Mailpit e crie o escritório **Demonstração LexFlow**.
2. Cadastre uma pessoa como cliente, por exemplo **Cliente Demonstração Ltda.**.
3. Crie um caso **Extrajudicial**, sem CNJ, incluindo o cliente e você como responsável.
4. Abra o detalhe e explore as abas. Cadastre um pedido, registre uma avaliação de risco e agende uma audiência de demonstração.
5. Redesigne a audiência e confira o histórico. Suspenda e reative o caso e consulte a linha do tempo.
6. Experimente um filtro salvo na lista.
7. Baixe o modelo da importação e valide um pequeno arquivo antes de confirmar qualquer importação.
8. Crie uma oportunidade e percorra o rascunho/aprovação de uma proposta.
9. Se quiser avaliar permissões, convide outra conta de demonstração e compare os acessos.

Ao apontar um problema, anote tela, ação, resultado esperado e mensagem recebida. Informe se estava usando a conta administradora ou outra conta. Esses detalhes ajudam a reproduzir o comportamento.

## 13. Disponibilidade dos recursos

| Recurso | Situação nesta versão |
| --- | --- |
| Fontes, captura e notificações de publicações | Backend na main; telas disponíveis na main desde o 6E (#35) |
| Captura real de publicações/intimações | Fornecedor ainda pendente; o ambiente local usa provedor simulado |
| DataJud e consultas periódicas de movimentações | Backend na main; configuração e acompanhamento disponíveis na main desde o 6E (#35) |
| Triagem, distribuição e fechamento diário da controladoria | Backend na main desde o 6D (#34); telas disponíveis na main desde o 6E (#35) |
| Calculadora de prazos e calendário forense (feriados, recesso e dias sem expediente do escritório) | Backend na main desde o 7A ([#36](https://github.com/iagogoliveira/ProjetoLaw/pull/36)); tela da calculadora na main pelo PR #40 (7E); calendário do escritório pela API; revisão jurídica pendente |
| Cadastro e ciclo completo dos prazos (andamento, cumprimento com protocolo, perda justificada e conferência) | Backend disponível na main pelo PR #37 (7B), mergeado em 2026-10-20; telas disponíveis na main pelo PR #40 (7E) |
| Agenda unificada e tarefas completas | Backend disponível na main pelo PR #38; telas na main pelo PR #41 (seção 19); sincronização no 7G |
| Automações e fluxos configuráveis | Etapa 8 |
| GED completo, assinatura, financeiro, IA jurídica e portal | Etapas posteriores |
| Indicadores gerenciais completos na página inicial | Ainda não disponíveis; backend da controladoria disponível na main pelo PR #39; telas no 7F |

## Andamentos automáticos

**Andamentos pelo DataJud:** depois de habilitada a integração do escritório, o sistema consulta processos judiciais ativos em ciclos de seis horas, distribuídos ao longo do dia. A primeira consulta traz o histórico público disponível; as seguintes acrescentam novidades, inclusive atos antigos disponibilizados depois. As datas dos atos são mantidas. Uma falha pode atrasar a atualização; o sistema retoma a captura sem apagar o histórico já salvo. DataJud não substitui a captura de publicações/intimações e não disponibiliza o conteúdo de processos sob sigilo. Desde o 6E (#35), a configuração está em **Fontes e integrações → Andamentos e DataJud**, para quem gerencia integrações no escritório. A chave existente nunca é exibida; deixar o campo vazio mantém a chave. Para remover, marque **Remover a chave cadastrada** e salve. O campo da nova chave é limpo após o envio.

## 14. Publicações, triagem e fechamento (6E, PR #35 mergeado)

### Fontes e captura

Abra **Fontes e integrações** no menu. Escolha Nome, OAB ou CNPJ, preencha nome da fonte e valor monitorado e salve. Fontes ativas seguem a captura automática; editar permite desativar sem apagar o histórico. O ambiente AppHost usa provedor simulado; isso não representa captura real de intimações.

Escolha o **Dia da captura (São Paulo)** e use **Consultar captura de …**. O painel mostra execuções e alerta da fonte. **Tentar captura agora** exige gestão de integrações em escopo de escritório e fonte ativa; registra uma solicitação auditada e a executa no Worker. Escolha um dia entre a última ativação da fonte e hoje. Aguarde e use **Atualizar** para conferir o resultado. Repetir uma captura mantém a deduplicação das publicações. Falhas de períodos anteriores a uma mudança do critério da fonte precisam de avaliação técnica; não altere o critério para tentar reproduzir um recorte antigo.

### Fila e notificações

Abra **Publicações**. Filtre por situação, vínculo ou responsável; a URL guarda os filtros e pode ser compartilhada com quem tem o acesso correspondente. A lista carrega por páginas. Selecione um item para ler o texto e preencher a triagem ao lado; o CNJ aparece destacado. O contador do menu mede publicações pendentes visíveis, independentemente de notificações lidas.

J/K navegam, T foca a classificação e P prepara um prazo, quando permitido. Os atalhos não atuam enquanto você digita ou usa uma seleção; desmarque **Habilitar atalhos de uma tecla** para desativá-los. Novidades são avisadas sem apagar o formulário em edição. Termine ou abandone a edição antes de **Atualizar**, trocar a publicação ou mudar filtros.

Em **Notificações**, consulte referências recebidas, abra a publicação autorizada ou marque o aviso como lido. Marcar aviso como lido não conclui triagem. A conexão se recupera automaticamente; a consulta manual continua disponível enquanto reconecta. Quem tem triagem no escritório pode consultar alertas de captura por dia na fila; quem só lê não recebe controles de triagem.

### Tratar uma publicação

A operação técnica está descrita no [guia de ambiente local](ambiente-local.md). Para tratar uma publicação, a pessoa precisa de permissão de leitura e triagem e de acesso ao seu conteúdo. Distribuir exige a permissão de distribuição; atribuir a alguém não concede acesso ao processo. O responsável precisa estar ativo e conseguir ler e triar aquela publicação.

Busque o processo por pasta ou CNJ, selecione-o e use **Vincular processo**, quando necessário. A busca traz até 50 opções por vez, respeitando seu alcance; refine a pesquisa. Em **Distribuição**, escolha um membro e use **Salvar distribuição**; a opção sem atribuição remove a distribuição. A classificação e o destino são confirmados por uma pessoa. Criar prazo ou tarefa exige também a respectiva permissão de cadastro; criar audiência exige editar o processo. Desde o PR #40 (7E), a triagem oferece sugestão de cálculo para o prazo, com confirmação humana; as telas de cumprimento e conferência estão descritas na seção 18. As datas das tarefas continuam manuais. A audiência criada aparece na aba **Audiências** do processo, e os prazos na aba **Prazos**; tarefas seguem pela API até o 7F.

Uma publicação irrelevante ou capturada por engano pode ser encerrada sem processo, com classificação **Outros** e ação **Encerrar como irrelevante/engano**, com justificativa e identificação de quem decidiu. As demais triagens exigem processo vinculado. Escolha a classificação e a ação compatíveis, preencha os dados do destino, marque a confirmação humana e use **Confirmar e salvar triagem**. A gravação da triagem e do destino é conjunta: falhas antes da confirmação da transação desfazem os dois. Se houver falha de resposta, consulte o estado antes de reenviar. Uma publicação já triada não pode ser triada, vinculada ou distribuída novamente.

### Fechar o dia

Abra **Fechamento diário**, escolha o dia e use **Consultar fechamento**. Confira pendências e capturas, marque a confirmação e use **Confirmar fechamento**. O histórico mantém as revisões anteriores e oferece **Carregar mais** quando necessário. O fechamento diário exige `publications.close` em escopo de escritório e alcance de todas as publicações consideradas. O dia usa o recebimento no horário de São Paulo e inclui publicações anteriores ainda pendentes. Captura esperada sem sucesso ou alerta anterior de captura ainda não resolvido impede fechar, mesmo sem publicações na fila; uma execução bem-sucedida com zero resultados conta como sucesso. Desativar a fonte depois da janela de captura não apaga essa obrigação. Falha de captura exige recuperar a captura; descartar publicações não substitui dados ausentes.

O fechamento registra usuário, instante e revisão. Se novas publicações chegarem depois, o dia volta a exigir conferência e novo fechamento, inclusive quando as novas entradas já tiverem sido triadas. A assinatura anterior permanece no histórico. Repetir o fechamento sem novidades devolve o mesmo registro. Essa assinatura é o registro auditado da conta autenticada; não é assinatura digital de um documento.

### Andamentos do processo

Na aba **Andamentos** do processo, consulte origem, data do ato e data de coleta, com paginação e situação da última sincronização. Para registrar manualmente, informe data/hora em São Paulo e descrição e use **Salvar andamento**; é necessário editar o processo. O registro aparece também na **Linha do tempo**, cujo filtro inclui publicações recebidas e andamentos.

## 15. Prazos e alertas (7B, backend na main)

O cadastro manual e as ações de iniciar, conferir, cumprir e registrar perda estão disponíveis pela API na main desde o merge do PR #37; a tela completa está disponível na main pelo PR #40, conforme a seção 18. O [guia local](ambiente-local.md#11-ciclo-do-prazo-7b-operação-técnica) descreve como conferir essas operações.

O cumprimento exige texto do protocolo. A perda exige justificativa. Quando a organização exige conferência, o trabalho pode ser iniciado antes dela, mas o prazo só pode ser marcado como cumprido depois de conferido. O conferente precisa ser outro membro; em escritório com um único membro ativo, a autoconferência continua exigindo a permissão de conferir prazos.

No sino de notificações, os alertas de **Prazo perdido** e **Calendário alterado** abrem o processo correspondente. Somente o responsável, o conferente e a coordenação com as permissões e o alcance necessários veem esses alertas. Uma alteração relevante de dia sem expediente também aparece na linha do tempo; as datas e a memória do prazo confirmado permanecem preservadas. Não há referência a documentos do GED nesta entrega; ela entra na Etapa 9.

## 16. Agenda e tarefas (7C, backend disponível na main)

Na main desde o merge do PR #38, a API permite cadastrar tarefas e compromissos com ou sem processo. As telas estão na main pelo PR #41 (seção 19); a implementação do 7C foi verificada em 2026-10-21 e foi mergeada no [PR #38](https://github.com/iagogoliveira/ProjetoLaw/pull/38), com CI integral aprovado. O [guia local](ambiente-local.md#12-agenda-e-tarefas-7c-operação-técnica) descreve os endpoints para a conferência técnica.

A tarefa tem responsável, vencimento, prioridade, checklist, comentários e subtarefas completas de um nível. Para concluir, todos os itens do checklist precisam estar marcados e as subtarefas concluídas ou canceladas. Tarefas concluídas/canceladas podem ser reabertas como pendentes. Reabra primeiro a principal para alterar uma subtarefa de principal fechada. Cancelar a principal preserva o estado das subtarefas. Anexos dependem do GED da Etapa 9.

A agenda reúne prazos (data fatal e informação da data interna), tarefas (vencimento), compromissos e audiências, com filtros por pessoa, equipe e período. Um filtro não concede acesso: as permissões, o alcance e o sigilo continuam valendo. Os compromissos aceitam horários simultâneos; cancelar preserva o registro. A redistribuição em lote informa o resultado de cada item e preserva as datas e a conferência dos prazos.

## 17. Controladoria e notificações (7D, backend disponível na main)

O backend do 7D acrescenta consultas e configurações pela API; as telas do painel e de preferências estão na main pelo PR #41 (seção 19). A verificação integral foi aprovada em 2026-10-21; implementação publicada no [PR #39](https://github.com/iagogoliveira/ProjetoLaw/pull/39), mergeada pelo usuário em 2026-10-21, com CI integral aprovado. Consulte o [guia técnico local](ambiente-local.md#13-controladoria-e-notificações-7d-operação-técnica).

O relatório reúne publicações não tratadas, prazos abertos sem responsável ativo, prazos abertos vencidos sem evidência e cumpridos legados sem protocolo. Processos sem andamento consideram apenas os ativos, pelo último andamento processual ou pelo cadastro quando não houver andamento; o padrão é de 30 dias. Contagens e indicadores respeitam as mesmas permissões, alcance e sigilo dos itens.

O tempo médio de triagem mede recebimento até a triagem concluída no período; sem amostras, o valor é vazio. O cadastro tardio compara a data de confirmação com a meta interna do prazo, no horário de São Paulo. Retrabalho por refazer triagem permanece zero porque esse fluxo ainda não existe; alterações comuns do cadastro não contam como nova triagem.

Os alertas **Prazo perdido**, **Calendário alterado** e **Prazo fatal hoje** são obrigatórios no aplicativo para os destinatários autorizados. As escolhas individuais não retiram essa proteção. O escritório pode exigir também os tipos existentes de publicação e captura. O lembrete fatal só permanece visível enquanto o prazo estiver aberto e vencer naquele dia. Ele abre o processo correspondente no sino.

O resumo por e-mail é opcional, desativado inicialmente e exige endereço confirmado. Às 07:30 de São Paulo, reúne as seções escolhidas: agenda atribuída ao usuário no dia, seus prazos abertos em sete dias a partir de hoje e publicações pendentes que ele pode ler. Mostra até vinte itens por seção, informando o total. Sem itens visíveis nas seções escolhidas, não envia e-mail. Confira o resumo no Mailpit local.

Receber um aviso ou e-mail não concede acesso ao processo. Os envios usam as permissões atuais; a leitura no sistema continua sujeita à autenticação exigida pelo escritório. O registro diário evita novas entregas após envio confirmado e permite retentar falhas. Em uma queda entre a aceitação SMTP e a gravação do sucesso, uma retentativa pode duplicar o e-mail.

## 18. Prazos (7E, PR #40 mergeado)

As telas desta seção estão disponíveis na main pelo PR #40, mergeado em 2026-10-22. Agenda e tarefas ficam no 7F; a revisão jurídica dos casos e do calendário do 7A permanece pendente.

Abra **Prazos** no menu lateral. Informe período do vencimento fatal, responsável e equipe e escolha **Aplicar filtros**. **Até** é uma data exclusiva: para incluir o dia 31, informe o dia 1 do mês seguinte. A consulta inicia trinta dias antes de hoje e termina trinta e um dias depois; amplie o período para procurar outros vencimentos. **Incluir encerrados** traz cumpridos e perdidos; **Carregar mais** continua a consulta por vencimento.

O semáforo dos abertos usa a data de São Paulo: **vermelho** para fatal vencido ou hoje; **amarelo** para meta interna vencida/hoje ou fatal nos próximos dois dias corridos; **verde** para os demais. Texto acompanha a cor. Cumprido e perdido têm identificação própria. Passar do vencimento não marca perda automaticamente.

Os filtros aplicados ficam na URL, que pode ser copiada para reabrir a mesma consulta. Em **Filtros salvos**, informe um **Nome do filtro** e use **Salvar filtros atuais**. É possível guardar até dez conjuntos; clique no nome para aplicar ou em **Excluir** para retirar. Salvar e excluir dependem das permissões de preferências pessoais. Aguarde a leitura dos filtros existentes; em caso de erro, use **Tentar novamente** antes de alterá-los.

### Cadastrar com a calculadora

Em **Cadastrar prazo**, pesquise e selecione o processo, informe tipo, descrição e responsável. Também é possível começar na aba **Prazos** do processo. Na calculadora, escolha origem, data do fato, dias, modo de contagem, eventual dobra e antecedência interna. Para intimação pelo portal, a consulta é opcional. Tribunal e comarca vêm do processo; confira UF e município, principalmente em tribunais que abrangem mais de um estado.

Use **Calcular sugestão** e abra a **Memória completa do cálculo**. Confira datas e cada dia contado, pulado ou prorrogado. **Aplicar datas sugeridas** apenas preenche a proposta: marque **Conferi e confirmo as datas sugeridas** e use **Salvar prazo**. Mudar as entradas ou o processo invalida a confirmação. Se o calendário mudar antes de salvar, o servidor pode pedir novo cálculo; confira e confirme novamente. A calculadora não grava por conta própria.

### Trabalhar, conferir e cumprir

Abra o prazo pela lista ou pela aba do processo. O detalhe mostra situação, datas, regra e memória disponíveis, conferência, protocolo ou justificativa registrados. Prazos provenientes da triagem podem não possuir regra ou memória gravada; a sugestão usada na triagem é exibida na prévia.

**Iniciar** permite trabalhar mesmo com conferência obrigatória pendente. **Conferir prazo** exige a permissão correspondente; o autor não confere o próprio prazo, salvo escritório com um único membro ativo. **Cumprir prazo** exige **Evidência do cumprimento** (protocolo textual, até 4.000 caracteres) e conferência quando obrigatória. **Marcar como perdido** exige **Justificativa da perda** e aciona os alertas existentes à coordenação autorizada. As ações respeitam permissões e situação atual no servidor.

Se houver aviso de alteração do calendário, confira o prazo confirmado; suas datas não são recalculadas automaticamente. A política de conferência continua sendo configurada pela API descrita no guia local. Integração com documentos do GED será posterior.

### Usar a sugestão na triagem

Escolha **Criar prazo**, selecione o processo e use a calculadora. Aplique a sugestão, confira responsável e datas e confirme a decisão da triagem. As datas continuam editáveis; mudar uma delas retira a confirmação. Depois de triar, abra o prazo pelo painel ou pela aba do processo e registre o cumprimento com evidência. O fechamento diário conserva as regras da seção de publicações.

## 19. Agenda, tarefas, controladoria e notificações (7F, disponível na main)

Estas telas estão disponíveis na main pelo [PR #41](https://github.com/iagogoliveira/ProjetoLaw/pull/41), mergeado em 2026-10-22. Os dados e as ações respeitam seu perfil e o alcance autorizado no escritório. As telas do 7E já estão disponíveis na main pelo PR #40.

### Consultar a agenda e cadastrar compromissos

Abra **Agenda** com permissão de leitura de compromissos. Use **Dia**, **Semana** ou **Mês**, a **Data da agenda** e os botões de período. A semana começa na segunda-feira; horários são de São Paulo. O período exibido inclui o início e exclui o fim. Filtre por responsável/equipe e, se necessário, inclua encerrados. Filtros não ampliam o acesso aos prazos, tarefas, audiências ou processos sigilosos. Quando houver mais itens no servidor, a tela informa que a agenda está parcial: use **Carregar mais itens** até completar o período. A lista abaixo do calendário também permite abrir cada item pelo teclado.

Em **Novo compromisso**, informe título, responsável, início/fim e, opcionalmente, descrição, local e processo. O fim deve ser posterior ao início. Compromissos administrativos dispensam processo. Os horários são interpretados em São Paulo, independentemente do fuso do navegador. Abra um compromisso para editar; o vínculo de processo é preservado. Cancelar exige a ação explícita de confirmação; o registro cancelado continua disponível para consulta e não aceita alterações.

Para redistribuir, selecione os itens editáveis, escolha o novo responsável e confira o resultado de cada item. Audiências continuam sendo administradas no processo. Datas e confirmações de prazos são preservadas na redistribuição.

### Trabalhar com tarefas

Abra **Tarefas**, alterne entre **Lista** e **Kanban** e filtre por estado, prioridade, responsável, equipe ou processo. Filtros ficam na URL; conjuntos podem ser salvos explicitamente, até dez por usuário. Em listas grandes, carregue as páginas seguintes; os cartões carregados não representam o total do servidor.

Em **Nova tarefa**, informe título, vencimento, responsável e prioridade; descrição e processo são opcionais. No detalhe, adicione itens de **Checklist**, comentários e subtarefas. Subtarefas têm seus próprios dados e checklist, com apenas um nível e o mesmo processo da principal. Use a continuação quando a lista de subtarefas estiver parcial.

Use **Mover para** ou arraste o cartão entre as colunas do kanban. A alteração só é confirmada após o servidor validar. Concluir exige checklist completo e subtarefas concluídas ou canceladas. Tarefas encerradas ficam somente leitura; para reabrir, escolha **Pendente**. Cancelar a principal não cancela suas subtarefas. Redistribuir pode ter sucessos e falhas na mesma operação: confira o resultado identificado por tarefa.

### Conferir a controladoria e as notificações

Abra **Controladoria** com a permissão correspondente. A janela inicial dos indicadores vai de 30 dias antes de hoje até amanhã, com fim exclusivo. **Dias sem andamento** começa em 30 e é ajustável. Pendências mostram o estado atual; a janela afeta os indicadores, não limita as pendências àquelas datas. Cada seção mostra seu total e até 100 linhas autorizadas; a tela indica quando há mais. Indicador sem amostras permanece identificado, sem inventar média zero. Abra as publicações, prazos ou processos pelos links e use **Fechamento de publicações** quando autorizado. Salve conjuntos de filtros para reutilizar a consulta.

Em **Preferências de notificações**, escolha os alertas opcionais e se quer receber o resumo diário por e-mail às **07:30 de São Paulo**, com as seções de agenda, prazos e publicações. Salve suas escolhas. Os alertas obrigatórios do sistema e do escritório não podem ser silenciados; somente quem administra as regras do escritório pode exigir alertas adicionais. Sem permissão de alteração, as preferências ficam somente leitura. Sem itens nas seções escolhidas, não há envio do resumo.

A sincronização simulada do 7G está descrita abaixo; anexos dependem do GED da Etapa 9. Calendário do escritório e política de dupla conferência continuam configurados pela API. Revisão jurídica do 7A e fornecedor real de publicações permanecem pendentes.

## 20. Sincronização de calendários (7G, PR #42 aberto)

Na **Agenda**, abra **Sincronização de calendários** (`/agenda/sincronizacao`). A configuração é pessoal em cada escritório. O ambiente de desenvolvimento informa **Simulação local**: não há autenticação OAuth nem envio para Google ou Microsoft nesta entrega. Fora de ambientes com simulação habilitada, a tela informa a indisponibilidade e desabilita conexão e sincronização.

Escolha **Google Calendar** ou **Microsoft 365**, informe **Início** e **Fim exclusivo** e use **Salvar conexão e período**. O intervalo inclui o início, exclui o fim e tem de 1 a 366 dias. Há uma conexão por provedor e usuário no escritório; salvar novamente atualiza o período ou reativa a conexão existente. Selecione a conexão para consultar suas pendências. **Editar período** preenche o formulário; a mudança só é salva pela ação explícita.

**Sincronizar agora** enfileira o trabalho. Use **Atualizar** para conferir a última execução e suas pendências; enfileirar não significa que a sincronização terminou. Conexões ativas também são verificadas periodicamente. Somente itens atribuídos a você e ainda autorizados são considerados. Compromissos podem receber alterações externas; prazos têm apenas saída: datas, conferência, cumprimento, perda e evidências do LexFlow nunca são substituídos pelo calendário externo. Tarefas e audiências permanecem em seus módulos.

A saída contém somente tipo, data/horário e link que exige autenticação no LexFlow. Título, descrição, local e dados do processo não são exportados, inclusive em processos sigilosos. Textos já existentes no calendário externo são preservados. Alterações externas aplicam somente os campos modificados, preservando os dados locais correspondentes aos campos inalterados.

Quando os dois lados mudarem, confira os horários e abra o item local antes de escolher uma ação. **Manter LexFlow** reaplica o estado local; **Aplicar alterações externas** exige acesso de edição e só aparece quando o estado permite essa ação. Um compromisso já cancelado não é reaberto pela sincronização. Exclusão externa fica pendente: **Confirmar cancelamento no LexFlow** exige sua decisão explícita. Se o item mudar depois da consulta, atualize e sincronize antes de decidir novamente. Confirme cada decisão no checkbox apresentado. Quando houver páginas adicionais, use **Carregar mais pendências**.

Recorrência e compromissos externos de dia inteiro não são convertidos silenciosamente. Aparecem como conteúdo não suportado; **Ignorar item não suportado** encerra essa pendência sem criar compromisso local. A desconexão exige confirmação e preserva registros locais, vínculos e itens externos. Adaptadores reais e consentimento OAuth continuam previstos para uma entrega posterior.

## 21. Manutenção do manual

Este é um documento em evolução, não a descrição de funcionalidades futuras como se já estivessem entregues. Após cada melhoria, revisar este manual e o guia de ambiente local. Atualizar as instruções de uso afetadas, os pré-requisitos de acesso e a tabela de disponibilidade; atualizar também o guia quando mudar a forma de iniciar, executar ou conferir o sistema. Se a melhoria não afetar um dos documentos, registrar essa ausência de impacto na verificação da entrega. Os nomes das ações devem ser conferidos na interface e nos testes de navegador.

Nesta edição, as instruções foram conferidas nas rotas, componentes, traduções e fluxos Playwright existentes. Capturas de tela comentadas e aprofundamento por perfil ficam para as próximas revisões do manual; esta edição não inclui imagens.
