# Validação — FlowSentinel 0.4.1

## Evidência recebida

Foram analisados os dois pacotes de logs dos workflows de `push` e `pull_request`.

### Resultado anterior

- restauração: concluída;
- análise de formatação: concluída;
- compilação: 0 avisos e 0 erros;
- Application.Tests: 11 aprovados;
- Desktop.Tests: 9 aprovados;
- Domain.Tests: 7 aprovados;
- Infrastructure.Tests: 12 aprovados e 3 reprovados;
- CodeQL: concluído sem alertas no código alterado.

### Falhas anteriores

- `DeveTransformarPlanilhaComBlocosEmEmpresasSituacoesETotais`;
- `NaoDeveGerarIndicadoresDeValoresVaziosPorPadrao`;
- `DeveInterpretarMatrizGenericaSemTermosContabeisNoParser`.

A causa foi a ausência de `GenerateAggregateRecords = true` nos próprios testes depois que o padrão seguro da 0.4.0 passou a desabilitar agregações automáticas.

## Correção aplicada

Os três testes passaram a habilitar explicitamente a geração de agregados. O código de produção não voltou ao comportamento ruidoso anterior.

## Novas verificações de regressão

- ausência de agregados quando a capacidade está desabilitada;
- agenda por dias e horários;
- janela que atravessa a meia-noite;
- compatibilidade de JSON antigo;
- persistência do estado por episódio;
- reinício da contagem após nova pendência;
- cancelamento seletivo de entregas;
- histórico de execução e mudança;
- ciclo integrado de pendência, conclusão e reabertura.

## Validação local desta correção

O validador `scripts/static-validate-0.4.1.py` concluiu com sucesso:

- 4 arquivos JSON válidos;
- 7 arquivos YAML válidos;
- 13 arquivos XML, projetos e manifestos válidos;
- 90 arquivos C# com delimitadores e estrutura léxica balanceados;
- 9 projetos da solução localizados;
- 13 referências entre projetos conferidas;
- proteções específicas para agregações opcionais e ciclo recorrente localizadas.

`git diff --check` também foi aprovado.

## Limitação deste ambiente

O SDK .NET 10 exigido pelo `global.json` não está instalado no ambiente de geração. A tentativa de obter o SDK oficial foi impedida pela ausência de resolução DNS no contêiner. Portanto, a nova árvore ainda precisa passar por `dotnet format`, `dotnet build` e `dotnet test` no GitHub Actions antes do merge. Essa limitação não altera a evidência recebida: a versão 0.4.0 compilava sem erros e falhava somente nos três testes de agregação descritos acima.
