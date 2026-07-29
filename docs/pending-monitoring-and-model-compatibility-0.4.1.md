# FlowSentinel 0.4.1 — pendências recorrentes e compatibilidade de modelos

## 1. Objetivo desta evolução

Esta versão corrige as falhas do CI da transição 0.3.0 → 0.4.0 e adiciona o monitoramento recorrente de situações que permanecem pendentes, sem substituir os comportamentos já existentes de detecção de mudanças.

A implementação é aditiva. As definições JSON antigas continuam desserializáveis porque todos os novos campos possuem valores padrão compatíveis e os identificadores já existentes não foram alterados.

## 2. Diagnóstico dos checks do GitHub

Os dois workflows de CI compilaram a solução com zero erros e executaram com sucesso os testes de Application, Desktop e Domain. A falha ocorreu apenas em três testes de `FlowSentinel.Infrastructure.Tests`:

1. `DeveTransformarPlanilhaComBlocosEmEmpresasSituacoesETotais`;
2. `NaoDeveGerarIndicadoresDeValoresVaziosPorPadrao`;
3. `DeveInterpretarMatrizGenericaSemTermosContabeisNoParser`.

Na versão 0.4.0, `ExcelMatrixSettings.GenerateAggregateRecords` passou corretamente a usar `false` como padrão seguro. Isso evita que uma única edição na planilha produza várias mensagens derivadas de agregações. Os três testes, entretanto, continuavam esperando registros `Aggregate` sem habilitar essa opção.

A correção foi aplicada nos testes, que agora declaram `GenerateAggregateRecords = true` somente nos cenários cujo objetivo é validar agregações. O comportamento seguro de produção não foi revertido.

## 3. Modelos existentes antes da 0.4.1

### 3.1 Ciclo da automação

A estrutura genérica já possuía os seguintes conjuntos de regras:

- `EntryRules`: abre uma ocorrência;
- `PersistenceRules`: mantém a ocorrência aberta;
- `CompletionRules`: conclui a ocorrência;
- `SuspensionRules`: suspende temporariamente a ocorrência.

Essas regras continuam em `FlowSentinel.Domain/AutomationDefinition.cs` e são executadas por `FlowSentinel.Application/AutomationExecutor.cs`.

### 3.2 Modelos de ação e notificação

As ações já possuíam três gatilhos:

- `OnOpen`: ao abrir a ocorrência;
- `WhileActive`: enquanto a ocorrência estiver ativa;
- `OnResolved`: ao concluir a ocorrência.

Também já existiam:

- repetição por `RepeatPolicyDefinition`;
- múltiplos canais e destinatários;
- retentativa persistente;
- chave de idempotência;
- histórico de entregas;
- agrupamento `Individual`, `ByEntity` e `SingleMessage` por canal.

Nenhum desses modelos foi removido.

### 3.3 Modelos de planilha

O menu `Modelos` da versão 0.4.0 passou a disponibilizar os perfis:

- `Rp102` — conferência contábil RP-102;
- `PeriodicMatrix` — matriz periódica genérica;
- `TaskTracking` — acompanhamento de tarefas;
- `DocumentControl` — controle de documentos;
- `Custom` — configuração personalizada.

Todos são perfis de configuração de `StructuredWorkbookWizardForm` e utilizam o mesmo parser polimórfico `SectionedMatrix`.

### 3.4 AccountingMonitorWizardForm

`AccountingMonitorWizardForm.cs` foi uma implementação visual intermediária, específica para o vocabulário contábil. Sua responsabilidade foi generalizada em:

- `src/FlowSentinel.Desktop/StructuredWorkbookWizardForm.cs`;
- `src/FlowSentinel.Desktop/WorkbookTemplateProfile.cs`;
- `src/FlowSentinel.Infrastructure/Sources/ExcelSectionedMatrixParser.cs`.

A classe visual antiga não representa um tipo persistido de automação. Sua remoção não apaga nem invalida automações salvas. Os aliases legados `Company`, `Collaborator` e `CurrentStatus` continuam coexistindo com `Entity`, `Owner` e `CurrentValue`.

## 4. O que foi renomeado, substituído ou generalizado

| Responsabilidade anterior | Situação atual | Localização atual |
|---|---|---|
| Assistente contábil específico | Generalizado | `StructuredWorkbookWizardForm.cs` |
| Configuração fixa RP-102 | Convertida em perfil reutilizável | `WorkbookTemplateProfile.cs` |
| `Company` | Alias preservado; nome principal `Entity` | `ExcelSectionedMatrixParser.cs` |
| `Collaborator` | Alias preservado; nome principal `Owner` | `ExcelSectionedMatrixParser.cs` |
| `CurrentStatus` | Alias preservado; nome principal `CurrentValue` | `ExcelSectionedMatrixParser.cs` |
| Botão único RP-102 | Substituído por menu com vários modelos | `MainForm.cs` |
| Destinatário digitado por automação | Mantido e complementado por catálogo | `ContactDirectoryDefinition` e telas de contatos |
| Notificação individual | Mantida como padrão | `NotificationGroupingMode.Individual` |
| Indicadores agregados automáticos na RP-102 | Mantidos, mas opt-in | `GenerateAggregateRecords` e opções do assistente |

