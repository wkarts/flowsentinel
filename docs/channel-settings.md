# Configurações de canais

## Evolution API

```json
{
  "baseUrl": "https://evolution.exemplo.com",
  "apiKey": "dpapi-machine:...",
  "apiKeyHeader": "apikey",
  "instance": "empresa-principal",
  "apiVersion": "V2",
  "sendTextPathTemplate": "/message/sendText/{instance}",
  "connectionStatePathTemplate": "/instance/connectionState/{instance}",
  "connectPathTemplate": "/instance/connect/{instance}",
  "payloadMode": "V2",
  "timeoutSeconds": 30
}
```

Na Evolution API V2, os caminhos padrão adotados são `GET /instance/connect/{instance}`, `GET /instance/connectionState/{instance}` e `POST /message/sendText/{instance}`. Para instalações V1 ou revisões personalizadas, use `apiVersion`/`payloadMode` como `V1` e sobrescreva os caminhos sem recompilar.

## Telegram

```json
{
  "botToken": "dpapi-user:...",
  "parseMode": "HTML",
  "disableNotification": false,
  "timeoutSeconds": 30
}
```

O destinatário deve ser o Chat ID, grupo ou canal aceito pela API do Telegram.

## SMTP

```json
{
  "host": "smtp.exemplo.com",
  "port": 587,
  "security": "StartTls",
  "username": "usuario@exemplo.com",
  "password": "dpapi-user:...",
  "fromAddress": "usuario@exemplo.com",
  "fromName": "FlowSentinel",
  "isHtml": false,
  "timeoutSeconds": 30
}
```

Valores possíveis de `security`: `None`, `Auto`, `SslOnConnect`, `StartTls` e `StartTlsWhenAvailable`.
