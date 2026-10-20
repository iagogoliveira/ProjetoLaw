# Manual de utilização do LexFlow

**Revisão — 2026-10-19.** Este manual acompanha as telas disponíveis até a Etapa 5 e identifica os recursos da Etapa 6 ainda em construção. O 6B (#32) acrescentou infraestrutura de captura e notificações. O 6C (#33), mergeado em 2026-10-19, acrescentou a integração DataJud no backend, disponível na main. O 6D (#34) disponibiliza triagem, distribuição e fechamento no backend da main. No branch `codex/etapa-6e-telas-publicacoes`, as telas de publicações, fontes, integração DataJud e fechamento estão implementadas e verificadas localmente, ainda sem merge. As instruções destas telas abaixo se referem a esse branch.

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
| Fontes, captura e notificações de publicações | Backend na main; telas no branch do 6E, verificadas localmente e ainda sem merge |
| Captura real de publicações/intimações | Fornecedor ainda pendente; o ambiente local usa provedor simulado |
| DataJud e consultas periódicas de movimentações | Backend na main; configuração e acompanhamento no branch do 6E, verificadas localmente e ainda sem merge |
| Triagem, distribuição e fechamento diário da controladoria | Backend na main desde o 6D (#34); telas no branch do 6E, verificadas localmente e ainda sem merge |
| Calculadora completa de prazos e agenda unificada | Etapa 7 |
| Automações e fluxos configuráveis | Etapa 8 |
| GED completo, assinatura, financeiro, IA jurídica e portal | Etapas posteriores |
| Indicadores gerenciais completos na página inicial | Ainda não disponíveis |

## Andamentos automáticos

**Andamentos pelo DataJud:** depois de habilitada a integração do escritório, o sistema consulta processos judiciais ativos em ciclos de seis horas, distribuídos ao longo do dia. A primeira consulta traz o histórico público disponível; as seguintes acrescentam novidades, inclusive atos antigos disponibilizados depois. As datas dos atos são mantidas. Uma falha pode atrasar a atualização; o sistema retoma a captura sem apagar o histórico já salvo. DataJud não substitui a captura de publicações/intimações e não disponibiliza o conteúdo de processos sob sigilo. No branch do 6E, a configuração está em **Fontes e integrações → Andamentos e DataJud**, para quem gerencia integrações no escritório. A chave existente nunca é exibida; deixar o campo vazio mantém a chave. Para remover, marque **Remover a chave cadastrada** e salve. O campo da nova chave é limpo após o envio.

## 14. Publicações, triagem e fechamento (telas do branch do 6E)

### Fontes e captura

Abra **Fontes e integrações** no menu. Escolha Nome, OAB ou CNPJ, preencha nome da fonte e valor monitorado e salve. Fontes ativas seguem a captura automática; editar permite desativar sem apagar o histórico. O ambiente AppHost usa provedor simulado; isso não representa captura real de intimações.

Escolha o **Dia da captura (São Paulo)** e use **Consultar captura de …**. O painel mostra execuções e alerta da fonte. **Tentar captura agora** exige gestão de integrações em escopo de escritório e fonte ativa; registra uma solicitação auditada e a executa no Worker. Escolha um dia entre a última ativação da fonte e hoje. Aguarde e use **Atualizar** para conferir o resultado. Repetir uma captura mantém a deduplicação das publicações. Falhas de períodos anteriores a uma mudança do critério da fonte precisam de avaliação técnica; não altere o critério para tentar reproduzir um recorte antigo.

### Fila e notificações

Abra **Publicações**. Filtre por situação, vínculo ou responsável; a URL guarda os filtros e pode ser compartilhada com quem tem o acesso correspondente. A lista carrega por páginas. Selecione um item para ler o texto e preencher a triagem ao lado; o CNJ aparece destacado. O contador do menu mede publicações pendentes visíveis, independentemente de notificações lidas.

J/K navegam, T foca a classificação e P prepara um prazo, quando permitido. Os atalhos não atuam enquanto você digita ou usa uma seleção; desmarque **Habilitar atalhos de uma tecla** para desativá-los. Novidades são avisadas sem apagar o formulário em edição. Termine ou abandone a edição antes de **Atualizar**, trocar a publicação ou mudar filtros.

Em **Notificações**, consulte referências recebidas, abra a publicação autorizada ou marque o aviso como lido. Marcar aviso como lido não conclui triagem. A conexão se recupera automaticamente; a consulta manual continua disponível enquanto reconecta. Quem tem triagem no escritório pode consultar alertas de captura por dia na fila; quem só lê não recebe controles de triagem.

### Tratar uma publicação

A operação técnica está descrita no [guia de ambiente local](ambiente-local.md). Para tratar uma publicação, a pessoa precisa de permissão de leitura e triagem e de acesso ao seu conteúdo. Distribuir exige a permissão de distribuição; atribuir a alguém não concede acesso ao processo. O responsável precisa estar ativo e conseguir ler e triar aquela publicação.

Busque o processo por pasta ou CNJ, selecione-o e use **Vincular processo**, quando necessário. A busca traz até 50 opções por vez, respeitando seu alcance; refine a pesquisa. Em **Distribuição**, escolha um membro e use **Salvar distribuição**; a opção sem atribuição remove a distribuição. A classificação e o destino são confirmados por uma pessoa. Criar prazo ou tarefa exige também a respectiva permissão de cadastro; criar audiência exige editar o processo. Prazo e tarefa têm responsável e datas informadas manualmente. Não há cálculo automático de prazo, cumprimento, calendário ou dupla conferência nesta entrega. A audiência criada aparece na aba **Audiências** já existente do processo; os prazos e tarefas ainda são consultados pela API.

Uma publicação irrelevante ou capturada por engano pode ser encerrada sem processo, com classificação **Outros** e ação **Encerrar como irrelevante/engano**, com justificativa e identificação de quem decidiu. As demais triagens exigem processo vinculado. Escolha a classificação e a ação compatíveis, preencha os dados do destino, marque a confirmação humana e use **Confirmar e salvar triagem**. A gravação da triagem e do destino é conjunta: falhas antes da confirmação da transação desfazem os dois. Se houver falha de resposta, consulte o estado antes de reenviar. Uma publicação já triada não pode ser triada, vinculada ou distribuída novamente.

### Fechar o dia

Abra **Fechamento diário**, escolha o dia e use **Consultar fechamento**. Confira pendências e capturas, marque a confirmação e use **Confirmar fechamento**. O histórico mantém as revisões anteriores e oferece **Carregar mais** quando necessário. O fechamento diário exige `publications.close` em escopo de escritório e alcance de todas as publicações consideradas. O dia usa o recebimento no horário de São Paulo e inclui publicações anteriores ainda pendentes. Captura esperada sem sucesso ou alerta anterior de captura ainda não resolvido impede fechar, mesmo sem publicações na fila; uma execução bem-sucedida com zero resultados conta como sucesso. Desativar a fonte depois da janela de captura não apaga essa obrigação. Falha de captura exige recuperar a captura; descartar publicações não substitui dados ausentes.

O fechamento registra usuário, instante e revisão. Se novas publicações chegarem depois, o dia volta a exigir conferência e novo fechamento, inclusive quando as novas entradas já tiverem sido triadas. A assinatura anterior permanece no histórico. Repetir o fechamento sem novidades devolve o mesmo registro. Essa assinatura é o registro auditado da conta autenticada; não é assinatura digital de um documento.

### Andamentos do processo

Na aba **Andamentos** do processo, consulte origem, data do ato e data de coleta, com paginação e situação da última sincronização. Para registrar manualmente, informe data/hora em São Paulo e descrição e use **Salvar andamento**; é necessário editar o processo. O registro aparece também na **Linha do tempo**, cujo filtro inclui publicações recebidas e andamentos.

## 15. Manutenção do manual

Este é um documento em evolução, não a descrição de funcionalidades futuras como se já estivessem entregues. Após cada melhoria, revisar este manual e o guia de ambiente local. Atualizar as instruções de uso afetadas, os pré-requisitos de acesso e a tabela de disponibilidade; atualizar também o guia quando mudar a forma de iniciar, executar ou conferir o sistema. Se a melhoria não afetar um dos documentos, registrar essa ausência de impacto na verificação da entrega. Os nomes das ações devem ser conferidos na interface e nos testes de navegador.

Nesta edição, as instruções foram conferidas nas rotas, componentes, traduções e fluxos Playwright existentes. Capturas de tela comentadas e aprofundamento por perfil ficam para as próximas revisões do manual; esta edição não inclui imagens.
