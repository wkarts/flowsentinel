# Validação — FlowSentinel 0.4.2

## Erro reproduzido pelos logs

Os dois workflows falharam somente no teste:

```text
ExcelSectionedMatrixParserTests.DeveTransformarPlanilhaComBlocosEmEmpresasSituacoesETotais
Assert.Contains() Failure — linha 70
```

O teste aplicava preenchimento amarelo em `E2` e esperava `IsHighlighted = true`, porém a configuração não habilitava `ExcelMatrixSettings.IncludeFormatting`. Desde a 0.4.0, a leitura de formatação é opt-in para reduzir custo e evitar notificações visuais não solicitadas.

## Correção

Foi acrescentado `IncludeFormatting = true` somente ao cenário de teste que valida formatação. O parser e os padrões de produção não foram afrouxados.

Também foi adicionado teste do motor de regras para valores genéricos de pendência: letra, número, palavra e frase.

## Limitação desta validação local

O ambiente de empacotamento não possui o SDK .NET 10.0.301 indicado em `global.json`. Foram executadas validações estáticas, de estrutura, manifesto, patch e integridade dos pacotes. A confirmação executável final deve ocorrer no GitHub Actions.
