# Pull Request — FlowSentinel 0.4.1

## Branch

```text
fix/v0.4.1-ci-pending-monitoring
```

## Título

```text
fix: corrige CI e adiciona monitoramento recorrente de pendências
```

## Descrição

### Contexto

A versão 0.4.0 alterou corretamente o padrão de agregações de planilhas para evitar cascatas de mensagens. Três testes de infraestrutura ainda pressupunham que agregações eram criadas implicitamente e passaram a falhar, embora a compilação e os demais testes estivessem concluindo com sucesso.

Esta PR corrige esses testes sem reativar agregações por padrão e acrescenta, de forma compatível, o ciclo recorrente de pendências solicitado.

### Correções do CI

- habilita `GenerateAggregateRecords` explicitamente apenas nos testes de agregação;
- mantém `GenerateAggregateRecords = false` como padrão de produção;
- adiciona asserção que protege o comportamento sem agregações;
- preserva as correções e a estrutura profissional da 0.4.0.

### Monitoramento recorrente

- condições separadas de ativação, permanência e conclusão por ação;
- repetição enquanto o valor esperado ainda não foi alcançado;
- encerramento automático ao atingir a condição esperada;
- cancelamento seletivo de entregas pendentes da ação concluída;
- reinício da contagem quando uma nova pendência surge;
- agenda por dias e horários;
- configuração no assistente de planilhas e no editor avançado.

### Deduplicação e histórico

- estado de cada ação persistido por ocorrência;
- número de episódio incluído na idempotência apenas para o novo ciclo recorrente;
- formato legado preservado para ações antigas;
- resumo persistente de execuções;
- histórico detalhado de mudanças de registros;
- histórico existente de ocorrências e entregas preservado.

### Compatibilidade

Nenhum modelo de monitoramento ou notificação foi removido. `AccountingMonitorWizardForm` continua substituído pela implementação genérica `StructuredWorkbookWizardForm`, e o perfil RP-102 permanece disponível no menu de modelos. Ações antigas continuam utilizando os mesmos gatilhos, regras, IDs, aliases e canais.

## Testes adicionados ou ajustados

- agregações explicitamente habilitadas nos três testes que dependem delas;
- padrão sem agregações protegido por teste;
- validação de agenda e janela noturna;
- desserialização de ação antiga sem os novos campos;
- persistência de episódios e reinício da contagem;
- cancelamento seletivo por ação;
- persistência de histórico;
- ciclo integrado `P → X → P`.

## Checklist antes do merge

```powershell
dotnet restore FlowSentinel.sln
dotnet format analyzers FlowSentinel.sln --verify-no-changes --no-restore --severity error
dotnet build FlowSentinel.sln -c Release --no-restore
dotnet test FlowSentinel.sln -c Release --no-build
```

## Commit sugerido

```text
fix: corrige CI e implementa ciclo recorrente de pendências
```

## Merge sugerido

```text
fix: publica FlowSentinel 0.4.1 com pendências recorrentes
```
