# Changelog


Todas as alterações relevantes deste projeto serão documentadas neste arquivo.
O formato segue Keep a Changelog e o projeto utiliza versionamento semântico.

## [Não publicado]

### Corrigido

- Corrigida a atribuição de parâmetros SQL nulos em `DatabaseSourceReader`, eliminando o erro de compilação `CS0019`.
- Definido `FlowSentinel.Infrastructure` como `net10.0-windows`, refletindo o uso intencional do Windows DPAPI e eliminando avisos `CA1416`.
- Adicionada permissão `actions: read` ao workflow CodeQL para leitura de metadados das execuções.

- Corrige o erro `WFO1000` do analisador WinForms, restringindo propriedades internas de estado e resultado dos formulários.
- Remove a referência redundante a `System.Text.Encoding.CodePages` no .NET 10, eliminando o aviso `NU1510`.
- Atualiza e fixa a família `SQLitePCLRaw` em `2.1.12`, removendo a dependência nativa vulnerável `2.1.11` (`GHSA-2m69-gcr7-jv3q`).

### Planejado

- Editor visual avançado de grupos lógicos.
- Conectores adicionais por plugins externos.
- Administração centralizada opcional.

## [0.1.0] - 2026-07-28

### Adicionado

- Aplicação Windows em bandeja e executável de Windows Service.
- Motor de regras com grupos AND/OR aninhados.
- Critérios independentes de entrada, permanência, suspensão e conclusão.
- Monitoramento de XLSX, CSV, TXT e bancos SQL Server, MySQL, PostgreSQL, Firebird e SQLite.
- Correlação de registros entre múltiplas fontes pela chave composta.
- Múltiplos canais, destinatários, templates e ações por automação.
- Evolution API V1/V2 configurável, Telegram Bot, SMTP e alerta local.
- SQLite local, fila persistente, retentativas, idempotência e auditoria.
- Pipelines CI, CodeQL e Release para win-x86 e win-x64.
- Pacotes portáteis, instaladores, checksums e scripts de serviço.
