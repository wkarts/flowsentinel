# Pull Request — FlowSentinel 0.4.3

## Título

`fix: elimina travamento de inicialização e reduz carga do monitoramento`

## Branch

`fix/v0.4.3-startup-performance`

## Objetivo

Corrigir o bloqueio observado no splash e a falta de resposta da janela principal, preservando integralmente as funcionalidades entregues entre as versões 0.3.0 e 0.4.2.

## Alterações principais

- mantém o WinForms e seu message loop na mesma thread STA;
- executa banco e catálogos fora da thread visual;
- adiciona limites e feedback de tempo às etapas do splash;
- inicializa o banco antes dos serviços em segundo plano;
- usa a marca branca fornecida no painel escuro;
- substitui a migração linha a linha por comandos SQL em lote;
- limpa somente estados inativos e vazios deixados pelas versões 0.4.1/0.4.2;
- carrega ocorrências e estados uma única vez por execução;
- elimina consultas e updates individuais em registros inalterados;
- consolida o heartbeat das ocorrências em cinco minutos;
- reduz logs informativos de infraestrutura;
- adiciona atraso inicial curto aos workers para liberar a interface.

## Compatibilidade

- não remove modelos, ações, regras, canais, contatos ou grupos;
- não exige exclusão do banco;
- mantém pendências recorrentes genéricas e seus episódios;
- preserva automações das versões anteriores.

## Validação esperada no CI

```powershell
dotnet restore FlowSentinel.sln
dotnet build FlowSentinel.sln --configuration Release --no-restore
dotnet test FlowSentinel.sln --configuration Release --no-build
```

## Commit sugerido

`fix: elimina travamento de inicialização e otimiza ciclos do monitoramento`

## Merge sugerido

`fix: publica FlowSentinel 0.4.3 com inicialização responsiva`
