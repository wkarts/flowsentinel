# Modelo de automação

## Estrutura

```text
AutomationDefinition
├── Sources[]
├── EntryRules
├── PersistenceRules
├── CompletionRules
├── SuspensionRules
├── Actions[]
│   ├── Conditions (ativação)
│   ├── PersistenceConditions
│   ├── CompletionConditions
│   ├── Repeat
│   ├── Schedule
│   ├── Channels[]
│   └── Recipients[]
└── ContactGroups[]
```

## Vários clientes na mesma fonte

Cada linha é identificada por `keyFields`. Para títulos financeiros, prefira uma chave de negócio estável:

```json
"keyFields": ["ClienteId", "Documento", "Parcela"]
```

Não use apenas o número da linha, pois ela pode mudar quando a planilha for ordenada.

## Regras específicas por cliente ou situação

Crie ações diferentes com `conditions`:

```text
Ação A: ClienteId = 100 e Status = Pendente
Ação B: Status = Atrasado e DiasAtraso > 5
Ação C: Filial = Salvador
```

Todas podem coexistir na mesma automação e no mesmo registro.

## Destinatários

- `Fixed`: endereço informado na definição.
- `Field`: endereço obtido de uma coluna/campo.
- `Group`: grupo reutilizável de contatos.

Uma coluna pode conter mais de um destinatário separado por `;` ou `,`.

## Ciclo recorrente por ação

Para lembrar enquanto uma condição ainda não foi resolvida, use uma ação `WhileActive` com:

```json
{
  "trigger": "WhileActive",
  "evaluateWhileActiveOnOpen": true,
  "repeat": {
    "enabled": true,
    "intervalSeconds": 3600,
    "maxExecutions": 0,
    "resetOnConditionReentry": true
  },
  "cancelPendingWhenConditionFails": true,
  "schedule": {
    "enabled": true,
    "startTime": "08:00",
    "endTime": "18:00",
    "daysOfWeek": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
  }
}
```

`Conditions` abre o episódio, `PersistenceConditions` o mantém ativo e `CompletionConditions` o encerra. Propriedades ausentes preservam o comportamento das definições anteriores.
