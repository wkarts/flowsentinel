# FlowSentinel 0.4.2 — valores genéricos em pendências recorrentes

## O recurso não depende de P ou X

Os códigos `P` e `X` são apenas valores iniciais do perfil de planilha. O motor de regras recebe texto configurável e pode utilizar como condição de pendência ou conclusão:

- uma letra: `A`;
- um número: `7`;
- uma palavra: `PENDENTE`;
- uma frase: `AGUARDANDO DOCUMENTAÇÃO`;
- vários valores alternativos: `P|AGUARDANDO|EM ANÁLISE`.

No assistente, valores múltiplos podem ser separados por `|`, `;`, vírgula ou quebra de linha. Espaços externos são removidos e, por padrão, a comparação não diferencia maiúsculas de minúsculas.

## Exemplo

```text
Ativação:    Status IN 7|AGUARDANDO DOCUMENTAÇÃO
Permanência: Status NOT IN 9|DOCUMENTAÇÃO APROVADA
Conclusão:   Status IN 9|DOCUMENTAÇÃO APROVADA
```

Depois de ativado, o episódio permanece recorrente enquanto o valor atual não corresponder a uma condição de conclusão. A recorrência para automaticamente quando a conclusão é satisfeita, quando o registro desaparece e a automação usa `MissingRecordBehavior.Resolve`, ou quando a automação/ação é desativada.

## Escopo genérico

O assistente de matriz utiliza o campo normalizado `Status`. O editor avançado pode aplicar o mesmo ciclo a qualquer campo mapeado por uma fonte Excel, CSV, texto ou banco de dados. Para números também estão disponíveis operadores como maior que, menor que e intervalos construídos por grupos de regras.
