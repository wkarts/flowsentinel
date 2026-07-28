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

## Datas e compatibilidade com SQLite

Os contratos do domínio continuam utilizando `DateTimeOffset`, preservando o instante e o fuso na comunicação entre os módulos. Na persistência SQLite, os valores são convertidos para `DateTime` em UTC, pois o provedor do Entity Framework não suporta comparação, ordenação e agregações de `DateTimeOffset` no servidor.

A inicialização controla a revisão do armazenamento por `PRAGMA user_version`. Bancos criados por versões anteriores têm seus campos temporais normalizados em UTC de forma transacional e idempotente, sem exclusão das automações, ocorrências, canais ou entregas existentes.

## Desktop, tray e parâmetros de execução

O Desktop mantém as preferências de experiência em `%LocalAppData%\FlowSentinel\desktop-settings.json`. O mesmo serviço implementa `IWorkerRuntimeSettings`, permitindo que os workers consultem intervalos, limites e flags atualizados durante a execução.

O Windows Service utiliza `JsonFileWorkerRuntimeSettings` sobre `%ProgramData%\FlowSentinel\service-settings.json`. Esse arquivo é atualizado pela tela de configurações do Desktop e relido quando sua data de modificação muda.

A distribuição Desktop inclui o serviço na pasta `service`, mas os processos continuam independentes:

```text
FlowSentinel.exe
└── UI, tray e worker do usuário atual

service\FlowSentinel.Service.exe
└── Worker do Windows Service com dados em ProgramData
```

A logo da WWSoftware's Sistemas e Tecnologias é um ativo institucional do desenvolvedor e não substitui automaticamente a identidade principal do produto.
