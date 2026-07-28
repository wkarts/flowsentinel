# Arquitetura do FlowSentinel

## Fluxo operacional

```text
Fontes
  ↓
Leitores normalizados
  ↓
Correlação pela chave do registro
  ↓
Motor de regras
  ↓
Ocorrência persistente
  ↓
Planejamento de ações
  ↓
Entregas por canal e destinatário
  ↓
Fila, retentativa e auditoria
```

## Unidade de processamento

A unidade de processamento é o registro, e não o arquivo. Uma planilha com mil clientes pode produzir mil estados independentes.

## Chaves compostas

Cada fonte define `keyFields`. Exemplo:

```json
"keyFields": ["ClienteId", "Documento", "Parcela"]
```

Fontes secundárias são correlacionadas quando produzem a mesma chave.

## Campos

Campos da fonte primária ficam disponíveis de duas formas:

```text
Status
clientes.Status
```

Campos de fontes secundárias devem ser acessados com alias:

```text
pagamentos.DataBaixa
retorno.StatusRetorno
```

## Ciclo de vida

- Entrada: cria a ocorrência.
- Permanência: confirma que a situação continua ativa.
- Suspensão: pausa notificações sem concluir.
- Conclusão: resolve e cancela entregas futuras.
- Ausência: pode ignorar ou resolver a ocorrência.

## Idempotência

Cada entrega possui uma chave SHA-256 formada por ocorrência, ação, canal, destinatário e número da execução. A restrição única no SQLite impede reenvio acidental.

## Extensibilidade

Novas fontes implementam `IDataSourceReader`. Novos canais implementam `INotificationChannel`. O motor não depende da tecnologia de origem ou destino.
