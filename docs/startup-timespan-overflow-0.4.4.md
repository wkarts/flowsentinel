# FlowSentinel 0.4.4 — correção do temporizador de inicialização

## Sintoma

Ao iniciar o Desktop, o splash era encerrado com a mensagem:

```text
System.OverflowException: TimeSpan overflowed because the duration is too long.
```

A pilha apontava para `Program.WaitWithMessagePump`, na expressão que calculava o intervalo desde a última atualização visual.

## Causa raiz

A versão 0.4.3 inicializava o marcador da última atualização com `TimeSpan.MinValue`:

```csharp
var lastProgressRefresh = TimeSpan.MinValue;
```

Logo na primeira iteração, o código executava:

```csharp
watch.Elapsed - lastProgressRefresh
```

Como `watch.Elapsed` é positivo e `TimeSpan.MinValue` representa o menor valor possível, a subtração ultrapassava a faixa suportada por `TimeSpan` e lançava `OverflowException` antes que a etapa do banco pudesse ser acompanhada.

## Correção

O marcador passou a ser anulável:

```csharp
TimeSpan? lastProgressRefresh = null;
```

A primeira atualização é reconhecida explicitamente, sem qualquer subtração. Nas atualizações seguintes, a diferença é calculada somente entre dois tempos não negativos obtidos do mesmo `Stopwatch`.

O ciclo também captura `watch.Elapsed` uma única vez por iteração, usando o mesmo valor para:

- decidir a atualização visual;
- atualizar o texto do splash;
- verificar o timeout.

Quando uma etapa falha, seu `CancellationTokenSource` é cancelado antes da propagação da exceção.

## Compatibilidade

A correção não altera:

- banco SQLite ou migrações;
- automações existentes;
- modelos de planilha;
- canais, contatos ou grupos;
- regras de mudança e pendência;
- histórico de ocorrências e entregas;
- formato JSON das definições.

Não é necessário excluir `flowsentinel.db`.
