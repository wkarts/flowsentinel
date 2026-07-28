# FlowSentinel

FlowSentinel é uma plataforma Windows genérica para monitoramento orientado a regras. Ela lê múltiplas fontes, acompanha cada registro individualmente, mantém o estado de cada ocorrência e distribui notificações para vários canais e destinatários simultaneamente.

## Principais capacidades

- Vários clientes e situações na mesma planilha.
- Chave composta por registro, evitando confusão entre documentos do mesmo cliente.
- Múltiplas fontes correlacionadas pela mesma chave.
- Grupos lógicos AND/OR aninhados.
- Critérios separados de entrada, permanência, suspensão e conclusão.
- Várias ações por ocorrência.
- Vários canais por ação.
- Vários destinatários no mesmo canal.
- Destinatários fixos, vindos dos dados ou pertencentes a grupos.
- Templates com campos do registro.
- Fila persistente, retentativa, idempotência e auditoria.
- Aplicação em System Tray e modo Windows Service.
- Publicação autocontida para `win-x86` e `win-x64`.

## Stack

- C# / .NET 10 LTS
- WinForms
- Generic Host / BackgroundService
- SQLite + Entity Framework Core
- ClosedXML
- MailKit
- ADO.NET para SQL Server, MySQL, PostgreSQL, Firebird e SQLite

## Estrutura

```text
src/
├── FlowSentinel.Domain
├── FlowSentinel.Application
├── FlowSentinel.Infrastructure
├── FlowSentinel.Desktop
└── FlowSentinel.Service

tests/
├── FlowSentinel.Domain.Tests
└── FlowSentinel.Application.Tests
```

## Compilação local

Requisitos:

- Windows 10/11 ou Windows Server compatível.
- SDK .NET 10.0.301 ou patch posterior da mesma feature band.

```powershell
dotnet restore FlowSentinel.sln
dotnet build FlowSentinel.sln -c Release
dotnet test FlowSentinel.sln -c Release --no-build
```

## Execução desktop

```powershell
dotnet run --project src/FlowSentinel.Desktop/FlowSentinel.Desktop.csproj
```

Na primeira execução é criado o banco em:

```text
%LOCALAPPDATA%\FlowSentinel\data\flowsentinel.db
```

## Publicação local

```powershell
./scripts/publish.ps1 -Version 0.1.0
```

Os arquivos serão gerados em `artifacts/`.

## Criação de release

1. Atualize `eng/Version.props` e `CHANGELOG.md`.
2. Valide:

```powershell
./scripts/validate.ps1
```

3. Crie e envie a tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

O workflow de release irá:

- validar a versão da tag;
- restaurar, compilar e testar;
- publicar Desktop e Service para x86 e x64;
- assinar binários quando os secrets estiverem configurados;
- gerar ZIPs portáteis;
- gerar instaladores Inno Setup;
- gerar `SHA256SUMS.txt`;
- publicar a GitHub Release.

## Assinatura opcional

Cadastre estes secrets no GitHub:

```text
WINDOWS_CERTIFICATE_BASE64
WINDOWS_CERTIFICATE_PASSWORD
```

O primeiro deve conter o PFX convertido para Base64.

## Configuração das automações

A versão 0.1.0 utiliza um editor JSON validado para preservar toda a expressividade do motor sem limitar fontes, grupos lógicos, ações, canais ou destinatários. Um designer visual poderá ser adicionado sobre o mesmo modelo sem alterar o núcleo.

O Desktop permite importar e exportar a definição JSON completa de cada automação. Um exemplo funcional está em:

```text
docs/examples/automation-clientes.json
```

## Segredos protegidos

Use os botões dos editores de automação e canais para proteger somente o conteúdo do segredo. Os formatos são:

```text
dpapi-user:<conteudo-base64>
dpapi-machine:<conteudo-base64>
```

Use `dpapi-user` quando somente o usuário atual executará o Desktop. Use `dpapi-machine` para configurações que também serão lidas pelo Windows Service na mesma máquina.

## Evolution API

O conector possui perfis V1 e V2 e permite sobrescrever os caminhos HTTP e o formato do payload. Isso evita acoplamento rígido a uma única revisão da Evolution API.

## Desktop e Windows Service

Por padrão, o Desktop usa `%LOCALAPPDATA%\FlowSentinel` e o serviço usa `%PROGRAMDATA%\FlowSentinel`. O diretório pode ser substituído pela variável `FLOWSENTINEL_DATA_ROOT`.

Não execute os dois motores simultaneamente contra o mesmo banco. Use o Desktop como modo operacional ou como administrador de uma cópia parada do serviço. Para credenciais compartilhadas entre contas do Windows, use proteção `dpapi-machine`.

## Documentação adicional

- `docs/architecture.md`
- `docs/automation-schema.md`
- `docs/channel-settings.md`
- `docs/github-flow.md`
- `docs/release-process.md`
