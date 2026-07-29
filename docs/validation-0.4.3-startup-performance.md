# Validação — FlowSentinel 0.4.3

Data: 29/07/2026

## Escopo

- correção do bloqueio no splash;
- manutenção do WinForms na thread STA;
- migração SQLite incremental;
- redução de consultas e gravações por registro;
- redução de logs de infraestrutura;
- uso da marca branca no splash;
- compatibilidade com automações e pendências recorrentes existentes.

## Validações estruturais

O script `scripts/static-validate-0.4.3.py` verificou:

- 4 arquivos JSON válidos;
- 7 arquivos YAML válidos;
- 13 arquivos XML, projetos e manifestos válidos;
- 90 arquivos C# com delimitadores balanceados;
- 9 projetos referenciados pela solução;
- 13 referências entre projetos existentes;
- presença das proteções de startup, migração, logging, logo, cache de ocorrências e testes de regressão.

Resultado: aprovado.

## Banco operacional analisado

Foi utilizada uma cópia do banco anexado, sem alterar o arquivo original:

- versão interna anterior: 2;
- automações: 1;
- canais: 3;
- ocorrências: 2.500;
- entregas: 254;
- tamanho do banco principal: aproximadamente 9,6 MB;
- WAL: aproximadamente 5,6 MB.

A normalização SQL em lote equivalente à nova migração foi executada sobre uma cópia. O processamento das colunas de data terminou em aproximadamente 0,012 segundo e não regravou linhas que já estavam no formato esperado. A implementação anterior carregava e marcava todas as ocorrências, entregas e históricos como modificados. Em um ensaio adicional, a limpeza em lote removeu 10.000 estados inativos e preservou um estado ativo em aproximadamente 0,023 segundo. Todas as conexões passam a utilizar timeout de contenção de 15 segundos.

## Logs operacionais analisados

Os dois arquivos diários observados possuíam aproximadamente 28 MB e 43 MB. A maior parte era composta por comandos informativos do Entity Framework executados repetidamente. A versão 0.4.3 filtra essas categorias para `Warning` no provedor de arquivo e utiliza flush periódico para mensagens informativas.

## Regressões adicionadas

- ações de mudança sem condição atendida não criam estados redundantes;
- a migração remove somente estados inativos, sem episódio, execução ou agendamento;
- estados ativos de pendências são preservados;
- o heartbeat das ocorrências é atualizado por janela, não em todos os ciclos;
- a versão interna esperada do armazenamento é 6.

## Marca

O arquivo `WWSoftwaresDeveloperLogoWhite.png` possui 500 × 500 pixels, canal alfa e fundo transparente. Ele foi incluído no projeto para cópia no build e na publicação.

## Limitação do ambiente

O ambiente de validação não possui o SDK .NET 10.0.301 definido em `global.json`. Por isso, `dotnet build` e `dotnet test` não foram executados localmente. O código foi validado estruturalmente, os projetos e manifestos foram conferidos e os testes foram adicionados para execução no GitHub Actions.
