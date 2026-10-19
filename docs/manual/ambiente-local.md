# Iniciar o LexFlow no computador

Guia do ambiente de desenvolvimento para explorar as telas do [manual de utilização](README.md). Esta configuração executa os serviços localmente; não publica o sistema na internet. Revisado no 6C, em 2026-10-19: os comandos e endereços permanecem os mesmos; o branch `codex/etapa-6c-datajud-integracoes` acrescenta consultas DataJud no backend.

## 1. O que precisa estar disponível

- Docker Desktop aberto e com o mecanismo de contêineres em execução.
- SDK .NET 10 e as dependências JavaScript configuradas no projeto.
- Repositório em `C:\Users\Admin\Desktop\ProjetoLaw`.
- Portas locais 4200 (aplicação), 5238 (API) e 8025 (caixa de e-mails) disponíveis.

No computador usado no desenvolvimento, esses pré-requisitos já foram usados na verificação do 6B. A primeira inicialização em outro computador pode exigir a instalação das ferramentas, restauração dos pacotes e download das imagens Docker.

## 2. Iniciar pelo PowerShell

Abra o PowerShell e execute:

```powershell
Set-Location C:\Users\Admin\Desktop\ProjetoLaw
dotnet run --project src/Aspire/LexFlow.AppHost
```

Mantenha esse terminal aberto. O AppHost coordena banco, API, Worker, aplicação web e os demais serviços locais; não é preciso iniciar somente o Angular em outro terminal para esse uso.

A inicialização pode levar alguns minutos, especialmente ao baixar imagens ou iniciar o antivírus pela primeira vez. O processo de migrations pode terminar com sucesso e aparecer como concluído; API, Worker e aplicação web permanecem em execução.

Abra [LexFlow](http://localhost:4200) quando a aplicação estiver pronta. O endereço deve responder normalmente, sem erro de conexão. Para uma conferência técnica, a API tem a rota [de prontidão](http://localhost:5238/health/ready).

O terminal também apresenta o endereço do painel Aspire. Use o endereço exibido naquela execução para acompanhar os recursos e logs; não salve o token temporário do painel no manual nem o compartilhe.

## 3. Endereços úteis

| Endereço | Finalidade |
| --- | --- |
| [http://localhost:4200](http://localhost:4200) | Usar as telas do LexFlow |
| [http://localhost:8025](http://localhost:8025) | Abrir o Mailpit e ler e-mails de cadastro, convites e recuperação |
| [http://localhost:5238/health/ready](http://localhost:5238/health/ready) | Conferir a prontidão da API |

Para o primeiro acesso, siga a seção inicial do manual. Não há credencial compartilhada de demonstração definida por este guia: crie sua conta pela tela ou use uma conta que você já cadastrou no banco local.

## 4. Parar e voltar depois

No terminal que está executando o AppHost, pressione **Ctrl+C** e aguarde o encerramento. Para voltar, execute o mesmo comando de inicialização.

O ambiente padrão usa volumes persistentes para banco e armazenamento. Parar e iniciar normalmente preserva os cadastros locais. Remover os volumes Docker pode apagar esses dados; isso não faz parte do procedimento normal de encerramento.

Enquanto estiver explorando a aplicação, não execute `LexFlow.AppHost.Tests` em paralelo: esse projeto de testes sobe outro AppHost e usa as mesmas portas. Também evite rodar a suíte completa de navegador enquanto faz sua navegação manual.

## 5. Dúvidas frequentes

**A página não abre:** confira se Docker e AppHost estão em execução e aguarde os serviços ficarem prontos. Confira a porta informada e o painel Aspire. Se houver outra instância, use a já existente ou encerre apenas a instância que você iniciou antes de tentar novamente.

**Perfil HTTP do Aspire:** o comando normal acima usa o perfil padrão. Se iniciar explicitamente com `--launch-profile http`, defina antes `$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT='true'` no mesmo PowerShell, como no CI. Essa variável permite o transporte local desse perfil; não altera a configuração de produção.

**O e-mail não chegou à minha caixa:** no ambiente local, procure a mensagem no Mailpit, pelo destinatário usado no cadastro. Confira no painel Aspire se o Worker e o Mailpit estão em execução.

**A página Início está simples:** os indicadores completos ainda não foram entregues. Use o menu lateral para explorar as telas existentes.

**Um botão ou aba não aparece:** confira as permissões da conta, o escritório ativo e o alcance do processo. Parte das funcionalidades planejadas ainda não tem tela.

**A importação continua processando:** o trabalho depende do Worker. Confira a situação no painel Aspire e a mensagem da tela antes de reenviar o arquivo.

**Aparecem dados de ensaios anteriores:** o banco local é persistente. Crie um escritório de demonstração próprio para organizar seu teste. Este guia não apaga nem reinicializa o banco.

## 6. Limites da demonstração

Os dados gravados são dados do ambiente local, e as ações feitas na interface realmente alteram esse banco. Os provedores simulados habilitados no AppHost não equivalem a integração real com um fornecedor. O uso local serve para conhecer e validar a versão em desenvolvimento; o piloto de produção é uma etapa posterior.

## 7. Integração de andamentos (6C, operação técnica)

As movimentações automáticas ficam desabilitadas por padrão, inclusive para escritórios locais existentes. O fake de publicações habilitado no AppHost é independente dessa configuração. A tela de configuração está prevista no 6E; até lá, a operação técnica usa a API autenticada, no escritório ativo, com `settings.integrations.manage` em escopo de escritório:

- `GET /api/v1/movement-integration`: consulta o provedor e indica se há chave cadastrada, sem devolvê-la.
- `PUT /api/v1/movement-integration`: informa `provider` (`disabled`, `fake` ou `dataJud`), `apiKey` opcional e `clearApiKey` opcional. Ao omitir a chave, mantém a anterior; para substituí-la, envia uma nova. Para removê-la, usa `clearApiKey: true` com o provedor desabilitado ou fake. Não envia remoção e substituição juntas. DataJud exige chave.
- `GET /api/v1/cases/{id}/movement-sync`: acompanha última tentativa, último sucesso, página pendente e código de falha; exige `cases.read` e acesso ao processo.

A chave pública vigente é obtida na [wiki oficial do CNJ](https://datajud-wiki.cnj.jus.br/api-publica/acesso/). Cadastre-a pela API; não a coloque no código, manual, logs ou captura de tela. A proteção usa o key ring persistido já compartilhado por API e Worker. Preserve esse key ring e o banco ao reiniciar. Se a chave for recusada ou não puder ser aberta, substitua-a pela configuração autenticada; salvar a configuração inicia uma nova revisão, mantendo os andamentos anteriores e permitindo reprocessar sem duplicação.

O Worker verifica os processos elegíveis a cada cinco minutos; cada processo tem uma janela de seis horas. Páginas pendentes ou falhas são tentadas novamente após cinco minutos. A consulta usa apenas endpoints oficiais e tem limite de concorrência/tempo/tamanho de resposta. Cursor inválido reinicia a varredura na tentativa seguinte. O fake identifica seus movimentos como origem de provedor simulado; não confundir com retorno do CNJ. Não é necessário cadastrar uma chave para continuar explorando as telas atuais.
