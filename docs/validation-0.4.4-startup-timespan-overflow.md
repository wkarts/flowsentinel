# Validação — FlowSentinel 0.4.4

## Falha analisada

```text
System.OverflowException: TimeSpan overflowed because the duration is too long.
at System.TimeSpan.op_Subtraction(TimeSpan t1, TimeSpan t2)
at FlowSentinel.Desktop.Program.WaitWithMessagePump(...)
```

## Proteções adicionadas

- o fluxo não contém mais `TimeSpan.MinValue`;
- o marcador de atualização é `TimeSpan?` e inicia como `null`;
- a primeira atualização retorna `true` sem subtração;
- diferenças são calculadas somente entre valores válidos e crescentes;
- o intervalo continua definido em 250 ms;
- `TimeSpan.MaxValue` com marcador inicial nulo não produz overflow;
- falhas de etapa cancelam o token associado.

## Testes de regressão

`ProgramStartupTimingTests` cobre:

1. primeira atualização com `TimeSpan.Zero`;
2. limite de 249/250 ms;
3. primeira atualização com `TimeSpan.MaxValue`.

## Limitação do ambiente

O SDK .NET 10.0.301 não está instalado no ambiente de geração. As validações executáveis de build e testes devem ser confirmadas pelo GitHub Actions.
