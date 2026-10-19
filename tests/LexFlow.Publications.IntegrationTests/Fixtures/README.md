# Contrato público do DataJud

`datajud-page.json` é uma fixture sintética e anonimizada baseada no formato das respostas dos exemplos oficiais, sem chave de acesso, partes ou dados pessoais. As datas, identificadores e o CNJ são usados apenas para testar o contrato; o histórico fictício não representa um processo real.

Fontes consultadas em 2026-10-19:

- https://datajud-wiki.cnj.jus.br/api-publica/glossario/
- https://datajud-wiki.cnj.jus.br/api-publica/exemplos/exemplo1/
- https://datajud-wiki.cnj.jus.br/api-publica/exemplos/exemplo3/

Os testes alteram esta resposta para simular paginação, reordenação, falhas HTTP, sigilo e resposta incompleta. Nenhuma consulta real ao CNJ é feita na suíte ou no CI.
