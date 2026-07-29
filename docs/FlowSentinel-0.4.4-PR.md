# Pull Request — FlowSentinel 0.4.4

## Branch

`fix/v0.4.4-startup-timespan-overflow`

## Título

`fix: corrige overflow do temporizador durante a inicialização`

## Contexto

A versão 0.4.3 passou a acompanhar o tempo decorrido de cada etapa no splash. O marcador inicial foi definido como `TimeSpan.MinValue` e usado em uma subtração com `Stopwatch.Elapsed`, provocando `OverflowException` na primeira atualização visual.

## Alterações

- substitui o marcador `TimeSpan.MinValue` por estado anulável seguro;
- trata a primeira atualização sem cálculo de diferença;
- preserva a atualização do splash a cada 250 ms;
- reutiliza o mesmo valor de tempo decorrido durante cada iteração;
- cancela a tarefa da etapa quando a inicialização falha;
- adiciona testes para primeira atualização, intervalo e valor máximo;
- incrementa a versão para 0.4.4.

## Compatibilidade

Nenhuma automação, modelo, canal, contato, histórico, banco ou regra de pendência foi removido ou alterado.

## Validação

- [x] erro reproduzido a partir da pilha anexada;
- [x] `TimeSpan.MinValue` removido do fluxo de inicialização;
- [x] teste de regressão adicionado;
- [x] JSON, YAML e XML verificados;
- [x] estrutura lexical dos arquivos C# verificada;
- [x] `git diff --check` aprovado;
- [x] patch e MBOX aplicados sobre a versão 0.4.3;
- [ ] build e testes executáveis a confirmar no GitHub Actions.

## Commit

`fix: corrige overflow do temporizador durante a inicialização`

## Merge sugerido

`fix: publica FlowSentinel 0.4.4 com inicialização corrigida`