Não houve remoção do parser de tabela Excel simples, do modo `SectionedMatrix`, das ações de mudança, dos canais, dos destinatários manuais ou dos agrupamentos.

## 5. Novo ciclo recorrente de pendência

Cada `ActionDefinition` pode agora possuir três conjuntos independentes:

- `Conditions`: condição que inicia o episódio;
- `PersistenceConditions`: condição que mantém o episódio ativo;
- `CompletionConditions`: condição que encerra o episódio.

Exemplo para o cenário `P` → `X`:

```text
Ativação:   Status IN P|PENDENTE
Permanência: Status NOT IN X|CONCLUÍDO
Conclusão:  Status IN X|CONCLUÍDO
```

### Comportamento

1. Quando o status se torna `P`, a ação entra em estado ativo.
2. O primeiro lembrete pode ser criado imediatamente quando a ação opta por `EvaluateWhileActiveOnOpen`; ações antigas mantêm o comportamento anterior.
3. Enquanto o status não atingir um valor esperado, a ação continua ativa.
4. O intervalo mínimo entre lembretes é respeitado.
5. Ao atingir `X`, a ação é encerrada.
6. Entregas ainda pendentes ou em retentativa daquela ação são canceladas seletivamente.
7. Se o registro voltar a `P`, um novo episódio é criado e a contagem reinicia.

O encerramento é controlado primeiro por `CompletionConditions`. Quando esse conjunto não existe, a ação volta ao comportamento legado de reavaliar `Conditions` ou `PersistenceConditions`.

## 6. Frequência, repetição e horários

`RepeatPolicyDefinition` continua controlando:

- `Enabled`;
- `IntervalSeconds`;
- `MaxExecutions`, em que zero significa ilimitado;
- `ResetOnConditionReentry`, que inicia uma nova contagem quando a pendência reaparece.

`ActionScheduleDefinition` acrescenta:

- dias permitidos da semana;
- horário inicial;
- horário final;
- janelas normais, como `08:00–18:00`;
- janelas que atravessam a meia-noite, como `22:00–06:00`.

A agenda restringe o momento de criação da entrega. A verificação da fonte continua ocorrendo no intervalo da automação, garantindo que a conclusão seja percebida mesmo fora da janela de envio.

## 7. Deduplicação

A deduplicação continua baseada em índice único de `IdempotencyKey` na tabela `deliveries`.

Para ações antigas, o formato da chave foi preservado. Para ações com episódios recorrentes, a chave inclui:

- ocorrência;
- ação;
- canal;
- destinatário;
- número do episódio;
- número da execução dentro do episódio.

Isso evita:

- duplicidade após reinicialização;
- colisão entre uma pendência antiga e uma nova pendência do mesmo registro;
- reenvio da mesma execução após falha entre a gravação da entrega e a atualização do estado.

## 8. Persistência e histórico

A versão interna do SQLite passa para 5 por migração aditiva. Nenhuma tabela existente é recriada.

O histórico fica distribuído da seguinte forma:

- `automations`: última execução, próxima execução e último erro;
- `occurrences`: abertura, última avaliação, resolução e snapshot atual;
- `deliveries`: cada notificação, tentativa, canal, destinatário, status e erro;
- `action_runtime_states`: estado ativo, episódio, contagem e último agendamento de cada ação;
- `automation_execution_history`: resumo de cada verificação da automação;
- `record_change_history`: snapshots anterior e atual e lista de campos alterados.

Assim, verificações sem mudança continuam registradas no resumo da execução, mudanças recebem histórico detalhado e notificações permanecem auditáveis individualmente.

## 9. Compatibilidade

As automações 0.3.0 e 0.4.0 permanecem compatíveis porque:

- propriedades novas têm inicializadores padrão;
- `EvaluateWhileActiveOnOpen` é `false` por padrão e somente novos lembretes guiados o habilitam;
- `Schedule.Enabled` é `false` por padrão;
- `PersistenceConditions` e `CompletionConditions` são opcionais;
- `ResetOnConditionReentry` é `false` por padrão;
- `CancelPendingWhenConditionFails` é `false` por padrão;
- chaves antigas de idempotência mantêm o formato anterior quando não existe episódio;
- aliases legados da planilha são preservados;
- os IDs das ações, automações, canais e ocorrências não são alterados.

## 10. Arquivos responsáveis

- Domínio e validação: `src/FlowSentinel.Domain/AutomationDefinition.cs`.
- Tipos de regras: `src/FlowSentinel.Domain/Enums.cs`.
- Execução e ciclo de pendência: `src/FlowSentinel.Application/AutomationExecutor.cs`.
- Contratos de persistência: `src/FlowSentinel.Application/Abstractions.cs`.
- Estado e histórico: `src/FlowSentinel.Application/StoreModels.cs`.
- Banco SQLite e migração: `src/FlowSentinel.Infrastructure/Persistence/FlowStore.cs`.
- Mapeamento EF Core: `FlowSentinelDbContext.cs` e `Entities.cs`.
- Configuração avançada: `src/FlowSentinel.Desktop/ActionEditorForm.cs`.
- Configuração guiada para planilhas: `src/FlowSentinel.Desktop/StructuredWorkbookWizardForm.cs`.
